using ClickClub.Api.Contratos;
using MySqlConnector;

namespace ClickClub.Api.Dados;

public sealed class FaturaRepositorio(IConfiguration configuration)
{
    private readonly string _connectionString = configuration
        .GetConnectionString("ClickClub")
        ?? throw new InvalidOperationException("Conexão MySQL não configurada.");

    public async Task<EmissaoMensalidadeResposta> EmitirAsync(
        EmissaoMensalidadeEntrada entrada, long autorId, CancellationToken ct)
    {
        if (entrada.Competencia is null || entrada.Competencia.Value.Day != 1 ||
            entrada.Competencia.Value.TimeOfDay != TimeSpan.Zero)
            throw new ArgumentException("Informe o primeiro dia da competência.");

        var competencia = entrada.Competencia.Value.Date;
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);

        // Mesma ordem de locks utilizada nas reservas: associação primeiro.
        long clubeId, categoriaId;
        DateTime inicioAssociacao;
        string statusAssociacao;
        using (var comando = new MySqlCommand("""
            SELECT unidade_origem_id, categoria_id, data_inicio, status
            FROM associacao WHERE id=@associacao FOR UPDATE
            """, conexao, transacao))
        {
            comando.Parameters.AddWithValue("@associacao", entrada.AssociacaoId);
            await using var reader = await comando.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                throw new ArgumentException("Associação não encontrada.");
            clubeId = reader.GetInt64(0);
            categoriaId = reader.GetInt64(1);
            inicioAssociacao = reader.GetDateTime(2);
            statusAssociacao = reader.GetString(3);
        }

        if (statusAssociacao == "Cancelada")
            throw new InvalidOperationException(
                "Associação cancelada não pode receber nova mensalidade.");

        var primeiraCompetencia = new DateTime(inicioAssociacao.Year,
            inicioAssociacao.Month, 1).AddMonths(1);
        if (competencia < primeiraCompetencia)
            throw new InvalidOperationException(
                $"Primeira competência elegível: {primeiraCompetencia:MM/yyyy}.");

