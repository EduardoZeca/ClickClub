using ClickClub.Api.Contratos;
using MySqlConnector;

namespace ClickClub.Api.Dados;

public sealed class ReservaRepositorio(IConfiguration configuration)
{
    private readonly string _connectionString = configuration
        .GetConnectionString("ClickClub")
        ?? throw new InvalidOperationException("Conexão MySQL não configurada.");

    public async Task<List<EspacoReservaOpcao>> OpcoesAsync(CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        using var cmd = new MySqlCommand("""
            SELECT e.id, u.nome, e.nome, e.tipo, e.capacidade,
                   e.taxa_base, u.fuso_horario
            FROM espaco e JOIN unidade u ON u.id=e.unidade_id
            WHERE e.ativo=TRUE AND u.status='Ativa'
              AND EXISTS (SELECT 1 FROM horario_espaco h
                          WHERE h.espaco_id=e.id AND h.ativo=TRUE)
            ORDER BY u.nome, e.nome
            """, conexao);
        var lista = new List<EspacoReservaOpcao>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            lista.Add(new EspacoReservaOpcao(reader.GetInt64(0),
                reader.GetString(1), reader.GetString(2),
                reader.GetString(3), reader.GetInt32(4), reader.GetDecimal(5),
                reader.GetString(6)));
        return lista;
    }

    public async Task<List<ReservaResposta>> MinhasAsync(
        long usuarioId, bool titular, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        using var cmd = new MySqlCommand("""
            SELECT r.id, u.nome, e.nome, e.tipo,
                   r.inicio_utc, r.fim_utc, r.finalidade,
                   r.quantidade_participantes, r.valor_contratado, r.status,
                   COALESCE(c.status, 'Gratuita'), u.fuso_horario, resp.nome
            FROM reserva r
            JOIN associacao a ON a.id=r.associacao_id
            JOIN usuario us ON us.id=@usuario
            JOIN pessoa resp ON resp.id=r.responsavel_pessoa_id
            JOIN espaco e ON e.id=r.espaco_id
            JOIN unidade u ON u.id=e.unidade_id
            LEFT JOIN cobranca_reserva c ON c.reserva_id=r.id
            WHERE (@titular=TRUE AND us.pessoa_id=a.titular_pessoa_id)
               OR (@titular=FALSE AND us.pessoa_id=r.responsavel_pessoa_id
                   AND EXISTS (SELECT 1 FROM dependente_associacao d
                       WHERE d.associacao_id=a.id AND d.dependente_pessoa_id=us.pessoa_id
                         AND d.ativo=TRUE))
            -- Histórico em ordem de cadastro, independentemente da data de uso.
            ORDER BY r.criada_em_utc DESC, r.id DESC
            """, conexao);
        cmd.Parameters.AddWithValue("@usuario", usuarioId);
        cmd.Parameters.AddWithValue("@titular", titular);
        return await LerReservasAsync(cmd, ct);
    }

    public async Task<ReservaResposta?> BuscarMinhaAsync(
        long id, long usuarioId, bool titular, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        using var cmd = new MySqlCommand("""
            SELECT r.id, u.nome, e.nome, e.tipo,
                   r.inicio_utc, r.fim_utc, r.finalidade,
                   r.quantidade_participantes, r.valor_contratado, r.status,
                   COALESCE(c.status, 'Gratuita'), u.fuso_horario, resp.nome
            FROM reserva r
            JOIN associacao a ON a.id=r.associacao_id
            JOIN usuario us ON us.id=@usuario
            JOIN pessoa resp ON resp.id=r.responsavel_pessoa_id
            JOIN espaco e ON e.id=r.espaco_id
            JOIN unidade u ON u.id=e.unidade_id
            LEFT JOIN cobranca_reserva c ON c.reserva_id=r.id
            WHERE r.id=@id AND (
                (@titular=TRUE AND us.pessoa_id=a.titular_pessoa_id)
                OR (@titular=FALSE AND us.pessoa_id=r.responsavel_pessoa_id
                    AND EXISTS (SELECT 1 FROM dependente_associacao d
                        WHERE d.associacao_id=a.id AND d.dependente_pessoa_id=us.pessoa_id
                          AND d.ativo=TRUE)))
            """, conexao);
        cmd.Parameters.AddWithValue("@usuario", usuarioId);
        cmd.Parameters.AddWithValue("@titular", titular);
        cmd.Parameters.AddWithValue("@id", id);
        return (await LerReservasAsync(cmd, ct)).FirstOrDefault();
    }

