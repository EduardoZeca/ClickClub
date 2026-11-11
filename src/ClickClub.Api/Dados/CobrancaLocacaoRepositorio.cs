using ClickClub.Api.Contratos;
using MySqlConnector;

namespace ClickClub.Api.Dados;

// Somente o titular escolhe como regularizar a taxa de uma reserva paga.
// O fluxo inteiro usa a ordem de locks: associação -> reserva/cobrança -> fatura.
public sealed class CobrancaLocacaoRepositorio(IConfiguration configuration)
{
    private readonly string _connectionString = configuration
        .GetConnectionString("ClickClub")
        ?? throw new InvalidOperationException("Conexão MySQL não configurada.");

    private sealed record AssociacaoLocacao(long Id, long UnidadeOrigemId,
        DateTime DataInicio, string FusoOrigem, string Status);

    private sealed record CobrancaLocacao(long Id, long ReservaId,
        long UnidadePrestadoraId, string ClubePrestador,
        string Espaco, string FusoPrestador, DateTime InicioUtc,
        string StatusReserva, string StatusCobranca,
        decimal ValorOriginal, DateTime? CompetenciaDestino,
        long? FaturaDestinoId, decimal AcrescimoRegistrado);

    private sealed record DestinoMensalidade(
        DateTime Competencia, long? FaturaId);

    public async Task<CobrancaLocacaoResposta?> ConsultarAsync(
        long reservaId, long titularUsuarioId, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        var associacao = await AssociacaoDoTitularAsync(
            conexao, null, reservaId, titularUsuarioId, ct);
        if (associacao is null) return null;

        var cobranca = await BuscarCobrancaAsync(
            conexao, null, reservaId, associacao.Id, ct);
        if (cobranca is null) return null; // Reserva gratuita não tem cobrança.

        var agoraUtc = DateTime.UtcNow;
        var podeDecidir = associacao.Status == "Ativa" &&
            cobranca.StatusReserva == "Confirmada" &&
            cobranca.StatusCobranca == "EmAberto" &&
            agoraUtc < cobranca.InicioUtc;

        DateTime? competencia = cobranca.CompetenciaDestino;
        long? faturaDestinoId = cobranca.FaturaDestinoId;
        if (podeDecidir)
        {
            var destino = await EscolherDestinoAsync(conexao, null,
                associacao, agoraUtc, ct);
            competencia = destino.Competencia;
            faturaDestinoId = destino.FaturaId;
        }

        var acrescimo = cobranca.StatusCobranca switch
        {
            "Transferida" => cobranca.AcrescimoRegistrado,
            "EmAberto" => CalcularAcrescimo(cobranca.ValorOriginal),
            _ => 0m
        };

        var fusoServico = ObterZona(cobranca.FusoPrestador);
        var inicioLocal = TimeZoneInfo.ConvertTimeFromUtc(
            cobranca.InicioUtc, fusoServico);

        var motivo = podeDecidir ? null : associacao.Status != "Ativa"
            ? "A associação não está ativa."
            : cobranca.StatusReserva != "Confirmada"
                ? "A reserva já não está confirmada."
                : agoraUtc >= cobranca.InicioUtc
                    ? "O prazo de escolha terminou ao iniciar a utilização."
                    : "Esta cobrança já teve uma decisão registrada.";

        return new CobrancaLocacaoResposta(
            cobranca.ReservaId, cobranca.ClubePrestador,
            cobranca.Espaco, inicioLocal, cobranca.StatusReserva,
            cobranca.StatusCobranca, cobranca.ValorOriginal, acrescimo,
            cobranca.ValorOriginal + acrescimo,
            competencia, competencia?.AddDays(9), faturaDestinoId,
            podeDecidir, motivo);
    }

    public async Task<DecisaoLocacaoResposta?> DecidirAsync(
        long reservaId, long titularUsuarioId, bool transferir,
        DateTime? competenciaConfirmada, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);

        // Leitura inicial apenas para identificar a associação do titular;
        // a autorização e o status são conferidos novamente após o lock.
        var associacao = await AssociacaoDoTitularAsync(
            conexao, transacao, reservaId, titularUsuarioId, ct);
        if (associacao is null) return null;
        using (var bloquear = new MySqlCommand("""
            SELECT status FROM associacao WHERE id=@id FOR UPDATE
            """, conexao, transacao))
        {
            bloquear.Parameters.AddWithValue("@id", associacao.Id);
            var statusAtual = await bloquear.ExecuteScalarAsync(ct) as string;
            if (statusAtual is null) return null;
            associacao = associacao with { Status = statusAtual };
        }

        var cobranca = await BuscarCobrancaAsync(
            conexao, transacao, reservaId, associacao.Id, ct);
        if (cobranca is null) return null;

