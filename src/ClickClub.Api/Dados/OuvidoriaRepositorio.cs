using ClickClub.Api.Contratos;
using MySqlConnector;

namespace ClickClub.Api.Dados;

public sealed class OuvidoriaRepositorio(IConfiguration configuration)
{
    private readonly string _connectionString = configuration
        .GetConnectionString("ClickClub")
        ?? throw new InvalidOperationException("Conexão MySQL não configurada.");

    private const string Consulta = """
        SELECT c.id, c.pessoa_id, p.nome, c.unidade_id, un.nome,
               c.tipo, c.categoria, c.assunto, c.descricao, c.status,
               c.criado_em_utc, c.atualizado_em_utc, c.encerrado_em_utc
        FROM chamado_ouvidoria c
        JOIN pessoa p ON p.id=c.pessoa_id
        LEFT JOIN unidade un ON un.id=c.unidade_id
        WHERE 1=1
        """;

    public async Task<List<ChamadoOuvidoriaItem>> MinhasAsync(
        long usuarioId, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        using var cmd = new MySqlCommand(Consulta + """
            AND EXISTS (SELECT 1 FROM usuario us
                WHERE us.id=@usuario AND us.ativo=TRUE
                  AND us.pessoa_id=c.pessoa_id)
            ORDER BY c.criado_em_utc DESC, c.id DESC
            """, conexao);
        cmd.Parameters.AddWithValue("@usuario", usuarioId);
        return await ListarAsync(cmd, ct);
    }

    public async Task<List<ChamadoOuvidoriaItem>> GerenciaveisAsync(
        long usuarioId, bool admin, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        using var cmd = new MySqlCommand(Consulta + """
            AND (@admin=TRUE OR (c.unidade_id IS NOT NULL AND EXISTS (
                SELECT 1 FROM papel_usuario pu
                WHERE pu.usuario_id=@usuario AND pu.unidade_id=c.unidade_id
                  AND pu.papel IN ('PresidenteClube','FuncionarioUnidade'))))
            ORDER BY (c.status='Encerrado'), c.criado_em_utc DESC, c.id DESC
            """, conexao);
        cmd.Parameters.AddWithValue("@admin", admin);
        cmd.Parameters.AddWithValue("@usuario", usuarioId);
        return await ListarAsync(cmd, ct);
    }

    public async Task<ChamadoOuvidoriaDetalhe?> DetalheAsync(
        long id, long usuarioId, bool admin, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        using var cmd = new MySqlCommand(Consulta + """
            AND c.id=@id AND (
                @admin=TRUE OR EXISTS (SELECT 1 FROM usuario us
                    WHERE us.id=@usuario AND us.ativo=TRUE
                      AND us.pessoa_id=c.pessoa_id)
                OR (c.unidade_id IS NOT NULL AND EXISTS (
                    SELECT 1 FROM papel_usuario pu
                    WHERE pu.usuario_id=@usuario AND pu.unidade_id=c.unidade_id
                      AND pu.papel IN ('PresidenteClube','FuncionarioUnidade'))))
            """, conexao);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@usuario", usuarioId);
        cmd.Parameters.AddWithValue("@admin", admin);
        ChamadoOuvidoriaItem? chamado = null;
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
            if (await reader.ReadAsync(ct)) chamado = LerChamado(reader);
        if (chamado is null) return null;

        using var respostasCmd = new MySqlCommand("""
            SELECT r.id, p.nome, r.conteudo, r.data_hora_utc
            FROM resposta_chamado r
            JOIN usuario u ON u.id=r.autor_usuario_id
            JOIN pessoa p ON p.id=u.pessoa_id
            WHERE r.chamado_id=@id
            ORDER BY r.data_hora_utc, r.id
            """, conexao);
        respostasCmd.Parameters.AddWithValue("@id", id);
        var respostas = new List<RespostaOuvidoriaItem>();
        await using var readerRespostas = await respostasCmd.ExecuteReaderAsync(ct);
        while (await readerRespostas.ReadAsync(ct))
            respostas.Add(new(readerRespostas.GetInt64(0),
                readerRespostas.GetString(1), readerRespostas.GetString(2),
                DateTime.SpecifyKind(readerRespostas.GetDateTime(3), DateTimeKind.Utc)));
        return new(chamado, respostas);
    }

    public async Task<List<ClubeOuvidoriaOpcao>> ClubesAtivosAsync(
        CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        using var cmd = new MySqlCommand("""
            SELECT id, nome FROM unidade WHERE status='Ativa' ORDER BY nome, id
            """, conexao);
        var lista = new List<ClubeOuvidoriaOpcao>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            lista.Add(new(reader.GetInt64(0), reader.GetString(1)));
        return lista;
    }