    public async Task<(ResultadoReserva Resultado, long Id)> CriarAsync(
        ReservaEntrada entrada, long usuarioId, bool titular, CancellationToken ct)
    {
        if (entrada.InicioLocal is null || entrada.FimLocal is null ||
            entrada.InicioLocal.Value.Kind != DateTimeKind.Unspecified ||
            entrada.FimLocal.Value.Kind != DateTimeKind.Unspecified ||
            entrada.FimLocal <= entrada.InicioLocal)
            return (ResultadoReserva.PeriodoInvalido, 0);

        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);

        // Sempre bloquear primeiro a associação: duas tentativas simultâneas
        // em clubes diferentes não podem superar a cota mensal.
        long associacaoId, pessoaId, vinculoId;
        DateTime? nascimento;
        var sqlAssociacao = titular
            ? """
              SELECT a.id, us.pessoa_id, NULL AS vinculo, NULL AS nascimento
              FROM usuario us
              JOIN associacao a ON a.titular_pessoa_id=us.pessoa_id
              JOIN pessoa p ON p.id=us.pessoa_id
              WHERE us.id=@usuario AND a.status='Ativa' AND p.status='Ativa'
              FOR UPDATE
              """
            : """
              SELECT a.id, us.pessoa_id, d.id AS vinculo, p.data_nascimento
              FROM usuario us
              JOIN dependente_associacao d
                  ON d.dependente_pessoa_id=us.pessoa_id AND d.ativo=TRUE
              JOIN associacao a ON a.id=d.associacao_id AND a.status='Ativa'
              JOIN pessoa p ON p.id=us.pessoa_id AND p.status='Ativa'
              WHERE us.id=@usuario
              FOR UPDATE
              """;
        using (var identificar = new MySqlCommand(sqlAssociacao, conexao, transacao))
        {
            identificar.Parameters.AddWithValue("@usuario", usuarioId);
            await using var reader = await identificar.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                return (ResultadoReserva.SemAssociacaoAtiva, 0);
            associacaoId = reader.GetInt64(0);
            pessoaId = reader.GetInt64(1);
            vinculoId = reader.IsDBNull(2) ? 0 : reader.GetInt64(2);
            nascimento = reader.IsDBNull(3) ? null : reader.GetDateTime(3);
        }

        // RN07: o bloqueio é recalculado na própria tentativa de reserva,
        // sem depender de processamento agendado da multa.
        string fusoOrigem;
        using (var origem = new MySqlCommand("""
            SELECT u.fuso_horario FROM associacao a
            JOIN unidade u ON u.id=a.unidade_origem_id
            WHERE a.id=@associacao
            """, conexao, transacao))
        {
            origem.Parameters.AddWithValue("@associacao", associacaoId);
            var resposta = await origem.ExecuteScalarAsync(ct);
            if (resposta is not string fusoCadastrado)
                return (ResultadoReserva.FusoInvalido, 0);
            fusoOrigem = fusoCadastrado;
        }

        DateTime hojeOrigem;
        try
        {
            hojeOrigem = RegraInadimplencia.DataLocal(
                DateTime.UtcNow, fusoOrigem);
        }
        catch (InvalidOperationException)
        {
            return (ResultadoReserva.FusoInvalido, 0);
        }

        using (var atraso = new MySqlCommand("""
            SELECT EXISTS(SELECT 1 FROM fatura f
                WHERE f.associacao_id=@associacao
                  AND f.tipo='Mensalidade' AND f.status='EmAberto'
                  AND f.valor_total > 0 AND f.data_vencimento < @limite)
            """, conexao, transacao))
        {
            atraso.Parameters.AddWithValue("@associacao", associacaoId);
            atraso.Parameters.AddWithValue("@limite",
                hojeOrigem.AddDays(-RegraInadimplencia.DiasTolerancia));
            if (Convert.ToInt32(await atraso.ExecuteScalarAsync(ct)) == 1)
                return (ResultadoReserva.MensalidadeInadimplente, 0);
        }

