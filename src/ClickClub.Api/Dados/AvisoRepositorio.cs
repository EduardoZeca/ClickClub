using ClickClub.Api.Contratos;
using MySqlConnector;

namespace ClickClub.Api.Dados;

public sealed class AvisoRepositorio(IConfiguration configuration)
{
    private readonly string _connectionString = configuration
        .GetConnectionString("ClickClub")
        ?? throw new InvalidOperationException("Conexão MySQL não configurada.");

    private const string BaseConsulta = """
        SELECT a.id, a.unidade_id, un.nome, a.titulo, a.corpo,
               a.inicio_utc, a.fim_utc, a.ativo, p.nome
        FROM aviso a
        LEFT JOIN unidade un ON un.id=a.unidade_id
        JOIN usuario us ON us.id=a.autor_usuario_id
        JOIN pessoa p ON p.id=us.pessoa_id
        WHERE 1=1
        """;

    public async Task<List<AvisoItem>> PublicadosAsync(
        long? unidadeId, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        using var cmd = new MySqlCommand(BaseConsulta + """
            AND a.ativo=TRUE
            AND a.inicio_utc<=UTC_TIMESTAMP(6)
            AND a.fim_utc>UTC_TIMESTAMP(6)
            AND (a.unidade_id IS NULL OR un.status='Ativa')
            AND (@unidade IS NULL OR a.unidade_id IS NULL OR a.unidade_id=@unidade)
            ORDER BY a.inicio_utc DESC, a.id DESC
            """, conexao);
        cmd.Parameters.AddWithValue("@unidade",
            unidadeId.HasValue ? unidadeId.Value : DBNull.Value);
        return await LerListaAsync(cmd, ct);
    }

    public async Task<List<AvisoItem>> GerenciaveisAsync(
        long usuarioId, bool admin, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        using var cmd = new MySqlCommand(BaseConsulta + """
            AND (@admin=TRUE OR (a.unidade_id IS NOT NULL AND EXISTS (
                SELECT 1 FROM papel_usuario pu
                WHERE pu.usuario_id=@usuario AND pu.unidade_id=a.unidade_id
                  AND pu.papel IN ('PresidenteClube','FuncionarioUnidade'))))
            ORDER BY a.inicio_utc DESC, a.id DESC
            """, conexao);
        cmd.Parameters.AddWithValue("@admin", admin);
        cmd.Parameters.AddWithValue("@usuario", usuarioId);
        return await LerListaAsync(cmd, ct);
    }