        long faturaId = 0;
        string? statusExistente = null;
        using (var buscar = new MySqlCommand("""
            SELECT id, status FROM fatura
            WHERE associacao_id=@associacao AND tipo='Mensalidade'
              AND competencia=@competencia FOR UPDATE
            """, conexao, transacao))
        {
            buscar.Parameters.AddWithValue("@associacao", entrada.AssociacaoId);
            buscar.Parameters.AddWithValue("@competencia", competencia);
            await using var reader = await buscar.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                faturaId = reader.GetInt64(0);
                statusExistente = reader.GetString(1);
            }
        }

        if (statusExistente is "EmAberto" or "QuitadaSimulada")
            return new EmissaoMensalidadeResposta(faturaId, true);
        if (statusExistente == "Cancelada")
            throw new InvalidOperationException(
                "Fatura cancelada não pode ser reaberta.");

        long politicaId;
        decimal valorMensal;
        using (var politica = new MySqlCommand("""
            SELECT id, valor_mensal FROM politica_financeira
            WHERE unidade_id=@unidade AND categoria_id=@categoria
              AND vigencia_inicio<=@competencia
              AND (vigencia_fim IS NULL OR vigencia_fim>=@competencia)
            ORDER BY vigencia_inicio DESC LIMIT 1
            """, conexao, transacao))
        {
            politica.Parameters.AddWithValue("@unidade", clubeId);
            politica.Parameters.AddWithValue("@categoria", categoriaId);
            politica.Parameters.AddWithValue("@competencia", competencia);
            await using var reader = await politica.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                throw new InvalidOperationException(
                    "Cadastre uma política financeira para o clube e a categoria desta competência.");
            politicaId = reader.GetInt64(0);
            valorMensal = reader.GetDecimal(1);
        }

        if (statusExistente is null)
        {
            using var inserir = new MySqlCommand("""
                INSERT INTO fatura
                    (associacao_id, unidade_origem_id, tipo, competencia,
                     data_vencimento, valor_total, status)
                VALUES (@associacao, @unidade, 'Mensalidade', @competencia,
                        @vencimento, 0, 'EmPreparacao')
                """, conexao, transacao);
            inserir.Parameters.AddWithValue("@associacao", entrada.AssociacaoId);
            inserir.Parameters.AddWithValue("@unidade", clubeId);
            inserir.Parameters.AddWithValue("@competencia", competencia);
            inserir.Parameters.AddWithValue("@vencimento", competencia.AddDays(9));
            await inserir.ExecuteNonQueryAsync(ct);
            faturaId = inserir.LastInsertedId;
        }

        using var item = new MySqlCommand("""
            INSERT INTO item_fatura
                (fatura_id, tipo, descricao, valor, origem_unidade_id)
            VALUES (@fatura, 'MensalidadeBase',
                    CONCAT('Mensalidade ', DATE_FORMAT(@competencia, '%m/%Y')),
                    @valor, @unidade)
            """, conexao, transacao);
        item.Parameters.AddWithValue("@fatura", faturaId);
        item.Parameters.AddWithValue("@competencia", competencia);
        item.Parameters.AddWithValue("@valor", valorMensal);
        item.Parameters.AddWithValue("@unidade", clubeId);
        await item.ExecuteNonQueryAsync(ct);
        var itemId = item.LastInsertedId;

        using (var atualizar = new MySqlCommand("""
            UPDATE fatura SET politica_financeira_id=@politica,
                valor_total=valor_total+@valor, status='EmAberto',
                emitida_em_utc=UTC_TIMESTAMP(6)
            WHERE id=@fatura AND status='EmPreparacao'
            """, conexao, transacao))
        {
            atualizar.Parameters.AddWithValue("@politica", politicaId);
            atualizar.Parameters.AddWithValue("@valor", valorMensal);
            atualizar.Parameters.AddWithValue("@fatura", faturaId);
            if (await atualizar.ExecuteNonQueryAsync(ct) != 1)
                throw new InvalidOperationException("Fatura modificada durante a emissão.");
        }

        using (var lancar = new MySqlCommand("""
            INSERT INTO lancamento_financeiro
                (fatura_id, item_fatura_id, unidade_referencia_id,
                 tipo, valor, usuario_responsavel_id, observacao)
            VALUES (@fatura, @item, @unidade,
                    'EmissaoMensalidade', @valor, @usuario,
                    'Mensalidade emitida de forma simulada')
            """, conexao, transacao))
        {
            lancar.Parameters.AddWithValue("@fatura", faturaId);
            lancar.Parameters.AddWithValue("@item", itemId);
            lancar.Parameters.AddWithValue("@unidade", clubeId);
            lancar.Parameters.AddWithValue("@valor", valorMensal);
            lancar.Parameters.AddWithValue("@usuario", autorId);
            await lancar.ExecuteNonQueryAsync(ct);
        }

        // Compensa créditos disponíveis na próxima fatura emitida;
        // saldo excedente permanece para competência posterior.
        await CreditoFinanceiroServico.AplicarAsync(conexao, transacao,
            entrada.AssociacaoId, faturaId, autorId, ct);

        await LogAuditoria.RegistrarAsync(conexao, transacao, autorId,
            clubeId, "Emitir", "Fatura", faturaId,
            "Mensalidade simulada emitida para a competência.", ct);
        await transacao.CommitAsync(ct);
        return new EmissaoMensalidadeResposta(faturaId, false);
    }

    public Task<List<FaturaResumoResposta>> ListarAdministracaoAsync(CancellationToken ct) =>
        ListarAsync(null, false, ct);

    public Task<List<FaturaResumoResposta>> ListarTitularAsync(
        long usuarioId, CancellationToken ct) => ListarAsync(usuarioId, false, ct);

    public Task<List<FaturaResumoResposta>> ListarTesoureiroAsync(
        long usuarioId, CancellationToken ct) => ListarAsync(usuarioId, true, ct);

    private async Task<List<FaturaResumoResposta>> ListarAsync(
        long? usuarioId, bool tesoureiro, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        using var comando = new MySqlCommand("""
            SELECT f.id, f.associacao_id, a.matricula, p.nome, u.nome,
                   f.competencia, f.data_vencimento, f.valor_total, f.status,
                   u.fuso_horario, EXISTS (
                       SELECT 1 FROM item_fatura multa
                       WHERE multa.fatura_id=f.id AND multa.tipo='MultaAtraso')
            FROM fatura f
            JOIN associacao a ON a.id=f.associacao_id
            JOIN pessoa p ON p.id=a.titular_pessoa_id
            JOIN unidade u ON u.id=f.unidade_origem_id
            WHERE (@usuario IS NULL OR (@tesoureiro=TRUE AND EXISTS (
                SELECT 1 FROM papel_usuario pu
                WHERE pu.usuario_id=@usuario AND pu.papel='Tesoureiro'
                  AND pu.unidade_id=f.unidade_origem_id)) OR
              (@tesoureiro=FALSE AND EXISTS (
                SELECT 1 FROM usuario us
                WHERE us.id=@usuario AND us.pessoa_id=a.titular_pessoa_id)))
            ORDER BY f.competencia DESC, f.id DESC
            """, conexao);
        comando.Parameters.AddWithValue("@usuario",
            usuarioId.HasValue ? (object)usuarioId.Value : DBNull.Value);
        comando.Parameters.AddWithValue("@tesoureiro", tesoureiro);
        return await LerFaturasAsync(comando, ct);
    }

    public async Task<FaturaDetalheResposta?> BuscarAsync(
        long faturaId, long usuarioId, bool administrador,
        bool tesoureiro, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        using var comando = new MySqlCommand("""
            SELECT f.id, f.associacao_id, a.matricula, p.nome, u.nome,
                   f.competencia, f.data_vencimento, f.valor_total, f.status,
                   u.fuso_horario, EXISTS (
                       SELECT 1 FROM item_fatura multa
                       WHERE multa.fatura_id=f.id AND multa.tipo='MultaAtraso')
            FROM fatura f
            JOIN associacao a ON a.id=f.associacao_id
            JOIN pessoa p ON p.id=a.titular_pessoa_id
            JOIN unidade u ON u.id=f.unidade_origem_id
            WHERE f.id=@fatura AND (@administrador=TRUE OR EXISTS (
                SELECT 1 FROM usuario us
                WHERE us.id=@usuario AND us.pessoa_id=a.titular_pessoa_id)
                OR (@tesoureiro=TRUE AND EXISTS (
                    SELECT 1 FROM papel_usuario pu
                    WHERE pu.usuario_id=@usuario AND pu.papel='Tesoureiro'
                      AND pu.unidade_id=f.unidade_origem_id)))
            """, conexao);
        comando.Parameters.AddWithValue("@fatura", faturaId);
        comando.Parameters.AddWithValue("@administrador", administrador);
        comando.Parameters.AddWithValue("@tesoureiro", tesoureiro);
        comando.Parameters.AddWithValue("@usuario", usuarioId);
        var resumo = (await LerFaturasAsync(comando, ct)).FirstOrDefault();
        if (resumo is null) return null;

        using var itensCmd = new MySqlCommand("""
            SELECT i.id, i.tipo, i.descricao, i.valor, i.reserva_id, u.nome
            FROM item_fatura i
            LEFT JOIN unidade u ON u.id=i.origem_unidade_id
            WHERE i.fatura_id=@fatura
            ORDER BY i.id
            """, conexao);
        itensCmd.Parameters.AddWithValue("@fatura", faturaId);
        var itens = new List<ItemFaturaResposta>();
        await using var reader = await itensCmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            itens.Add(new(reader.GetInt64(0), reader.GetString(1),
                reader.GetString(2), reader.GetDecimal(3),
                reader.IsDBNull(4) ? null : reader.GetInt64(4),
                reader.IsDBNull(5) ? null : reader.GetString(5)));
        return new FaturaDetalheResposta(resumo, itens);
    }

    private static async Task<List<FaturaResumoResposta>> LerFaturasAsync(
        MySqlCommand comando, CancellationToken ct)
    {
        var lista = new List<FaturaResumoResposta>();
        var agoraUtc = DateTime.UtcNow;
        await using var reader = await comando.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var vencimento = reader.GetDateTime(6);
            var total = reader.GetDecimal(7);
            var status = reader.GetString(8);
            var hojeLocal = RegraInadimplencia.DataLocal(agoraUtc,
                reader.GetString(9));
            var diasAtraso = RegraInadimplencia.DiasAtraso(
                vencimento, hojeLocal);
            var bloqueada = RegraInadimplencia.Bloqueia(
                status, total, vencimento, hojeLocal);
            var multaPendente = RegraInadimplencia.MultaPendente(
                status, total, vencimento, hojeLocal,
                Convert.ToInt32(reader.GetValue(10)) == 1);
            lista.Add(new(reader.GetInt64(0), reader.GetInt64(1),
                reader.GetString(2), reader.GetString(3), reader.GetString(4),
                reader.GetDateTime(5), vencimento, total, status,
                diasAtraso, bloqueada, multaPendente,
                total + multaPendente));
        }
        return lista;
    }
}