        long unidadeId;
        int capacidade;
        string tipo, fuso;
        decimal taxa;
        using (var espaco = new MySqlCommand("""
            SELECT e.unidade_id, e.tipo, e.taxa_base, u.fuso_horario,
                   e.ativo, u.status, e.capacidade
            FROM espaco e JOIN unidade u ON u.id=e.unidade_id
            WHERE e.id=@espaco FOR UPDATE
            """, conexao, transacao))
        {
            espaco.Parameters.AddWithValue("@espaco", entrada.EspacoId);
            await using var reader = await espaco.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct) || !reader.GetBoolean(4) ||
                reader.GetString(5) != "Ativa")
                return (ResultadoReserva.EspacoIndisponivel, 0);
            unidadeId = reader.GetInt64(0);
            tipo = reader.GetString(1);
            taxa = reader.GetDecimal(2);
            fuso = reader.GetString(3);
            capacidade = reader.GetInt32(6);
        }

        if (!titular && taxa > 0 &&
            PermissaoReservaDependenteRepositorio.PrecisaAutorizacao(nascimento))
        {
            using var permissao = new MySqlCommand("""
                SELECT permitir_reserva_paga
                FROM autorizacao_reserva_dependente
                WHERE dependente_associacao_id=@vinculo
                """, conexao, transacao);
            permissao.Parameters.AddWithValue("@vinculo", vinculoId);
            var concedida = await permissao.ExecuteScalarAsync(ct);
            if (concedida is null || Convert.ToInt32(concedida) != 1)
                return (ResultadoReserva.PermissaoTitularNecessaria, 0);
        }

        if (entrada.QuantidadeParticipantes is < 1 or > 10000 ||
            entrada.QuantidadeParticipantes > capacidade)
            return (ResultadoReserva.CapacidadeExcedida, 0);

        var recebeConvidados = tipo is "SalaoFestas" or "Churrasqueira";
        if (!PersistenciaConvidados.ListaValida(
                entrada.Convidados, entrada.QuantidadeParticipantes))
            return (ResultadoReserva.ListaConvidadosInvalida, 0);
        if (!recebeConvidados && entrada.Convidados.Count > 0)
            return (ResultadoReserva.ListaConvidadosInvalida, 0);
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(fuso, out var zona))
            return (ResultadoReserva.FusoInvalido, 0);

        var inicioLocal = entrada.InicioLocal.Value;
        var fimLocal = entrada.FimLocal.Value;
        if (zona.IsInvalidTime(inicioLocal) || zona.IsInvalidTime(fimLocal) ||
            zona.IsAmbiguousTime(inicioLocal) || zona.IsAmbiguousTime(fimLocal))
            return (ResultadoReserva.PeriodoInvalido, 0);

        DateTime inicioUtc, fimUtc;
        try
        {
            inicioUtc = TimeZoneInfo.ConvertTimeToUtc(inicioLocal, zona);
            fimUtc = TimeZoneInfo.ConvertTimeToUtc(fimLocal, zona);
        }
        catch (ArgumentException)
        {
            return (ResultadoReserva.PeriodoInvalido, 0);
        }
        var agoraUtc = DateTime.UtcNow;
        if (fimUtc <= inicioUtc || inicioUtc <= agoraUtc)
            return (ResultadoReserva.PeriodoInvalido, 0);

        if (tipo == "SalaoFestas" &&
            inicioUtc < agoraUtc.AddHours(24))
            return (ResultadoReserva.AntecedenciaInsuficiente, 0);

        if (recebeConvidados && inicioUtc <= agoraUtc.AddHours(24) &&
            !entrada.ListaConvidadosCompleta)
            return (ResultadoReserva.ListaConvidadosIncompleta, 0);

        if (!await EstaNoFuncionamentoAsync(conexao, transacao,
                entrada.EspacoId, inicioLocal, fimLocal, ct))
            return (ResultadoReserva.ForaDoHorario, 0);

        using (var manutencao = new MySqlCommand("""
            SELECT EXISTS (SELECT 1 FROM indisponibilidade_espaco m
                WHERE m.espaco_id=@espaco AND m.ativo=TRUE
                  AND m.inicio_utc < @fim AND m.fim_utc > @inicio)
            """, conexao, transacao))
        {
            PreencherIntervalo(manutencao, entrada.EspacoId, inicioUtc, fimUtc);
            if (Convert.ToInt32(await manutencao.ExecuteScalarAsync(ct)) == 1)
                return (ResultadoReserva.Manutencao, 0);
        }

        using (var conflito = new MySqlCommand("""
            SELECT EXISTS (SELECT 1 FROM reserva r
                WHERE r.espaco_id=@espaco AND r.status='Confirmada'
                  AND r.inicio_utc < @fim AND r.fim_utc > @inicio)
            """, conexao, transacao))
        {
            PreencherIntervalo(conflito, entrada.EspacoId, inicioUtc, fimUtc);
            if (Convert.ToInt32(await conflito.ExecuteScalarAsync(ct)) == 1)
                return (ResultadoReserva.ConflitoReserva, 0);
        }

        // RN27: só conta o mês da data LOCAL de início, em qualquer clube.
        var primeiroDiaMes = new DateTime(inicioLocal.Year, inicioLocal.Month, 1);
        var primeiroDiaSeguinte = primeiroDiaMes.AddMonths(1);
        using (var cota = new MySqlCommand("""
            SELECT COUNT(*) FROM reserva
            WHERE associacao_id=@associacao AND status='Confirmada'
              AND inicio_local_data >= @inicioMes
              AND inicio_local_data < @fimMes
            """, conexao, transacao))
        {
            cota.Parameters.AddWithValue("@associacao", associacaoId);
            cota.Parameters.AddWithValue("@inicioMes", primeiroDiaMes);
            cota.Parameters.AddWithValue("@fimMes", primeiroDiaSeguinte);
            if (Convert.ToInt64(await cota.ExecuteScalarAsync(ct)) >= 3)
                return (ResultadoReserva.CotaEsgotada, 0);
        }

        using var reservar = new MySqlCommand("""
            INSERT INTO reserva
                (espaco_id, associacao_id, responsavel_pessoa_id,
                 quantidade_participantes,
                 inicio_utc, fim_utc, inicio_local_data, finalidade,
                 valor_contratado, tipo_espaco_contratado,
                 status, criada_em_utc)
            VALUES
                (@espaco, @associacao, @pessoa, @participantes,
                 @inicio, @fim, @dataLocal,
                 @finalidade, @valor, @tipo, 'Confirmada', UTC_TIMESTAMP(6))
            """, conexao, transacao);
        reservar.Parameters.AddWithValue("@espaco", entrada.EspacoId);
        reservar.Parameters.AddWithValue("@associacao", associacaoId);
        reservar.Parameters.AddWithValue("@pessoa", pessoaId);
        reservar.Parameters.AddWithValue("@participantes", entrada.QuantidadeParticipantes);
        reservar.Parameters.AddWithValue("@inicio", SemFuso(inicioUtc));
        reservar.Parameters.AddWithValue("@fim", SemFuso(fimUtc));
        reservar.Parameters.AddWithValue("@dataLocal", inicioLocal.Date);
        reservar.Parameters.AddWithValue("@finalidade",
            string.IsNullOrWhiteSpace(entrada.Finalidade)
                ? (object)DBNull.Value : entrada.Finalidade.Trim());
        reservar.Parameters.AddWithValue("@valor", taxa);
        reservar.Parameters.AddWithValue("@tipo", tipo);
        await reservar.ExecuteNonQueryAsync(ct);
        var reservaId = reservar.LastInsertedId;

        if (taxa > 0)
        {
            using var cobrar = new MySqlCommand("""
                INSERT INTO cobranca_reserva
                    (reserva_id, valor_original, status, modalidade)
                VALUES (@reserva, @valor, 'EmAberto', 'Avulsa')
                """, conexao, transacao);
            cobrar.Parameters.AddWithValue("@reserva", reservaId);
            cobrar.Parameters.AddWithValue("@valor", taxa);
            await cobrar.ExecuteNonQueryAsync(ct);
        }

        if (recebeConvidados)
            await PersistenciaConvidados.SincronizarAsync(conexao, transacao,
                reservaId, entrada.Convidados, ct);

        await LogAuditoria.RegistrarAsync(conexao, transacao, usuarioId,
            unidadeId, "Confirmar", "Reserva", reservaId,
            "Reserva confirmada e taxa simulada registrada quando aplicável.", ct);
        await transacao.CommitAsync(ct);
        return (ResultadoReserva.Concluida, reservaId);
    }

    private static async Task<List<ReservaResposta>> LerReservasAsync(
        MySqlCommand cmd, CancellationToken ct)
    {
        var lista = new List<ReservaResposta>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var fuso = reader.GetString(11);
            if (!TimeZoneInfo.TryFindSystemTimeZoneById(fuso, out var zona))
                throw new InvalidOperationException("Fuso horário inválido no cadastro do clube.");
            lista.Add(new ReservaResposta(reader.GetInt64(0),
                reader.GetString(1), reader.GetString(2), reader.GetString(3),
                ParaLocal(reader.GetDateTime(4), zona),
                ParaLocal(reader.GetDateTime(5), zona),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.GetInt32(7), reader.GetDecimal(8),
                reader.GetString(9), reader.GetString(10), reader.GetString(12)));
        }
        return lista;
    }

    private static async Task<bool> EstaNoFuncionamentoAsync(
        MySqlConnection conexao, MySqlTransaction transacao,
        long espacoId, DateTime inicioLocal, DateTime fimLocal, CancellationToken ct)
    {
        using var cmd = new MySqlCommand("""
            SELECT dia_semana, inicio, fim, fim_dia_seguinte
            FROM horario_espaco WHERE espaco_id=@espaco AND ativo=TRUE
            """, conexao, transacao);
        cmd.Parameters.AddWithValue("@espaco", espacoId);
        var faixas = new List<(int Dia, TimeSpan Inicio, TimeSpan Fim, bool Seguinte)>();
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct))
                faixas.Add((Convert.ToInt32(reader.GetValue(0)),
                    reader.GetTimeSpan(1), reader.GetTimeSpan(2),
                    reader.GetBoolean(3)));

        // A grade é semanal. Se cobrir 14 dias consecutivos,
        // a repetição garante cobertura também nos períodos maiores.
        var fimVerificacao = fimLocal - inicioLocal > TimeSpan.FromDays(14)
            ? inicioLocal.AddDays(14) : fimLocal;
        var cursor = inicioLocal;
        while (cursor < fimVerificacao)
        {
            var diaIso = ((int)cursor.DayOfWeek + 6) % 7;
            var segunda = cursor.Date.AddDays(-diaIso);
            var ate = cursor;
            foreach (var faixa in faixas)
                foreach (var deslocamento in new[] { -7, 0, 7 })
                {
                    var inicio = segunda.AddDays(faixa.Dia - 1 + deslocamento)
                        .Add(faixa.Inicio);
                    var fim = segunda.AddDays(faixa.Dia - 1 + deslocamento +
                        (faixa.Seguinte ? 1 : 0)).Add(faixa.Fim);
                    if (inicio <= cursor && fim > ate) ate = fim;
                }
            if (ate == cursor) return false;
            cursor = ate;
        }
        return true;
    }

    private static void PreencherIntervalo(MySqlCommand cmd,
        long espacoId, DateTime inicioUtc, DateTime fimUtc)
    {
        cmd.Parameters.AddWithValue("@espaco", espacoId);
        cmd.Parameters.AddWithValue("@inicio", SemFuso(inicioUtc));
        cmd.Parameters.AddWithValue("@fim", SemFuso(fimUtc));
    }

    private static DateTime SemFuso(DateTime instanteUtc) =>
        DateTime.SpecifyKind(instanteUtc, DateTimeKind.Unspecified);

    private static DateTime ParaLocal(DateTime utcSemFuso, TimeZoneInfo zona) =>
        DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utcSemFuso, DateTimeKind.Utc), zona),
            DateTimeKind.Unspecified);
}