    public async Task<(ResultadoOuvidoria Resultado, long Id)> CriarAsync(
        ChamadoOuvidoriaEntrada entrada, long usuarioId, CancellationToken ct)
    {
        if (!EntradaValida(entrada)) return (ResultadoOuvidoria.DadosInvalidos, 0);
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);

        long pessoaId;
        using (var pessoa = new MySqlCommand("""
            SELECT u.pessoa_id FROM usuario u JOIN pessoa p ON p.id=u.pessoa_id
            WHERE u.id=@usuario AND u.ativo=TRUE AND p.status='Ativa'
            """, conexao, transacao))
        {
            pessoa.Parameters.AddWithValue("@usuario", usuarioId);
            var valor = await pessoa.ExecuteScalarAsync(ct);
            if (valor is null) return (ResultadoOuvidoria.SemContaAtiva, 0);
            pessoaId = Convert.ToInt64(valor);
        }
        if (entrada.UnidadeId.HasValue)
        {
            using var conferir = new MySqlCommand("""
                SELECT status FROM unidade WHERE id=@unidade
                """, conexao, transacao);
            conferir.Parameters.AddWithValue("@unidade", entrada.UnidadeId.Value);
            if (await conferir.ExecuteScalarAsync(ct) is not string status ||
                status != "Ativa")
                return (ResultadoOuvidoria.UnidadeInativa, 0);
        }

        using var cmd = new MySqlCommand("""
            INSERT INTO chamado_ouvidoria
                (pessoa_id, unidade_id, tipo, categoria, assunto, descricao,
                 status, criado_em_utc, atualizado_em_utc)
            VALUES (@pessoa, @unidade, @tipo, @categoria, @assunto, @descricao,
                    'Aberto', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6))
            """, conexao, transacao);
        cmd.Parameters.AddWithValue("@pessoa", pessoaId);
        cmd.Parameters.AddWithValue("@unidade", entrada.UnidadeId.HasValue
            ? entrada.UnidadeId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@tipo", entrada.Tipo);
        cmd.Parameters.AddWithValue("@categoria", entrada.Categoria.Trim());
        cmd.Parameters.AddWithValue("@assunto", entrada.Assunto.Trim());
        cmd.Parameters.AddWithValue("@descricao", entrada.Descricao.Trim());
        await cmd.ExecuteNonQueryAsync(ct);
        var id = cmd.LastInsertedId;
        await LogAuditoria.RegistrarAsync(conexao, transacao, usuarioId,
            entrada.UnidadeId, "Abrir", "ChamadoOuvidoria", id,
            "Chamado de ouvidoria aberto pelo próprio usuário.", ct);
        await transacao.CommitAsync(ct);
        return (ResultadoOuvidoria.Concluido, id);
    }

