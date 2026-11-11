using ClickClub.Api.Contratos;
using MySqlConnector;

namespace ClickClub.Api.Dados;

public sealed class EventoSocialRepositorio(IConfiguration configuration)
{
    private readonly string _connectionString = configuration
        .GetConnectionString("ClickClub")
        ?? throw new InvalidOperationException("Conexão MySQL não configurada.");

    private const string Selecao = """
        SELECT e.id, e.unidade_id, u.nome, u.fuso_horario,
               e.nome, e.descricao, e.inicio_utc, e.fim_utc,
               e.publico_alvo, e.tipo, e.capacidade, e.status,
               (SELECT COUNT(*) FROM inscricao_evento ie
                WHERE ie.evento_id=e.id AND ie.status='Confirmada')
               + (SELECT COUNT(*) FROM convidado_evento ce
                  WHERE ce.evento_id=e.id AND ce.situacao='Autorizado') inscritos
        FROM evento_social e JOIN unidade u ON u.id=e.unidade_id
        WHERE 1=1
        """;

    public async Task<List<EventoSocialItem>> PublicadosAsync(
        bool associado, long? unidadeId, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        using var cmd = new MySqlCommand(Selecao + """
            AND e.status='Publicado' AND u.status='Ativa'
            AND e.fim_utc>UTC_TIMESTAMP(6)
            AND (e.publico_alvo='PublicoGeral' OR @associado=TRUE)
            AND (@unidade IS NULL OR e.unidade_id=@unidade)
            ORDER BY e.inicio_utc, e.id
            """, conexao);
        cmd.Parameters.AddWithValue("@associado", associado);
        cmd.Parameters.AddWithValue("@unidade", unidadeId.HasValue
            ? unidadeId.Value : DBNull.Value);
        return await LerListaAsync(cmd, ct);
    }

    public async Task<List<EventoSocialItem>> GerenciaveisAsync(
        long usuarioId, bool admin, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        using var cmd = new MySqlCommand(Selecao + """
            AND (@admin=TRUE OR EXISTS (
                SELECT 1 FROM papel_usuario pu
                WHERE pu.usuario_id=@usuario AND pu.unidade_id=e.unidade_id
                  AND pu.papel='FuncionarioUnidade'))
            ORDER BY e.inicio_utc DESC, e.id DESC
            """, conexao);
        cmd.Parameters.AddWithValue("@admin", admin);
        cmd.Parameters.AddWithValue("@usuario", usuarioId);
        return await LerListaAsync(cmd, ct);
    }

    public async Task<EventoSocialItem?> ConsultarAsync(
        long id, long usuarioId, bool admin, bool associado,
        CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        using var cmd = new MySqlCommand(Selecao + """
            AND e.id=@id AND (
                (e.status='Publicado' AND u.status='Ativa'
                 AND e.fim_utc>UTC_TIMESTAMP(6)
                 AND (e.publico_alvo='PublicoGeral' OR @associado=TRUE))
                OR @admin=TRUE
                OR EXISTS (SELECT 1 FROM papel_usuario pu
                    WHERE pu.usuario_id=@usuario AND pu.unidade_id=e.unidade_id
                      AND pu.papel='FuncionarioUnidade'))
            """, conexao);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@usuario", usuarioId);
        cmd.Parameters.AddWithValue("@admin", admin);
        cmd.Parameters.AddWithValue("@associado", associado);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Ler(reader) : null;
    }