        var statusDesejado = transferir ? "Transferida" : "QuitadaSimulada";
        if (cobranca.StatusCobranca == statusDesejado)
            return Responder(cobranca, true);
        if (cobranca.StatusCobranca != "EmAberto")
            throw new InvalidOperationException(
                "A cobrança já foi quitada, transferida ou cancelada.");

        var agoraUtc = DateTime.UtcNow;
        if (associacao.Status != "Ativa" ||
            cobranca.StatusReserva != "Confirmada")
            throw new InvalidOperationException(
                "A associação e a reserva precisam estar ativas para decidir a cobrança.");
        if (agoraUtc >= cobranca.InicioUtc)
            throw new InvalidOperationException(
                "O prazo de quitação ou transferência terminou no início da utilização.");

        if (!transferir)
        {
            using var quitar = new MySqlCommand("""
                UPDATE cobranca_reserva SET status='QuitadaSimulada',
                    decisao_em_utc=UTC_TIMESTAMP(6)
                WHERE id=@id AND status='EmAberto'
                """, conexao, transacao);
            quitar.Parameters.AddWithValue("@id", cobranca.Id);
            if (await quitar.ExecuteNonQueryAsync(ct) != 1)
                throw new InvalidOperationException("Cobrança modificada durante a quitação.");

            await LancarAsync(conexao, transacao, null, null,
                cobranca.Id, cobranca.UnidadePrestadoraId,
                "QuitacaoLocacaoAvulsa", -cobranca.ValorOriginal,
                titularUsuarioId, "Quitação avulsa exclusivamente simulada", ct);

            await LogAuditoria.RegistrarAsync(conexao, transacao,
                titularUsuarioId, cobranca.UnidadePrestadoraId,
                "Quitar", "CobrancaReserva", cobranca.Id,
                "Quitação avulsa simulada registrada pelo titular.", ct);
            await transacao.CommitAsync(ct);
            return Responder(cobranca with { StatusCobranca = statusDesejado }, false);
        }

        var destino = await EscolherDestinoAsync(
            conexao, transacao, associacao, agoraUtc, ct);
        if (competenciaConfirmada is null ||
            competenciaConfirmada.Value.Date != destino.Competencia ||
            competenciaConfirmada.Value.TimeOfDay != TimeSpan.Zero)
            throw new InvalidOperationException(
                "A competência de destino mudou ou não foi confirmada. Recarregue a prévia e confirme novamente.");
        var faturaId = destino.FaturaId;
        if (faturaId is null)
        {
            using var criarFatura = new MySqlCommand("""
                INSERT INTO fatura
                    (associacao_id, unidade_origem_id, tipo,
                     competencia, data_vencimento, valor_total, status)
                VALUES (@associacao, @origem, 'Mensalidade',
                        @competencia, @vencimento, 0, 'EmPreparacao')
                """, conexao, transacao);
            criarFatura.Parameters.AddWithValue("@associacao", associacao.Id);
            criarFatura.Parameters.AddWithValue("@origem", associacao.UnidadeOrigemId);
            criarFatura.Parameters.AddWithValue("@competencia", destino.Competencia);
            criarFatura.Parameters.AddWithValue("@vencimento", destino.Competencia.AddDays(9));
            await criarFatura.ExecuteNonQueryAsync(ct);
            faturaId = criarFatura.LastInsertedId;
        }

        var acrescimoNovo = CalcularAcrescimo(cobranca.ValorOriginal);
        var principalItemId = await InserirItemAsync(conexao, transacao,
            faturaId.Value, cobranca.ReservaId, cobranca.UnidadePrestadoraId,
            "LocacaoPrincipal", "Locação transferida para mensalidade",
            cobranca.ValorOriginal, ct);
        var acrescimoItemId = await InserirItemAsync(conexao, transacao,
            faturaId.Value, cobranca.ReservaId, cobranca.UnidadePrestadoraId,
            "AcrescimoTransferencia", "Acréscimo único de 10% pela transferência",
            acrescimoNovo, ct);

        using (var atualizarFatura = new MySqlCommand("""
            UPDATE fatura SET valor_total=valor_total+@valor
            WHERE id=@fatura AND status IN ('EmPreparacao','EmAberto')
            """, conexao, transacao))
        {
            atualizarFatura.Parameters.AddWithValue("@fatura", faturaId.Value);
            atualizarFatura.Parameters.AddWithValue("@valor",
                cobranca.ValorOriginal + acrescimoNovo);
            if (await atualizarFatura.ExecuteNonQueryAsync(ct) != 1)
                throw new InvalidOperationException("A fatura de destino deixou de estar elegível.");
        }

