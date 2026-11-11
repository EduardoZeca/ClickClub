using System.Data;
using ClickClub.Api.Contratos;
using MySqlConnector;

namespace ClickClub.Api.Dados;

public sealed class InscricaoEventoRepositorio(IConfiguration configuration)
{
    private readonly string _connectionString = configuration
        .GetConnectionString("ClickClub")
        ?? throw new InvalidOperationException("Conexão MySQL não configurada.");

    public async Task<(ResultadoEvento Resultado, InscricaoEventoCriada? Item)>
        InscreverMinhaAsync(long eventoId, long usuarioId, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        var pessoaId = await PessoaAsync(conexao, usuarioId, ct);
        if (!pessoaId.HasValue) return (ResultadoEvento.SemAssociacaoAtiva, null);
        await using var transacao = await conexao.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, ct);
        await TravarPessoaAsync(conexao, transacao, pessoaId.Value, ct);
        var elegibilidade = await ElegibilidadeAsync(
            conexao, transacao, pessoaId.Value, ct);
        if (elegibilidade != ResultadoEvento.Concluido) return (elegibilidade, null);

        var evento = await BuscarEventoAsync(conexao, transacao, eventoId, ct);
        if (evento is null) return (ResultadoEvento.NaoEncontrado, null);
        if (evento.Value.Status != "Publicado" || evento.Value.UnidadeStatus != "Ativa" ||
            evento.Value.InicioUtc <= DateTime.UtcNow || !evento.Value.Capacidade.HasValue)
            return (ResultadoEvento.Indisponivel, null);

        using (var duplicidade = new MySqlCommand("""
            SELECT id FROM inscricao_evento
            WHERE evento_id=@evento AND pessoa_id=@pessoa
              AND status='Confirmada' LIMIT 1
            """, conexao, transacao))
        {
            duplicidade.Parameters.AddWithValue("@evento", eventoId);
            duplicidade.Parameters.AddWithValue("@pessoa", pessoaId.Value);
            if (await duplicidade.ExecuteScalarAsync(ct) is not null)
                return (ResultadoEvento.InscricaoDuplicada, null);
        }
        if (await ContarAsync(conexao, transacao, eventoId, ct) >=
            evento.Value.Capacidade.Value)
            return (ResultadoEvento.SemVagas, null);

