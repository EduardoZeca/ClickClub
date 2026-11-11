using ClickClub.Api.Contratos;
using MySqlConnector;

namespace ClickClub.Api.Dados;

public sealed class VisitanteEventoRepositorio(IConfiguration configuration)
{
    private readonly string _connectionString = configuration
        .GetConnectionString("ClickClub")
        ?? throw new InvalidOperationException("Conexão MySQL não configurada.");

    private sealed record EventoVisita(long UnidadeId, string Status,
        string PublicoAlvo, string UnidadeStatus, DateTime FimUtc,
        int? Capacidade, bool Escopo);

    public async Task<(ResultadoVisitanteEvento Resultado,
        List<VisitanteEventoItem> Itens)> ListarAsync(
        long eventoId, long usuarioId, bool admin, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        var evento = await BuscarEventoAsync(conexao, null, eventoId,
            usuarioId, admin, false, ct);
        if (evento is null) return (ResultadoVisitanteEvento.EventoNaoEncontrado, []);
        if (!evento.Escopo) return (ResultadoVisitanteEvento.ForaDoEscopo, []);
        using var cmd = new MySqlCommand("""
            SELECT id, evento_id, nome, identificacao_minima, situacao
            FROM convidado_evento WHERE evento_id=@evento ORDER BY nome, id
            """, conexao);
        cmd.Parameters.AddWithValue("@evento", eventoId);
        var itens = new List<VisitanteEventoItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            itens.Add(new(reader.GetInt64(0), reader.GetInt64(1),
                reader.GetString(2), reader.GetString(3), reader.GetString(4)));
        return (ResultadoVisitanteEvento.Concluido, itens);
    }

    public async Task<(ResultadoVisitanteEvento Resultado, long Id)> CriarAsync(
        long eventoId, VisitanteEventoEntrada entrada, long usuarioId,
        bool admin, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);
        var evento = await BuscarEventoAsync(conexao, transacao,
            eventoId, usuarioId, admin, true, ct);
        if (evento is null) return (ResultadoVisitanteEvento.EventoNaoEncontrado, 0);
        if (!evento.Escopo) return (ResultadoVisitanteEvento.ForaDoEscopo, 0);
        if (evento.PublicoAlvo != "PublicoGeral")
            return (ResultadoVisitanteEvento.PublicoNaoPermitido, 0);
        if (evento.Status != "Publicado" || evento.UnidadeStatus != "Ativa" ||
            evento.FimUtc <= DateTime.UtcNow)
            return (ResultadoVisitanteEvento.EventoIndisponivel, 0);
        if (!await HaVagaAsync(conexao, transacao, eventoId,
                evento.Capacidade, ct))
            return (ResultadoVisitanteEvento.CapacidadeEsgotada, 0);

