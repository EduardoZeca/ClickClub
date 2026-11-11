using ClickClub.Api.Contratos;
using MySqlConnector;

namespace ClickClub.Api.Dados;

public sealed class TurmaRepositorio(IConfiguration configuration)
{
    private readonly string _connectionString = configuration
        .GetConnectionString("ClickClub")
        ?? throw new InvalidOperationException("Conexão MySQL não configurada.");

    private const string ConsultaTurmas = """
        SELECT t.id, t.atividade_id, a.nome, a.tipo,
               t.unidade_id, u.nome, u.fuso_horario,
               t.instrutor_pessoa_id, p.nome, t.inicio_utc, t.fim_utc,
               t.capacidade, t.status
        FROM turma t
        JOIN atividade a ON a.id=t.atividade_id
        JOIN unidade u ON u.id=t.unidade_id
        LEFT JOIN pessoa p ON p.id=t.instrutor_pessoa_id
        WHERE (@unidade IS NULL OR t.unidade_id=@unidade)
        """;

    public async Task<List<TurmaItem>> ListarAsync(long? unidadeId,
        CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        using var cmd = new MySqlCommand(ConsultaTurmas +
            " ORDER BY t.inicio_utc, u.nome, a.nome, t.id", conexao);
        cmd.Parameters.AddWithValue("@unidade", unidadeId.HasValue
            ? unidadeId.Value : DBNull.Value);
        var itens = new List<TurmaItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) itens.Add(Ler(reader));
        return itens;
    }

    public async Task<TurmaItem?> BuscarAsync(long id, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        using var cmd = new MySqlCommand(ConsultaTurmas + " AND t.id=@id", conexao);
        cmd.Parameters.AddWithValue("@unidade", DBNull.Value);
        cmd.Parameters.AddWithValue("@id", id);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Ler(reader) : null;
    }

    public async Task<List<ClubeTurmaOpcao>> ClubesGerenciaveisAsync(
        long usuarioId, bool admin, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        using var cmd = new MySqlCommand("""
            SELECT u.id, u.nome, u.fuso_horario
            FROM unidade u
            WHERE u.status='Ativa' AND (
                @admin=TRUE OR EXISTS (
                    SELECT 1 FROM papel_usuario pu
                    WHERE pu.usuario_id=@usuario AND pu.unidade_id=u.id
                      AND pu.papel='FuncionarioUnidade'
                )
            ) ORDER BY u.nome
            """, conexao);
        cmd.Parameters.AddWithValue("@admin", admin);
        cmd.Parameters.AddWithValue("@usuario", usuarioId);
        var itens = new List<ClubeTurmaOpcao>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            itens.Add(new(reader.GetInt64(0), reader.GetString(1), reader.GetString(2)));
        return itens;
    }

    public async Task<(ResultadoTurma Resultado, long Id)> CriarAsync(
        TurmaEntrada entrada, long usuarioId, bool admin, CancellationToken ct)
    {
        if (!PeriodoLocalValido(entrada)) return (ResultadoTurma.PeriodoInvalido, 0);
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);
        var (resultado, fuso) = await ValidarUnidadeAsync(conexao, transacao,
            entrada.UnidadeId, usuarioId, admin, ct);
        if (resultado != ResultadoTurma.Concluido) return (resultado, 0);
        if (!ConverterPeriodo(entrada, fuso!, out var inicioUtc, out var fimUtc))
            return (ResultadoTurma.FusoInvalido, 0);
        resultado = await ValidarReferenciasAsync(conexao, transacao, entrada, ct);
        if (resultado != ResultadoTurma.Concluido) return (resultado, 0);

        using var cmd = new MySqlCommand("""
            INSERT INTO turma (atividade_id, unidade_id, instrutor_pessoa_id,
                               inicio_utc, fim_utc, capacidade, status)
            VALUES (@atividade, @unidade, @instrutor, @inicio, @fim,
                    @capacidade, 'Aberta')
            """, conexao, transacao);
        Preencher(cmd, entrada, inicioUtc, fimUtc);
        await cmd.ExecuteNonQueryAsync(ct);
        var id = cmd.LastInsertedId;
        await LogAuditoria.RegistrarAsync(conexao, transacao, usuarioId,
            entrada.UnidadeId, "Criar", "Turma", id, "Turma cadastrada.", ct);
        await transacao.CommitAsync(ct);
        return (ResultadoTurma.Concluido, id);
    }

    public async Task<ResultadoTurma> AtualizarAsync(long id,
        TurmaEntrada entrada, long usuarioId, bool admin, CancellationToken ct)
    {
        if (!PeriodoLocalValido(entrada)) return ResultadoTurma.PeriodoInvalido;
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);
        var (resultado, unidadeAtual) = await ObterUnidadeTurmaAsync(
            conexao, transacao, id, usuarioId, admin, ct);
        if (resultado != ResultadoTurma.Concluido) return resultado;
        if (entrada.UnidadeId != unidadeAtual) return ResultadoTurma.UnidadeImutavel;
        var (validade, fuso) = await ValidarUnidadeAsync(conexao, transacao,
            unidadeAtual, usuarioId, admin, ct);
        if (validade != ResultadoTurma.Concluido) return validade;
        if (!ConverterPeriodo(entrada, fuso!, out var inicioUtc, out var fimUtc))
            return ResultadoTurma.FusoInvalido;
        resultado = await ValidarReferenciasAsync(conexao, transacao, entrada, ct);
        if (resultado != ResultadoTurma.Concluido) return resultado;

        // Uma turma matriculada não pode ser deslocada de horário/atividade:
        // isso poderia criar conflitos retroativos com outras inscrições.
        // A capacidade pode aumentar, mas nunca ficar abaixo dos confirmados.
        using (var verificar = new MySqlCommand("""
            SELECT t.atividade_id, t.inicio_utc, t.fim_utc,
                (SELECT COUNT(*) FROM inscricao_turma i
                 WHERE i.turma_id=t.id AND i.status IN ('Confirmada','EmEspera')),
                (SELECT COUNT(*) FROM inscricao_turma i
                 WHERE i.turma_id=t.id AND i.status='Confirmada')
            FROM turma t WHERE t.id=@id
            """, conexao, transacao))
        {
            verificar.Parameters.AddWithValue("@id", id);
            await using var reader = await verificar.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return ResultadoTurma.NaoEncontrada;
            var inscricoesAtivas = Convert.ToInt32(reader.GetValue(3));
            var confirmadas = Convert.ToInt32(reader.GetValue(4));
            if (inscricoesAtivas > 0 &&
                (reader.GetInt64(0) != entrada.AtividadeId ||
                 reader.GetDateTime(1) != inicioUtc ||
                 reader.GetDateTime(2) != fimUtc))
                return ResultadoTurma.PeriodoBloqueadoPorInscricoes;
            if (entrada.Capacidade < confirmadas)
                return ResultadoTurma.CapacidadeInferiorInscritos;
        }

        using var cmd = new MySqlCommand("""
            UPDATE turma SET atividade_id=@atividade,
                instrutor_pessoa_id=@instrutor, inicio_utc=@inicio,
                fim_utc=@fim, capacidade=@capacidade
            WHERE id=@id
            """, conexao, transacao);
        cmd.Parameters.AddWithValue("@id", id);
        Preencher(cmd, entrada, inicioUtc, fimUtc);
        await cmd.ExecuteNonQueryAsync(ct);
        await LogAuditoria.RegistrarAsync(conexao, transacao, usuarioId,
            unidadeAtual, "Atualizar", "Turma", id, "Turma alterada.", ct);
        await transacao.CommitAsync(ct);
        return ResultadoTurma.Concluido;
    }

    public async Task<ResultadoTurma> AlterarSituacaoAsync(long id,
        string status, long usuarioId, bool admin, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);
        var (resultado, unidadeId) = await ObterUnidadeTurmaAsync(
            conexao, transacao, id, usuarioId, admin, ct);
        if (resultado != ResultadoTurma.Concluido) return resultado;
        var (validade, _) = await ValidarUnidadeAsync(conexao, transacao,
            unidadeId, usuarioId, admin, ct);
        if (validade != ResultadoTurma.Concluido) return validade;
        using var cmd = new MySqlCommand("""
            UPDATE turma SET status=@status WHERE id=@id
            """, conexao, transacao);
        cmd.Parameters.AddWithValue("@status", status);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync(ct);
        if (status == "Cancelada")
        {
            // Cancelar a turma não pode deixar inscrições ativas reservando
            // vagas ou produzindo conflitos de horário em outras turmas.
            using var cancelar = new MySqlCommand("""
                UPDATE inscricao_turma
                SET status='Cancelada', cancelada_em_utc=UTC_TIMESTAMP(6)
                WHERE turma_id=@turma AND status IN ('Confirmada','EmEspera')
                """, conexao, transacao);
            cancelar.Parameters.AddWithValue("@turma", id);
            await cancelar.ExecuteNonQueryAsync(ct);
        }
        await LogAuditoria.RegistrarAsync(conexao, transacao, usuarioId,
            unidadeId, "AlterarSituacao", "Turma", id,
            "Situação da turma alterada para " + status + ".", ct);
        await transacao.CommitAsync(ct);
        return ResultadoTurma.Concluido;
    }

    private static async Task<(ResultadoTurma, string?)> ValidarUnidadeAsync(
        MySqlConnection conexao, MySqlTransaction transacao, long unidadeId,
        long usuarioId, bool admin, CancellationToken ct)
    {
        using var cmd = new MySqlCommand("""
            SELECT u.status, u.fuso_horario,
                (@admin=TRUE OR EXISTS (
                    SELECT 1 FROM papel_usuario pu
                    WHERE pu.usuario_id=@usuario AND pu.unidade_id=u.id
                      AND pu.papel='FuncionarioUnidade'
                )) AS permitido
            FROM unidade u WHERE u.id=@unidade FOR UPDATE
            """, conexao, transacao);
        cmd.Parameters.AddWithValue("@unidade", unidadeId);
        cmd.Parameters.AddWithValue("@usuario", usuarioId);
        cmd.Parameters.AddWithValue("@admin", admin);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return (ResultadoTurma.NaoEncontrada, null);
        if (!reader.GetBoolean(2)) return (ResultadoTurma.ForaDoEscopo, null);
        if (reader.GetString(0) != "Ativa")
            return (ResultadoTurma.UnidadeInativa, null);
        return (ResultadoTurma.Concluido, reader.GetString(1));
    }

    private static async Task<(ResultadoTurma, long)> ObterUnidadeTurmaAsync(
        MySqlConnection conexao, MySqlTransaction transacao, long id,
        long usuarioId, bool admin, CancellationToken ct)
    {
        using var cmd = new MySqlCommand("""
            SELECT t.unidade_id,
                (@admin=TRUE OR EXISTS (
                    SELECT 1 FROM papel_usuario pu
                    WHERE pu.usuario_id=@usuario AND pu.unidade_id=t.unidade_id
                      AND pu.papel='FuncionarioUnidade'
                )) AS permitido
            FROM turma t WHERE t.id=@id FOR UPDATE
            """, conexao, transacao);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@usuario", usuarioId);
        cmd.Parameters.AddWithValue("@admin", admin);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return (ResultadoTurma.NaoEncontrada, 0);
        var unidadeId = reader.GetInt64(0);
        return reader.GetBoolean(1) ? (ResultadoTurma.Concluido, unidadeId)
            : (ResultadoTurma.ForaDoEscopo, unidadeId);
    }

    private static async Task<ResultadoTurma> ValidarReferenciasAsync(
        MySqlConnection conexao, MySqlTransaction transacao, TurmaEntrada entrada,
        CancellationToken ct)
    {
        using (var cmd = new MySqlCommand(
            "SELECT id FROM atividade WHERE id=@id", conexao, transacao))
        {
            cmd.Parameters.AddWithValue("@id", entrada.AtividadeId);
            if (await cmd.ExecuteScalarAsync(ct) is null)
                return ResultadoTurma.AtividadeNaoEncontrada;
        }
        if (entrada.InstrutorPessoaId.HasValue)
        {
            using var cmd = new MySqlCommand("""
                SELECT id FROM pessoa WHERE id=@id AND status='Ativa'
                """, conexao, transacao);
            cmd.Parameters.AddWithValue("@id", entrada.InstrutorPessoaId.Value);
            if (await cmd.ExecuteScalarAsync(ct) is null)
                return ResultadoTurma.InstrutorInvalido;
        }
        return ResultadoTurma.Concluido;
    }

    private static bool PeriodoLocalValido(TurmaEntrada entrada) =>
        entrada.InicioLocal is { Kind: DateTimeKind.Unspecified } &&
        entrada.FimLocal is { Kind: DateTimeKind.Unspecified } &&
        entrada.FimLocal > entrada.InicioLocal;

    private static bool ConverterPeriodo(TurmaEntrada entrada, string fuso,
        out DateTime inicioUtc, out DateTime fimUtc)
    {
        inicioUtc = default;
        fimUtc = default;
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(fuso, out var zona))
            return false;
        var inicio = entrada.InicioLocal!.Value;
        var fim = entrada.FimLocal!.Value;
        if (zona.IsInvalidTime(inicio) || zona.IsInvalidTime(fim) ||
            zona.IsAmbiguousTime(inicio) || zona.IsAmbiguousTime(fim)) return false;
        try
        {
            inicioUtc = DateTime.SpecifyKind(
                TimeZoneInfo.ConvertTimeToUtc(inicio, zona), DateTimeKind.Unspecified);
            fimUtc = DateTime.SpecifyKind(
                TimeZoneInfo.ConvertTimeToUtc(fim, zona), DateTimeKind.Unspecified);
            return fimUtc > inicioUtc;
        }
        catch (ArgumentException) { return false; }
    }

    private static void Preencher(MySqlCommand cmd, TurmaEntrada entrada,
        DateTime inicioUtc, DateTime fimUtc)
    {
        cmd.Parameters.AddWithValue("@atividade", entrada.AtividadeId);
        cmd.Parameters.AddWithValue("@unidade", entrada.UnidadeId);
        cmd.Parameters.AddWithValue("@instrutor", entrada.InstrutorPessoaId.HasValue
            ? entrada.InstrutorPessoaId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@inicio", inicioUtc);
        cmd.Parameters.AddWithValue("@fim", fimUtc);
        cmd.Parameters.AddWithValue("@capacidade", entrada.Capacidade);
    }

    private static TurmaItem Ler(MySqlDataReader reader)
    {
        var fuso = reader.GetString(6);
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(fuso, out var zona))
            throw new InvalidOperationException("Fuso horário de clube inválido.");
        static DateTime Local(DateTime utc, TimeZoneInfo zona) =>
            DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(utc, DateTimeKind.Utc), zona),
                DateTimeKind.Unspecified);
        return new TurmaItem(reader.GetInt64(0), reader.GetInt64(1),
            reader.GetString(2), reader.GetString(3), reader.GetInt64(4),
            reader.GetString(5), fuso,
            reader.IsDBNull(7) ? null : reader.GetInt64(7),
            reader.IsDBNull(8) ? null : reader.GetString(8),
            Local(reader.GetDateTime(9), zona), Local(reader.GetDateTime(10), zona),
            reader.GetInt32(11), reader.GetString(12));
    }
}