        using var inserir = new MySqlCommand("""
            INSERT INTO inscricao_evento
                (evento_id, pessoa_id, status, inscrita_em_utc)
            VALUES (@evento, @pessoa, 'Confirmada', UTC_TIMESTAMP(6))
            """, conexao, transacao);
        inserir.Parameters.AddWithValue("@evento", eventoId);
        inserir.Parameters.AddWithValue("@pessoa", pessoaId.Value);
        await inserir.ExecuteNonQueryAsync(ct);
        var id = inserir.LastInsertedId;
        await LogAuditoria.RegistrarAsync(conexao, transacao, usuarioId,
            evento.Value.UnidadeId, "Inscrever", "InscricaoEvento", id,
            "Associado inscrito em evento com vaga disponível.", ct);
        await transacao.CommitAsync(ct);
        return (ResultadoEvento.Concluido,
            new InscricaoEventoCriada(id, eventoId, "Confirmada"));
    }

    public async Task<ResultadoEvento> CancelarMinhaAsync(long eventoId,
        long usuarioId, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        var pessoaId = await PessoaAsync(conexao, usuarioId, ct);
        if (!pessoaId.HasValue) return ResultadoEvento.InscricaoNaoEncontrada;
        await using var transacao = await conexao.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, ct);
        await TravarPessoaAsync(conexao, transacao, pessoaId.Value, ct);
        var evento = await BuscarEventoAsync(conexao, transacao, eventoId, ct);
        if (evento is null) return ResultadoEvento.NaoEncontrado;
        long inscricaoId;
        string status;
        using (var cmd = new MySqlCommand("""
            SELECT id, status FROM inscricao_evento
            WHERE evento_id=@evento AND pessoa_id=@pessoa
            ORDER BY (status='Confirmada') DESC, id DESC
            LIMIT 1 FOR UPDATE
            """, conexao, transacao))
        {
            cmd.Parameters.AddWithValue("@evento", eventoId);
            cmd.Parameters.AddWithValue("@pessoa", pessoaId.Value);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                return ResultadoEvento.InscricaoNaoEncontrada;
            inscricaoId = reader.GetInt64(0);
            status = reader.GetString(1);
        }
        if (status == "Cancelada") return ResultadoEvento.JaCancelada;
        using var cancelar = new MySqlCommand("""
            UPDATE inscricao_evento SET status='Cancelada',
                cancelada_em_utc=UTC_TIMESTAMP(6) WHERE id=@id
            """, conexao, transacao);
        cancelar.Parameters.AddWithValue("@id", inscricaoId);
        await cancelar.ExecuteNonQueryAsync(ct);
        await LogAuditoria.RegistrarAsync(conexao, transacao, usuarioId,
            evento.Value.UnidadeId, "Cancelar", "InscricaoEvento", inscricaoId,
            "Inscrição de evento cancelada pelo associado.", ct);
        await transacao.CommitAsync(ct);
        return ResultadoEvento.Concluido;
    }

    public async Task<List<MinhaInscricaoEventoItem>> MinhasAsync(
        long usuarioId, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        using var cmd = new MySqlCommand("""
            SELECT i.id, i.evento_id, e.nome, u.nome,
                   e.inicio_utc, u.fuso_horario, i.status
            FROM inscricao_evento i
            JOIN evento_social e ON e.id=i.evento_id
            JOIN unidade u ON u.id=e.unidade_id
            JOIN usuario us ON us.pessoa_id=i.pessoa_id
            WHERE us.id=@usuario
            ORDER BY i.inscrita_em_utc DESC, i.id DESC
            """, conexao);
        cmd.Parameters.AddWithValue("@usuario", usuarioId);
        var itens = new List<MinhaInscricaoEventoItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var fuso = reader.GetString(5);
            if (!TimeZoneInfo.TryFindSystemTimeZoneById(fuso, out var zona))
                throw new InvalidOperationException("Fuso horário inválido.");
            var inicio = DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(reader.GetDateTime(4), DateTimeKind.Utc), zona),
                DateTimeKind.Unspecified);
            itens.Add(new(reader.GetInt64(0), reader.GetInt64(1),
                reader.GetString(2), reader.GetString(3), inicio, reader.GetString(6)));
        }
        return itens;
    }

    public async Task<(ResultadoEvento Resultado, List<InscricaoEventoItem> Itens)>
        GestaoAsync(long eventoId, long usuarioId, bool admin, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        using (var escopo = new MySqlCommand("""
            SELECT (@admin=TRUE OR EXISTS (SELECT 1 FROM papel_usuario pu
                WHERE pu.usuario_id=@usuario AND pu.unidade_id=e.unidade_id
                  AND pu.papel='FuncionarioUnidade'))
            FROM evento_social e WHERE e.id=@evento
            """, conexao))
        {
            escopo.Parameters.AddWithValue("@evento", eventoId);
            escopo.Parameters.AddWithValue("@usuario", usuarioId);
            escopo.Parameters.AddWithValue("@admin", admin);
            var autorizado = await escopo.ExecuteScalarAsync(ct);
            if (autorizado is null) return (ResultadoEvento.NaoEncontrado, []);
            if (!Convert.ToBoolean(autorizado)) return (ResultadoEvento.ForaDoEscopo, []);
        }
        using var cmd = new MySqlCommand("""
            SELECT i.id, i.evento_id, i.pessoa_id, p.nome,
                   i.status, i.inscrita_em_utc
            FROM inscricao_evento i JOIN pessoa p ON p.id=i.pessoa_id
            WHERE i.evento_id=@evento
            ORDER BY i.inscrita_em_utc, i.id
            """, conexao);
        cmd.Parameters.AddWithValue("@evento", eventoId);
        var itens = new List<InscricaoEventoItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            itens.Add(new(reader.GetInt64(0), reader.GetInt64(1),
                reader.GetInt64(2), reader.GetString(3), reader.GetString(4),
                DateTime.SpecifyKind(reader.GetDateTime(5), DateTimeKind.Utc)));
        return (ResultadoEvento.Concluido, itens);
    }

    private static async Task<long?> PessoaAsync(
        MySqlConnection c, long usuarioId, CancellationToken ct)
    {
        using var cmd = new MySqlCommand("""
            SELECT pessoa_id FROM usuario WHERE id=@usuario AND ativo=TRUE
            """, c);
        cmd.Parameters.AddWithValue("@usuario", usuarioId);
        var valor = await cmd.ExecuteScalarAsync(ct);
        return valor is null ? null : Convert.ToInt64(valor);
    }

    private static async Task TravarPessoaAsync(MySqlConnection c,
        MySqlTransaction t, long pessoaId, CancellationToken ct)
    {
        using var cmd = new MySqlCommand(
            "SELECT id FROM pessoa WHERE id=@id FOR UPDATE", c, t);
        cmd.Parameters.AddWithValue("@id", pessoaId);
        await cmd.ExecuteScalarAsync(ct);
    }

    private static async Task<ResultadoEvento> ElegibilidadeAsync(
        MySqlConnection c, MySqlTransaction t, long pessoaId, CancellationToken ct)
    {
        long associacao;
        string fuso;
        using (var cmd = new MySqlCommand("""
            SELECT a.id, origem.fuso_horario
            FROM associacao a JOIN unidade origem ON origem.id=a.unidade_origem_id
            JOIN pessoa p ON p.id=@pessoa AND p.status='Ativa'
            JOIN usuario us ON us.pessoa_id=p.id AND us.ativo=TRUE
            WHERE a.status='Ativa' AND
              (a.titular_pessoa_id=p.id OR EXISTS (
                SELECT 1 FROM dependente_associacao d
                WHERE d.associacao_id=a.id
                  AND d.dependente_pessoa_id=p.id AND d.ativo=TRUE))
              AND EXISTS(SELECT 1 FROM papel_usuario pu
                WHERE pu.usuario_id=us.id AND pu.papel IN ('Titular','Dependente'))
            ORDER BY a.id LIMIT 1
            """, c, t))
        {
            cmd.Parameters.AddWithValue("@pessoa", pessoaId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return ResultadoEvento.SemAssociacaoAtiva;
            associacao = reader.GetInt64(0);
            fuso = reader.GetString(1);
        }
        DateTime hoje;
        try { hoje = RegraInadimplencia.DataLocal(DateTime.UtcNow, fuso); }
        catch (InvalidOperationException) { return ResultadoEvento.FusoInvalido; }
        using var consultar = new MySqlCommand("""
            SELECT EXISTS(SELECT 1 FROM fatura f
                WHERE f.associacao_id=@associacao
                  AND f.tipo='Mensalidade' AND f.status='EmAberto'
                  AND f.valor_total>0 AND f.data_vencimento<@limite)
            """, c, t);
        consultar.Parameters.AddWithValue("@associacao", associacao);
        consultar.Parameters.AddWithValue("@limite",
            hoje.AddDays(-RegraInadimplencia.DiasTolerancia));
        return Convert.ToInt32(await consultar.ExecuteScalarAsync(ct)) == 1
            ? ResultadoEvento.MensalidadeInadimplente : ResultadoEvento.Concluido;
    }

    private static async Task<(long UnidadeId, int? Capacidade,
        string Status, string UnidadeStatus, DateTime InicioUtc)?> BuscarEventoAsync(
        MySqlConnection c, MySqlTransaction t, long eventoId, CancellationToken ct)
    {
        using var cmd = new MySqlCommand("""
            SELECT e.unidade_id, e.capacidade, e.status, u.status, e.inicio_utc
            FROM evento_social e JOIN unidade u ON u.id=e.unidade_id
            WHERE e.id=@evento FOR UPDATE
            """, c, t);
        cmd.Parameters.AddWithValue("@evento", eventoId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)
            ? (reader.GetInt64(0), reader.IsDBNull(1) ? null : reader.GetInt32(1),
                reader.GetString(2), reader.GetString(3), reader.GetDateTime(4))
            : null;
    }

    private static async Task<int> ContarAsync(MySqlConnection c,
        MySqlTransaction t, long eventoId, CancellationToken ct)
    {
        using var cmd = new MySqlCommand("""
            SELECT (
                (SELECT COUNT(*) FROM inscricao_evento
                 WHERE evento_id=@evento AND status='Confirmada')
                + (SELECT COUNT(*) FROM convidado_evento
                   WHERE evento_id=@evento AND situacao='Autorizado'))
            """, c, t);
        cmd.Parameters.AddWithValue("@evento", eventoId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }
}