    public async Task<ResultadoOuvidoria> ResponderAsync(long id,
        RespostaOuvidoriaEntrada entrada, long usuarioId,
        bool admin, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(entrada.Conteudo) ||
            entrada.Conteudo.Trim().Length is < 2 or > 4000)
            return ResultadoOuvidoria.DadosInvalidos;
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);
        var (resultado, unidade, status) = await BuscarGestaoAsync(
            conexao, transacao, id, usuarioId, admin, ct);
        if (resultado != ResultadoOuvidoria.Concluido) return resultado;
        if (status == "Encerrado") return ResultadoOuvidoria.JaEncerrado;

        using var cmd = new MySqlCommand("""
            INSERT INTO resposta_chamado
                (chamado_id, autor_usuario_id, conteudo, data_hora_utc)
            VALUES (@chamado, @autor, @conteudo, UTC_TIMESTAMP(6))
            """, conexao, transacao);
        cmd.Parameters.AddWithValue("@chamado", id);
        cmd.Parameters.AddWithValue("@autor", usuarioId);
        cmd.Parameters.AddWithValue("@conteudo", entrada.Conteudo.Trim());
        await cmd.ExecuteNonQueryAsync(ct);
        using var atualizar = new MySqlCommand("""
            UPDATE chamado_ouvidoria
            SET status='Respondido', atualizado_em_utc=UTC_TIMESTAMP(6)
            WHERE id=@id
            """, conexao, transacao);
        atualizar.Parameters.AddWithValue("@id", id);
        await atualizar.ExecuteNonQueryAsync(ct);
        await LogAuditoria.RegistrarAsync(conexao, transacao, usuarioId,
            unidade, "Responder", "ChamadoOuvidoria", id,
            "Resposta administrativa registrada no chamado.", ct);
        await transacao.CommitAsync(ct);
        return ResultadoOuvidoria.Concluido;
    }

    public async Task<ResultadoOuvidoria> EncerrarAsync(
        long id, long usuarioId, bool admin, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);
        var (resultado, unidade, status) = await BuscarGestaoAsync(
            conexao, transacao, id, usuarioId, admin, ct);
        if (resultado != ResultadoOuvidoria.Concluido) return resultado;
        if (status == "Encerrado") return ResultadoOuvidoria.JaEncerrado;

        using (var resposta = new MySqlCommand("""
            SELECT id FROM resposta_chamado WHERE chamado_id=@id LIMIT 1
            """, conexao, transacao))
        {
            resposta.Parameters.AddWithValue("@id", id);
            if (await resposta.ExecuteScalarAsync(ct) is null)
                return ResultadoOuvidoria.SemResposta;
        }
        using var cmd = new MySqlCommand("""
            UPDATE chamado_ouvidoria SET status='Encerrado',
                encerrado_em_utc=UTC_TIMESTAMP(6),
                atualizado_em_utc=UTC_TIMESTAMP(6)
            WHERE id=@id
            """, conexao, transacao);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync(ct);
        await LogAuditoria.RegistrarAsync(conexao, transacao, usuarioId,
            unidade, "Encerrar", "ChamadoOuvidoria", id,
            "Chamado encerrado após resposta administrativa.", ct);
        await transacao.CommitAsync(ct);
        return ResultadoOuvidoria.Concluido;
    }

    private static bool EntradaValida(ChamadoOuvidoriaEntrada entrada) =>
        entrada.Tipo is "Solicitacao" or "Reclamacao" &&
        !string.IsNullOrWhiteSpace(entrada.Categoria) &&
        entrada.Categoria.Trim().Length is >= 2 and <= 80 &&
        !string.IsNullOrWhiteSpace(entrada.Assunto) &&
        entrada.Assunto.Trim().Length is >= 2 and <= 150 &&
        !string.IsNullOrWhiteSpace(entrada.Descricao) &&
        entrada.Descricao.Trim().Length is >= 2 and <= 4000 &&
        (entrada.UnidadeId is null or > 0);

    private static async Task<(ResultadoOuvidoria Resultado,
        long? UnidadeId, string Status)> BuscarGestaoAsync(
        MySqlConnection conexao, MySqlTransaction transacao,
        long chamadoId, long usuarioId, bool admin, CancellationToken ct)
    {
        using var cmd = new MySqlCommand("""
            SELECT c.unidade_id, c.status,
                   (@admin=TRUE OR (c.unidade_id IS NOT NULL AND EXISTS (
                       SELECT 1 FROM papel_usuario pu
                       WHERE pu.usuario_id=@usuario AND pu.unidade_id=c.unidade_id
                         AND pu.papel IN ('PresidenteClube','FuncionarioUnidade'))))
            FROM chamado_ouvidoria c WHERE c.id=@id FOR UPDATE
            """, conexao, transacao);
        cmd.Parameters.AddWithValue("@id", chamadoId);
        cmd.Parameters.AddWithValue("@usuario", usuarioId);
        cmd.Parameters.AddWithValue("@admin", admin);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return (ResultadoOuvidoria.NaoEncontrado, null, string.Empty);
        if (!reader.GetBoolean(2))
            return (ResultadoOuvidoria.ForaDoEscopo, null, string.Empty);
        return (ResultadoOuvidoria.Concluido,
            reader.IsDBNull(0) ? null : reader.GetInt64(0), reader.GetString(1));
    }

    private static async Task<List<ChamadoOuvidoriaItem>> ListarAsync(
        MySqlCommand cmd, CancellationToken ct)
    {
        var lista = new List<ChamadoOuvidoriaItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) lista.Add(LerChamado(reader));
        return lista;
    }

    private static ChamadoOuvidoriaItem LerChamado(MySqlDataReader reader) => new(
        reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2),
        reader.IsDBNull(3) ? null : reader.GetInt64(3),
        reader.IsDBNull(4) ? null : reader.GetString(4),
        reader.GetString(5), reader.GetString(6), reader.GetString(7),
        reader.GetString(8), reader.GetString(9),
        DateTime.SpecifyKind(reader.GetDateTime(10), DateTimeKind.Utc),
        DateTime.SpecifyKind(reader.GetDateTime(11), DateTimeKind.Utc),
        reader.IsDBNull(12) ? null :
            DateTime.SpecifyKind(reader.GetDateTime(12), DateTimeKind.Utc));
}