        using (var atualizarCobranca = new MySqlCommand("""
            UPDATE cobranca_reserva SET status='Transferida',
                modalidade='Mensalidade', competencia_destino=@competencia,
                fatura_destino_id=@fatura, percentual_acrescimo=10.00,
                valor_acrescimo=@acrescimo,
                decisao_em_utc=UTC_TIMESTAMP(6)
            WHERE id=@id AND status='EmAberto'
            """, conexao, transacao))
        {
            atualizarCobranca.Parameters.AddWithValue("@competencia", destino.Competencia);
            atualizarCobranca.Parameters.AddWithValue("@fatura", faturaId.Value);
            atualizarCobranca.Parameters.AddWithValue("@acrescimo", acrescimoNovo);
            atualizarCobranca.Parameters.AddWithValue("@id", cobranca.Id);
            if (await atualizarCobranca.ExecuteNonQueryAsync(ct) != 1)
                throw new InvalidOperationException("Cobrança modificada durante a transferência.");
        }

        await LancarAsync(conexao, transacao, faturaId.Value,
            principalItemId, cobranca.Id, cobranca.UnidadePrestadoraId,
            "TransferenciaLocacaoPrincipal", cobranca.ValorOriginal,
            titularUsuarioId, "Locação incluída em mensalidade futura", ct);
        await LancarAsync(conexao, transacao, faturaId.Value,
            acrescimoItemId, cobranca.Id, cobranca.UnidadePrestadoraId,
            "AcrescimoTransferencia", acrescimoNovo,
            titularUsuarioId, "Acréscimo único de 10%", ct);

        // Créditos podem existir mesmo antes da primeira emissão mensal.
        // Aplicar após incluir a locação evita fatura de valor negativo.
        await CreditoFinanceiroServico.AplicarAsync(conexao, transacao,
            associacao.Id, faturaId.Value, titularUsuarioId, ct);

        await LogAuditoria.RegistrarAsync(conexao, transacao,
            titularUsuarioId, cobranca.UnidadePrestadoraId,
            "Transferir", "CobrancaReserva", cobranca.Id,
            "Cobrança transferida para fatura futura, principal e 10% separados.", ct);
        await transacao.CommitAsync(ct);