        using var cmd = new MySqlCommand("""
            INSERT INTO convidado_evento
                (evento_id, nome, identificacao_minima, situacao, criado_em_utc)
            VALUES (@evento, @nome, @identificacao, 'Autorizado', UTC_TIMESTAMP(6))
            """, conexao, transacao);
        cmd.Parameters.AddWithValue("@evento", eventoId);
        cmd.Parameters.AddWithValue("@nome", entrada.Nome.Trim());
        cmd.Parameters.AddWithValue("@identificacao", entrada.IdentificacaoMinima.Trim());
        await cmd.ExecuteNonQueryAsync(ct);
        var id = cmd.LastInsertedId;
        await LogAuditoria.RegistrarAsync(conexao, transacao, usuarioId,
            evento.UnidadeId, "CadastrarVisitante", "ConvidadoEvento", id,
            "Visitante identificado e pré-autorizado para evento público.", ct);
        await transacao.CommitAsync(ct);
        return (ResultadoVisitanteEvento.Concluido, id);
    }

    public async Task<ResultadoVisitanteEvento> AlterarSituacaoAsync(
        long eventoId, long visitanteId, string situacao, long usuarioId,
        bool admin, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);
        var evento = await BuscarEventoAsync(conexao, transacao,
            eventoId, usuarioId, admin, true, ct);
        if (evento is null) return ResultadoVisitanteEvento.EventoNaoEncontrado;
        if (!evento.Escopo) return ResultadoVisitanteEvento.ForaDoEscopo;

        string estadoAtual;
        using (var consultar = new MySqlCommand("""
            SELECT situacao FROM convidado_evento
            WHERE id=@id AND evento_id=@evento FOR UPDATE
            """, conexao, transacao))
        {
            consultar.Parameters.AddWithValue("@id", visitanteId);
            consultar.Parameters.AddWithValue("@evento", eventoId);
            var retorno = await consultar.ExecuteScalarAsync(ct);
            if (retorno is not string estado)
                return ResultadoVisitanteEvento.VisitanteNaoEncontrado;
            estadoAtual = estado;
        }

        if (situacao == "Autorizado" && estadoAtual != "Autorizado")
        {
            if (evento.PublicoAlvo != "PublicoGeral")
                return ResultadoVisitanteEvento.PublicoNaoPermitido;
            if (evento.Status != "Publicado" || evento.UnidadeStatus != "Ativa" ||
                evento.FimUtc <= DateTime.UtcNow)
                return ResultadoVisitanteEvento.EventoIndisponivel;
            if (!await HaVagaAsync(conexao, transacao, eventoId,
                    evento.Capacidade, ct))
                return ResultadoVisitanteEvento.CapacidadeEsgotada;
        }

        using var cmd = new MySqlCommand("""
            UPDATE convidado_evento SET situacao=@situacao
            WHERE id=@id AND evento_id=@evento
            """, conexao, transacao);
        cmd.Parameters.AddWithValue("@situacao", situacao);
        cmd.Parameters.AddWithValue("@id", visitanteId);
        cmd.Parameters.AddWithValue("@evento", eventoId);
        await cmd.ExecuteNonQueryAsync(ct);
        await LogAuditoria.RegistrarAsync(conexao, transacao, usuarioId,
            evento.UnidadeId, "AlterarSituacao", "ConvidadoEvento", visitanteId,
            "Situação do visitante alterada para " + situacao + ".", ct);
        await transacao.CommitAsync(ct);
        return ResultadoVisitanteEvento.Concluido;
    }

    private static async Task<bool> HaVagaAsync(MySqlConnection conexao,
        MySqlTransaction transacao, long eventoId, int? capacidade,
        CancellationToken ct)
    {
        if (!capacidade.HasValue) return true;
        using var cmd = new MySqlCommand("""
            SELECT (SELECT COUNT(*) FROM inscricao_evento i
                    WHERE i.evento_id=@evento AND i.status='Confirmada')
                 + (SELECT COUNT(*) FROM convidado_evento v
                    WHERE v.evento_id=@evento AND v.situacao='Autorizado')
            """, conexao, transacao);
        cmd.Parameters.AddWithValue("@evento", eventoId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) < capacidade.Value;
    }

    private static async Task<EventoVisita?> BuscarEventoAsync(
        MySqlConnection conexao, MySqlTransaction? transacao, long eventoId,
        long usuarioId, bool admin, bool travar, CancellationToken ct)
    {
        using var cmd = new MySqlCommand("""
            SELECT e.unidade_id, e.status, e.publico_alvo,
                u.status, e.fim_utc, e.capacidade,
                (@admin=TRUE OR EXISTS (SELECT 1 FROM papel_usuario pu
                  WHERE pu.usuario_id=@usuario AND pu.unidade_id=e.unidade_id
                    AND pu.papel='FuncionarioUnidade'))
            FROM evento_social e JOIN unidade u ON u.id=e.unidade_id
            WHERE e.id=@evento
            """ + (travar ? " FOR UPDATE" : ""), conexao, transacao);
        cmd.Parameters.AddWithValue("@evento", eventoId);
        cmd.Parameters.AddWithValue("@usuario", usuarioId);
        cmd.Parameters.AddWithValue("@admin", admin);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)
            ? new EventoVisita(reader.GetInt64(0), reader.GetString(1),
                reader.GetString(2), reader.GetString(3), reader.GetDateTime(4),
                reader.IsDBNull(5) ? null : reader.GetInt32(5),
                reader.GetBoolean(6))
            : null;
    }
}