    public async Task<(ResultadoEvento Resultado, long Id)> CriarAsync(
        EventoSocialEntrada entrada, long usuarioId, bool admin, CancellationToken ct)
    {
        if (!PeriodoLocalValido(entrada)) return (ResultadoEvento.PeriodoInvalido, 0);
        if (!EntradaValida(entrada)) return (ResultadoEvento.CapacidadeInvalida, 0);
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);
        var (resultado, fuso) = await UnidadeAsync(conexao, transacao,
            entrada.UnidadeId, usuarioId, admin, ct);
        if (resultado != ResultadoEvento.Concluido) return (resultado, 0);
        if (!Converter(entrada, fuso!, out var inicio, out var fim))
            return (ResultadoEvento.FusoInvalido, 0);
        using var cmd = new MySqlCommand("""
            INSERT INTO evento_social
                (unidade_id, nome, descricao, inicio_utc, fim_utc,
                 publico_alvo, tipo, capacidade, status)
            VALUES (@unidade, @nome, @descricao, @inicio, @fim,
                    @publico, @tipo, @capacidade, 'Publicado')
            """, conexao, transacao);
        Preencher(cmd, entrada, inicio, fim);
        await cmd.ExecuteNonQueryAsync(ct);
        var id = cmd.LastInsertedId;
        await LogAuditoria.RegistrarAsync(conexao, transacao, usuarioId,
            entrada.UnidadeId, "Criar", "EventoSocial", id,
            "Evento social publicado.", ct);
        await transacao.CommitAsync(ct);
        return (ResultadoEvento.Concluido, id);
    }

    public async Task<ResultadoEvento> AtualizarAsync(long id,
        EventoSocialEntrada entrada, long usuarioId, bool admin,
        CancellationToken ct)
    {
        if (!PeriodoLocalValido(entrada)) return ResultadoEvento.PeriodoInvalido;
        if (!EntradaValida(entrada)) return ResultadoEvento.CapacidadeInvalida;
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);
        var (resultado, unidade) = await UnidadeDoEventoAsync(conexao, transacao,
            id, usuarioId, admin, ct);
        if (resultado != ResultadoEvento.Concluido) return resultado;
        if (entrada.UnidadeId != unidade) return ResultadoEvento.UnidadeImutavel;
        var (validade, fuso) = await UnidadeAsync(conexao, transacao,
            unidade, usuarioId, admin, ct);
        if (validade != ResultadoEvento.Concluido) return validade;
        if (!Converter(entrada, fuso!, out var inicio, out var fim))
            return ResultadoEvento.FusoInvalido;

        using (var conferir = new MySqlCommand("""
            SELECT e.inicio_utc, e.fim_utc, e.publico_alvo, e.tipo,
                (SELECT COUNT(*) FROM inscricao_evento i
                 WHERE i.evento_id=e.id AND i.status='Confirmada')
                + (SELECT COUNT(*) FROM convidado_evento ce
                   WHERE ce.evento_id=e.id AND ce.situacao='Autorizado'),
                e.capacidade
            FROM evento_social e WHERE e.id=@id
            """, conexao, transacao))
        {
            conferir.Parameters.AddWithValue("@id", id);
            await using var reader = await conferir.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return ResultadoEvento.NaoEncontrado;
            var confirmadas = Convert.ToInt32(reader.GetValue(4));
            if (confirmadas > 0 &&
                (reader.GetDateTime(0) != inicio || reader.GetDateTime(1) != fim ||
                 reader.GetString(2) != entrada.PublicoAlvo ||
                 reader.GetString(3) != entrada.Tipo))
                return ResultadoEvento.DadosBloqueadosPorInscricoes;
            if (confirmadas > 0 && !entrada.Capacidade.HasValue &&
                !reader.IsDBNull(5))
                return ResultadoEvento.CapacidadeInferiorInscritos;
            if (entrada.Capacidade.HasValue && entrada.Capacidade.Value < confirmadas)
                return ResultadoEvento.CapacidadeInferiorInscritos;
        }

        using var cmd = new MySqlCommand("""
            UPDATE evento_social SET nome=@nome, descricao=@descricao,
                inicio_utc=@inicio, fim_utc=@fim, publico_alvo=@publico,
                tipo=@tipo, capacidade=@capacidade WHERE id=@id
            """, conexao, transacao);
        cmd.Parameters.AddWithValue("@id", id);
        Preencher(cmd, entrada, inicio, fim);
        await cmd.ExecuteNonQueryAsync(ct);
        await LogAuditoria.RegistrarAsync(conexao, transacao, usuarioId,
            unidade, "Atualizar", "EventoSocial", id, "Evento social alterado.", ct);
        await transacao.CommitAsync(ct);
        return ResultadoEvento.Concluido;
    }

    public async Task<ResultadoEvento> SituacaoAsync(long id, string status,
        long usuarioId, bool admin, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);
        var (resultado, unidade) = await UnidadeDoEventoAsync(conexao, transacao,
            id, usuarioId, admin, ct);
        if (resultado != ResultadoEvento.Concluido) return resultado;
        using var cmd = new MySqlCommand(
            "UPDATE evento_social SET status=@status WHERE id=@id",
            conexao, transacao);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@status", status);
        await cmd.ExecuteNonQueryAsync(ct);
        if (status == "Cancelado")
        {
            using var cancelar = new MySqlCommand("""
                UPDATE inscricao_evento SET status='Cancelada',
                    cancelada_em_utc=UTC_TIMESTAMP(6)
                WHERE evento_id=@evento AND status='Confirmada'
                """, conexao, transacao);
            cancelar.Parameters.AddWithValue("@evento", id);
            await cancelar.ExecuteNonQueryAsync(ct);
            using var revogar = new MySqlCommand("""
                UPDATE convidado_evento SET situacao='Revogado'
                WHERE evento_id=@evento AND situacao='Autorizado'
                """, conexao, transacao);
            revogar.Parameters.AddWithValue("@evento", id);
            await revogar.ExecuteNonQueryAsync(ct);
        }
        await LogAuditoria.RegistrarAsync(conexao, transacao, usuarioId,
            unidade, "AlterarSituacao", "EventoSocial", id,
            "Situação de evento alterada para " + status + ".", ct);
        await transacao.CommitAsync(ct);
        return ResultadoEvento.Concluido;
    }

    private static async Task<List<EventoSocialItem>> LerListaAsync(
        MySqlCommand cmd, CancellationToken ct)
    {
        var lista = new List<EventoSocialItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) lista.Add(Ler(reader));
        return lista;
    }

    private static EventoSocialItem Ler(MySqlDataReader reader)
    {
        var fuso = reader.GetString(3);
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(fuso, out var zona))
            throw new InvalidOperationException("Fuso horário de clube inválido.");
        static DateTime Local(DateTime utc, TimeZoneInfo zona) =>
            DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(utc, DateTimeKind.Utc), zona),
                DateTimeKind.Unspecified);
        var capacidade = reader.IsDBNull(10) ? (int?)null : reader.GetInt32(10);
        var inscritas = Convert.ToInt32(reader.GetValue(12));
        return new(reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2),
            fuso, reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            Local(reader.GetDateTime(6), zona), Local(reader.GetDateTime(7), zona),
            reader.GetString(8), reader.GetString(9), capacidade, reader.GetString(11),
            inscritas, capacidade.HasValue ? Math.Max(0, capacidade.Value - inscritas) : null);
    }

    private static bool EntradaValida(EventoSocialEntrada e) =>
        e.Nome.Trim().Length >= 2 &&
        (e.Tipo != "Torneio" || e.Capacidade.HasValue);

    private static bool PeriodoLocalValido(EventoSocialEntrada e) =>
        e.InicioLocal is { Kind: DateTimeKind.Unspecified } &&
        e.FimLocal is { Kind: DateTimeKind.Unspecified } &&
        e.FimLocal > e.InicioLocal;

    private static bool Converter(EventoSocialEntrada e, string fuso,
        out DateTime inicio, out DateTime fim)
    {
        inicio = fim = default;
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(fuso, out var zona))
            return false;
        var a = e.InicioLocal!.Value;
        var b = e.FimLocal!.Value;
        if (zona.IsInvalidTime(a) || zona.IsInvalidTime(b) ||
            zona.IsAmbiguousTime(a) || zona.IsAmbiguousTime(b)) return false;
        try
        {
            inicio = DateTime.SpecifyKind(
                TimeZoneInfo.ConvertTimeToUtc(a, zona), DateTimeKind.Unspecified);
            fim = DateTime.SpecifyKind(
                TimeZoneInfo.ConvertTimeToUtc(b, zona), DateTimeKind.Unspecified);
            return fim > inicio;
        }
        catch (ArgumentException) { return false; }
    }

    private static void Preencher(MySqlCommand cmd, EventoSocialEntrada e,
        DateTime inicio, DateTime fim)
    {
        cmd.Parameters.AddWithValue("@unidade", e.UnidadeId);
        cmd.Parameters.AddWithValue("@nome", e.Nome.Trim());
        cmd.Parameters.AddWithValue("@descricao", string.IsNullOrWhiteSpace(e.Descricao)
            ? DBNull.Value : e.Descricao.Trim());
        cmd.Parameters.AddWithValue("@inicio", inicio);
        cmd.Parameters.AddWithValue("@fim", fim);
        cmd.Parameters.AddWithValue("@publico", e.PublicoAlvo);
        cmd.Parameters.AddWithValue("@tipo", e.Tipo);
        cmd.Parameters.AddWithValue("@capacidade", e.Capacidade.HasValue
            ? e.Capacidade.Value : DBNull.Value);
    }

    private static async Task<(ResultadoEvento, string?)> UnidadeAsync(
        MySqlConnection c, MySqlTransaction t, long unidadeId,
        long usuarioId, bool admin, CancellationToken ct)
    {
        using var cmd = new MySqlCommand("""
            SELECT u.status, u.fuso_horario,
                (@admin=TRUE OR EXISTS (SELECT 1 FROM papel_usuario pu
                  WHERE pu.usuario_id=@usuario AND pu.unidade_id=u.id
                    AND pu.papel='FuncionarioUnidade'))
            FROM unidade u WHERE u.id=@unidade FOR UPDATE
            """, c, t);
        cmd.Parameters.AddWithValue("@unidade", unidadeId);
        cmd.Parameters.AddWithValue("@usuario", usuarioId);
        cmd.Parameters.AddWithValue("@admin", admin);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return (ResultadoEvento.NaoEncontrado, null);
        if (!reader.GetBoolean(2)) return (ResultadoEvento.ForaDoEscopo, null);
        if (reader.GetString(0) != "Ativa")
            return (ResultadoEvento.UnidadeInativa, null);
        return (ResultadoEvento.Concluido, reader.GetString(1));
    }

    private static async Task<(ResultadoEvento, long)> UnidadeDoEventoAsync(
        MySqlConnection c, MySqlTransaction t, long eventoId,
        long usuarioId, bool admin, CancellationToken ct)
    {
        using var cmd = new MySqlCommand("""
            SELECT e.unidade_id, (@admin=TRUE OR EXISTS (
                SELECT 1 FROM papel_usuario pu
                WHERE pu.usuario_id=@usuario AND pu.unidade_id=e.unidade_id
                  AND pu.papel='FuncionarioUnidade'))
            FROM evento_social e WHERE e.id=@evento FOR UPDATE
            """, c, t);
        cmd.Parameters.AddWithValue("@evento", eventoId);
        cmd.Parameters.AddWithValue("@usuario", usuarioId);
        cmd.Parameters.AddWithValue("@admin", admin);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return (ResultadoEvento.NaoEncontrado, 0);
        var unidadeId = reader.GetInt64(0);
        return reader.GetBoolean(1) ? (ResultadoEvento.Concluido, unidadeId)
            : (ResultadoEvento.ForaDoEscopo, unidadeId);
    }
}