        return Responder(cobranca with
        {
            StatusCobranca = statusDesejado,
            CompetenciaDestino = destino.Competencia,
            FaturaDestinoId = faturaId,
            AcrescimoRegistrado = acrescimoNovo
        }, false);
    }

    private static DecisaoLocacaoResposta Responder(
        CobrancaLocacao cobranca, bool jaRegistrada) => new(
            cobranca.ReservaId, cobranca.StatusCobranca, jaRegistrada,
            cobranca.ValorOriginal, cobranca.AcrescimoRegistrado,
            cobranca.CompetenciaDestino,
            cobranca.CompetenciaDestino?.AddDays(9),
            cobranca.FaturaDestinoId);

    private static decimal CalcularAcrescimo(decimal original) =>
        decimal.Round(original * 0.10m, 2, MidpointRounding.AwayFromZero);

    private static TimeZoneInfo ObterZona(string fuso)
    {
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(fuso, out var zona))
            throw new InvalidOperationException("Fuso horário inválido no cadastro do clube.");
        return zona;
    }

    private static async Task<AssociacaoLocacao?> AssociacaoDoTitularAsync(
        MySqlConnection conexao, MySqlTransaction? transacao,
        long reservaId, long usuarioId, CancellationToken ct)
    {
        using var cmd = new MySqlCommand("""
            SELECT a.id, a.unidade_origem_id, a.data_inicio,
                   u.fuso_horario, a.status
            FROM reserva r
            JOIN associacao a ON a.id=r.associacao_id
            JOIN unidade u ON u.id=a.unidade_origem_id
            JOIN usuario us ON us.pessoa_id=a.titular_pessoa_id
            WHERE r.id=@reserva AND us.id=@usuario
            """, conexao, transacao);
        cmd.Parameters.AddWithValue("@reserva", reservaId);
        cmd.Parameters.AddWithValue("@usuario", usuarioId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new AssociacaoLocacao(reader.GetInt64(0), reader.GetInt64(1),
            reader.GetDateTime(2), reader.GetString(3), reader.GetString(4));
    }

    private static async Task<CobrancaLocacao?> BuscarCobrancaAsync(
        MySqlConnection conexao, MySqlTransaction? transacao,
        long reservaId, long associacaoId, CancellationToken ct)
    {
        var trava = transacao is null ? "" : " FOR UPDATE";
        using var cmd = new MySqlCommand($"""
            SELECT c.id, r.id, e.unidade_id, u.nome, e.nome,
                   u.fuso_horario, r.inicio_utc, r.status,
                   c.status, c.valor_original, c.competencia_destino,
                   c.fatura_destino_id, c.valor_acrescimo
            FROM reserva r
            JOIN espaco e ON e.id=r.espaco_id
            JOIN unidade u ON u.id=e.unidade_id
            JOIN cobranca_reserva c ON c.reserva_id=r.id
            WHERE r.id=@reserva AND r.associacao_id=@associacao{trava}
            """, conexao, transacao);
        cmd.Parameters.AddWithValue("@reserva", reservaId);
        cmd.Parameters.AddWithValue("@associacao", associacaoId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new CobrancaLocacao(reader.GetInt64(0), reader.GetInt64(1),
            reader.GetInt64(2), reader.GetString(3), reader.GetString(4),
            reader.GetString(5),
            DateTime.SpecifyKind(reader.GetDateTime(6), DateTimeKind.Utc),
            reader.GetString(7), reader.GetString(8), reader.GetDecimal(9),
            reader.IsDBNull(10) ? null : reader.GetDateTime(10),
            reader.IsDBNull(11) ? null : reader.GetInt64(11),
            reader.GetDecimal(12));
    }

    private static async Task<DestinoMensalidade> EscolherDestinoAsync(
        MySqlConnection conexao, MySqlTransaction? transacao,
        AssociacaoLocacao associacao, DateTime agoraUtc, CancellationToken ct)
    {
        var hojeOrigem = RegraInadimplencia.DataLocal(
            agoraUtc, associacao.FusoOrigem);
        var proximoMes = new DateTime(hojeOrigem.Year, hojeOrigem.Month, 1)
            .AddMonths(1);
        var primeiraCompetencia = new DateTime(
            associacao.DataInicio.Year, associacao.DataInicio.Month, 1)
            .AddMonths(1);
        var competencia = proximoMes >= primeiraCompetencia
            ? proximoMes : primeiraCompetencia;

        for (var i = 0; i < 120; i++, competencia = competencia.AddMonths(1))
        {
            var trava = transacao is null ? "" : " FOR UPDATE";
            using var cmd = new MySqlCommand($"""
                SELECT id, status FROM fatura
                WHERE associacao_id=@associacao AND tipo='Mensalidade'
                  AND competencia=@competencia{trava}
                """, conexao, transacao);
            cmd.Parameters.AddWithValue("@associacao", associacao.Id);
            cmd.Parameters.AddWithValue("@competencia", competencia);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                return new DestinoMensalidade(competencia, null);

            var id = reader.GetInt64(0);
            var status = reader.GetString(1);
            if (status is "EmPreparacao" or "EmAberto")
                return new DestinoMensalidade(competencia, id);
        }
        throw new InvalidOperationException(
            "Não foi encontrada mensalidade futura elegível para transferência.");
    }

    private static async Task<long> InserirItemAsync(
        MySqlConnection conexao, MySqlTransaction transacao,
        long faturaId, long reservaId, long unidadeId,
        string tipo, string descricao, decimal valor, CancellationToken ct)
    {
        using var cmd = new MySqlCommand("""
            INSERT INTO item_fatura
                (fatura_id, tipo, descricao, valor, reserva_id, origem_unidade_id)
            VALUES (@fatura, @tipo, @descricao, @valor, @reserva, @origem)
            """, conexao, transacao);
        cmd.Parameters.AddWithValue("@fatura", faturaId);
        cmd.Parameters.AddWithValue("@tipo", tipo);
        cmd.Parameters.AddWithValue("@descricao", descricao);
        cmd.Parameters.AddWithValue("@valor", valor);
        cmd.Parameters.AddWithValue("@reserva", reservaId);
        cmd.Parameters.AddWithValue("@origem", unidadeId);
        await cmd.ExecuteNonQueryAsync(ct);
        return cmd.LastInsertedId;
    }

    private static async Task LancarAsync(
        MySqlConnection conexao, MySqlTransaction transacao,
        long? faturaId, long? itemId, long cobrancaId,
        long unidadeId, string tipo, decimal valor, long usuarioId,
        string observacao, CancellationToken ct)
    {
        using var cmd = new MySqlCommand("""
            INSERT INTO lancamento_financeiro
                (fatura_id, item_fatura_id, cobranca_reserva_id,
                 unidade_referencia_id, tipo, valor,
                 usuario_responsavel_id, observacao)
            VALUES (@fatura, @item, @cobranca, @unidade, @tipo,
                    @valor, @usuario, @observacao)
            """, conexao, transacao);
        cmd.Parameters.AddWithValue("@fatura",
            faturaId.HasValue ? (object)faturaId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@item",
            itemId.HasValue ? (object)itemId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@cobranca", cobrancaId);
        cmd.Parameters.AddWithValue("@unidade", unidadeId);
        cmd.Parameters.AddWithValue("@tipo", tipo);
        cmd.Parameters.AddWithValue("@valor", valor);
        cmd.Parameters.AddWithValue("@usuario", usuarioId);
        cmd.Parameters.AddWithValue("@observacao", observacao);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