    public async Task<(ResultadoAviso Resultado, AvisoItem? Item)> BuscarAsync(
        long id, long usuarioId, bool admin, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        var escopo = await EscopoDoAvisoAsync(conexao, null, id,
            usuarioId, admin, false, ct);
        if (escopo == ResultadoAviso.NaoEncontrado) return (escopo, null);
        if (escopo != ResultadoAviso.Concluido) return (escopo, null);
        using var cmd = new MySqlCommand(BaseConsulta + " AND a.id=@id", conexao);
        cmd.Parameters.AddWithValue("@id", id);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)
            ? (ResultadoAviso.Concluido, Ler(reader))
            : (ResultadoAviso.NaoEncontrado, null);
    }

    public async Task<List<ClubeAvisoOpcao>> ClubesGerenciaveisAsync(
        long usuarioId, bool admin, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        using var cmd = new MySqlCommand("""
            SELECT u.id, u.nome FROM unidade u
            WHERE u.status='Ativa' AND (@admin=TRUE OR EXISTS (
                SELECT 1 FROM papel_usuario pu
                WHERE pu.usuario_id=@usuario AND pu.unidade_id=u.id
                  AND pu.papel IN ('PresidenteClube','FuncionarioUnidade')))
            ORDER BY u.nome, u.id
            """, conexao);
        cmd.Parameters.AddWithValue("@admin", admin);
        cmd.Parameters.AddWithValue("@usuario", usuarioId);
        var lista = new List<ClubeAvisoOpcao>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            lista.Add(new(reader.GetInt64(0), reader.GetString(1)));
        return lista;
    }

    public async Task<(ResultadoAviso Resultado, long Id)> CriarAsync(
        AvisoEntrada entrada, long usuarioId, bool admin, CancellationToken ct)
    {
        if (!EntradaValida(entrada)) return (ResultadoAviso.DadosInvalidos, 0);
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);
        var autorizado = await ValidarDestinoAsync(conexao, transacao,
            entrada.UnidadeId, usuarioId, admin, ct);
        if (autorizado != ResultadoAviso.Concluido) return (autorizado, 0);

        using var cmd = new MySqlCommand("""
            INSERT INTO aviso (unidade_id, titulo, corpo, inicio_utc, fim_utc,
                               ativo, autor_usuario_id, atualizado_em_utc)
            VALUES (@unidade, @titulo, @corpo, @inicio, @fim, TRUE,
                    @usuario, UTC_TIMESTAMP(6))
            """, conexao, transacao);
        Preencher(cmd, entrada);
        cmd.Parameters.AddWithValue("@usuario", usuarioId);
        await cmd.ExecuteNonQueryAsync(ct);
        var id = cmd.LastInsertedId;
        await LogAuditoria.RegistrarAsync(conexao, transacao,
            usuarioId, entrada.UnidadeId, "Criar", "Aviso", id,
            "Aviso publicado no mural.", ct);
        await transacao.CommitAsync(ct);
        return (ResultadoAviso.Concluido, id);
    }

    public async Task<ResultadoAviso> AtualizarAsync(long id,
        AvisoEntrada entrada, long usuarioId, bool admin, CancellationToken ct)
    {
        if (!EntradaValida(entrada)) return ResultadoAviso.DadosInvalidos;
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);
        var (escopo, unidadeAtual) = await ConsultarEscopoAsync(
            conexao, transacao, id, usuarioId, admin, true, ct);
        if (escopo != ResultadoAviso.Concluido) return escopo;
        if (entrada.UnidadeId != unidadeAtual) return ResultadoAviso.UnidadeImutavel;
        var autorizado = await ValidarDestinoAsync(conexao, transacao,
            unidadeAtual, usuarioId, admin, ct);
        if (autorizado != ResultadoAviso.Concluido) return autorizado;

        using var cmd = new MySqlCommand("""
            UPDATE aviso SET titulo=@titulo, corpo=@corpo,
                inicio_utc=@inicio, fim_utc=@fim,
                atualizado_em_utc=UTC_TIMESTAMP(6) WHERE id=@id
            """, conexao, transacao);
        Preencher(cmd, entrada);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync(ct);
        await LogAuditoria.RegistrarAsync(conexao, transacao,
            usuarioId, unidadeAtual, "Atualizar", "Aviso", id,
            "Conteúdo ou período de aviso atualizado.", ct);
        await transacao.CommitAsync(ct);
        return ResultadoAviso.Concluido;
    }

    public async Task<ResultadoAviso> AlterarSituacaoAsync(long id,
        bool ativo, long usuarioId, bool admin, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);
        var (resultado, unidadeId) = await ConsultarEscopoAsync(
            conexao, transacao, id, usuarioId, admin, true, ct);
        if (resultado != ResultadoAviso.Concluido) return resultado;
        if (ativo)
        {
            var destino = await ValidarDestinoAsync(conexao, transacao,
                unidadeId, usuarioId, admin, ct);
            if (destino != ResultadoAviso.Concluido) return destino;
        }
        using var cmd = new MySqlCommand("""
            UPDATE aviso SET ativo=@ativo,
                atualizado_em_utc=UTC_TIMESTAMP(6) WHERE id=@id
            """, conexao, transacao);
        cmd.Parameters.AddWithValue("@ativo", ativo);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync(ct);
        await LogAuditoria.RegistrarAsync(conexao, transacao,
            usuarioId, unidadeId, "AlterarSituacao", "Aviso", id,
            ativo ? "Aviso reativado." : "Aviso retirado do mural.", ct);
        await transacao.CommitAsync(ct);
        return ResultadoAviso.Concluido;
    }

    private static bool EntradaValida(AvisoEntrada entrada) =>
        !string.IsNullOrWhiteSpace(entrada.Titulo) &&
        entrada.Titulo.Trim().Length is >= 2 and <= 150 &&
        !string.IsNullOrWhiteSpace(entrada.Corpo) &&
        entrada.Corpo.Trim().Length is >= 2 and <= 4000 &&
        entrada.InicioUtc.HasValue && entrada.FimUtc.HasValue &&
        entrada.InicioUtc.Value.Year >= 1000 &&
        entrada.FimUtc.Value > entrada.InicioUtc.Value &&
        (entrada.UnidadeId is null or > 0);

    private static void Preencher(MySqlCommand cmd, AvisoEntrada entrada)
    {
        cmd.Parameters.AddWithValue("@unidade", entrada.UnidadeId.HasValue
            ? entrada.UnidadeId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@titulo", entrada.Titulo.Trim());
        cmd.Parameters.AddWithValue("@corpo", entrada.Corpo.Trim());
        cmd.Parameters.AddWithValue("@inicio", entrada.InicioUtc!.Value);
        cmd.Parameters.AddWithValue("@fim", entrada.FimUtc!.Value);
    }

    private static async Task<ResultadoAviso> ValidarDestinoAsync(
        MySqlConnection conexao, MySqlTransaction transacao,
        long? unidadeId, long usuarioId, bool admin, CancellationToken ct)
    {
        if (!unidadeId.HasValue)
            return admin ? ResultadoAviso.Concluido : ResultadoAviso.ForaDoEscopo;
        using var cmd = new MySqlCommand("""
            SELECT u.status, (@admin=TRUE OR EXISTS (
                SELECT 1 FROM papel_usuario pu
                WHERE pu.usuario_id=@usuario AND pu.unidade_id=u.id
                  AND pu.papel IN ('PresidenteClube','FuncionarioUnidade')))
            FROM unidade u WHERE u.id=@unidade
            """, conexao, transacao);
        cmd.Parameters.AddWithValue("@unidade", unidadeId.Value);
        cmd.Parameters.AddWithValue("@usuario", usuarioId);
        cmd.Parameters.AddWithValue("@admin", admin);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return ResultadoAviso.NaoEncontrado;
        if (!reader.GetBoolean(1)) return ResultadoAviso.ForaDoEscopo;
        return reader.GetString(0) == "Ativa"
            ? ResultadoAviso.Concluido : ResultadoAviso.UnidadeInativa;
    }

    private static async Task<ResultadoAviso> EscopoDoAvisoAsync(
        MySqlConnection conexao, MySqlTransaction? transacao,
        long id, long usuarioId, bool admin, bool travar, CancellationToken ct)
    {
        var (resultado, _) = await ConsultarEscopoAsync(
            conexao, transacao, id, usuarioId, admin, travar, ct);
        return resultado;
    }

    private static async Task<(ResultadoAviso Resultado, long? UnidadeId)>
        ConsultarEscopoAsync(MySqlConnection conexao, MySqlTransaction? transacao,
            long id, long usuarioId, bool admin, bool travar, CancellationToken ct)
    {
        using var cmd = new MySqlCommand("""
            SELECT a.unidade_id, (@admin=TRUE OR
                (a.unidade_id IS NOT NULL AND EXISTS (
                    SELECT 1 FROM papel_usuario pu
                    WHERE pu.usuario_id=@usuario AND pu.unidade_id=a.unidade_id
                      AND pu.papel IN ('PresidenteClube','FuncionarioUnidade'))))
            FROM aviso a WHERE a.id=@id
            """ + (travar ? " FOR UPDATE" : ""), conexao, transacao);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@usuario", usuarioId);
        cmd.Parameters.AddWithValue("@admin", admin);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return (ResultadoAviso.NaoEncontrado, null);
        var unidade = reader.IsDBNull(0) ? (long?)null : reader.GetInt64(0);
        return reader.GetBoolean(1)
            ? (ResultadoAviso.Concluido, unidade)
            : (ResultadoAviso.ForaDoEscopo, null);
    }

    private static async Task<List<AvisoItem>> LerListaAsync(
        MySqlCommand cmd, CancellationToken ct)
    {
        var lista = new List<AvisoItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) lista.Add(Ler(reader));
        return lista;
    }

    private static AvisoItem Ler(MySqlDataReader reader) => new(
        reader.GetInt64(0), reader.IsDBNull(1) ? null : reader.GetInt64(1),
        reader.IsDBNull(2) ? null : reader.GetString(2),
        reader.GetString(3), reader.GetString(4),
        DateTime.SpecifyKind(reader.GetDateTime(5), DateTimeKind.Utc),
        DateTime.SpecifyKind(reader.GetDateTime(6), DateTimeKind.Utc),
        reader.GetBoolean(7), reader.GetString(8));
}
