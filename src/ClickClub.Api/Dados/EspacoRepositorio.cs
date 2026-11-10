using ClickClub.Api.Contratos;
using MySqlConnector;

namespace ClickClub.Api.Dados;

public sealed class EspacoRepositorio(IConfiguration configuration)
{
    private readonly string _connectionString = configuration
        .GetConnectionString("ClickClub")
        ?? throw new InvalidOperationException("Conexão MySQL não configurada.");

    private const string ConsultaEspacos = """
        SELECT e.id, e.unidade_id, u.nome, e.nome, e.tipo,
               e.capacidade, e.taxa_base, e.ativo
        FROM espaco e
        JOIN unidade u ON u.id=e.unidade_id
        WHERE (@admin=TRUE OR EXISTS (
            SELECT 1 FROM papel_usuario pu
            WHERE pu.usuario_id=@usuario
              AND pu.unidade_id=e.unidade_id
              AND pu.papel IN ('FuncionarioUnidade','PresidenteClube')
        ))
        """;

    public async Task<List<ClubeEspacoOpcao>> OpcoesClubesAsync(
        long usuarioId, bool adminGeral, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        using var cmd = new MySqlCommand("""
            SELECT u.id, u.nome FROM unidade u
            WHERE u.status='Ativa' AND (
                @admin=TRUE OR EXISTS (
                    SELECT 1 FROM papel_usuario pu
                    WHERE pu.unidade_id=u.id AND pu.usuario_id=@usuario
                      AND pu.papel IN ('FuncionarioUnidade','PresidenteClube')
                )
            )
            ORDER BY u.nome
            """, conexao);
        cmd.Parameters.AddWithValue("@admin", adminGeral);
        cmd.Parameters.AddWithValue("@usuario", usuarioId);
        var lista = new List<ClubeEspacoOpcao>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            lista.Add(new(reader.GetInt64(0), reader.GetString(1)));
        return lista;
    }

    public async Task<List<EspacoResumo>> ListarAsync(
        long usuarioId, bool adminGeral, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        using var cmd = new MySqlCommand(ConsultaEspacos + " ORDER BY u.nome, e.nome", conexao);
        cmd.Parameters.AddWithValue("@admin", adminGeral);
        cmd.Parameters.AddWithValue("@usuario", usuarioId);
        var lista = new List<EspacoResumo>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            lista.Add(LerEspaco(reader));
        return lista;
    }

    public async Task<EspacoResumo?> BuscarAsync(
        long id, long usuarioId, bool adminGeral, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        using var cmd = new MySqlCommand(
            ConsultaEspacos + " AND e.id=@id", conexao);
        cmd.Parameters.AddWithValue("@admin", adminGeral);
        cmd.Parameters.AddWithValue("@usuario", usuarioId);
        cmd.Parameters.AddWithValue("@id", id);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? LerEspaco(reader) : null;
    }

    public async Task<(ResultadoEspaco Resultado, long Id)> CriarAsync(
        EspacoEntrada entrada, long usuarioId, bool adminGeral, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);
        var autorizacao = await ValidarClubeAsync(conexao, transacao,
            entrada.UnidadeId, usuarioId, adminGeral, ct);
        if (autorizacao != ResultadoEspaco.Concluido)
            return (autorizacao, 0);

        using var cmd = new MySqlCommand("""
            INSERT INTO espaco (unidade_id, nome, tipo, capacidade, taxa_base, ativo)
            VALUES (@unidade, @nome, @tipo, @capacidade, @taxa, TRUE)
            """, conexao, transacao);
        Preencher(cmd, entrada);
        await cmd.ExecuteNonQueryAsync(ct);
        var id = cmd.LastInsertedId;
        await LogAuditoria.RegistrarAsync(conexao, transacao, usuarioId,
            entrada.UnidadeId, "Criar", "Espaco", id, "Espaço cadastrado.", ct);
        await transacao.CommitAsync(ct);
        return (ResultadoEspaco.Concluido, id);
    }

    public async Task<ResultadoEspaco> AtualizarAsync(
        long id, EspacoEntrada entrada, long usuarioId,
        bool adminGeral, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);
        var (resultado, unidadeAtual) = await ObterClubeEspacoAsync(
            conexao, transacao, id, usuarioId, adminGeral, ct);
        if (resultado != ResultadoEspaco.Concluido) return resultado;
        if (unidadeAtual != entrada.UnidadeId)
            return ResultadoEspaco.UnidadeNaoPodeSerAlterada;

        using var cmd = new MySqlCommand("""
            UPDATE espaco SET nome=@nome, tipo=@tipo,
                              capacidade=@capacidade, taxa_base=@taxa
            WHERE id=@id
            """, conexao, transacao);
        cmd.Parameters.AddWithValue("@id", id);
        Preencher(cmd, entrada);
        await cmd.ExecuteNonQueryAsync(ct);
        await LogAuditoria.RegistrarAsync(conexao, transacao, usuarioId,
            unidadeAtual, "Atualizar", "Espaco", id, "Dados do espaço alterados.", ct);
        await transacao.CommitAsync(ct);
        return ResultadoEspaco.Concluido;
    }

    public async Task<ResultadoEspaco> AlterarSituacaoAsync(
        long id, bool ativo, long usuarioId, bool adminGeral, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);
        var (resultado, unidadeId) = await ObterClubeEspacoAsync(
            conexao, transacao, id, usuarioId, adminGeral, ct);
        if (resultado != ResultadoEspaco.Concluido) return resultado;

        using var cmd = new MySqlCommand("""
            UPDATE espaco SET ativo=@ativo WHERE id=@id
            """, conexao, transacao);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@ativo", ativo);
        await cmd.ExecuteNonQueryAsync(ct);
        await LogAuditoria.RegistrarAsync(conexao, transacao, usuarioId,
            unidadeId, ativo ? "Reativar" : "Desativar", "Espaco", id,
            ativo ? "Espaço reativado." : "Espaço desativado.", ct);
        await transacao.CommitAsync(ct);
        return ResultadoEspaco.Concluido;
    }

    private static async Task<ResultadoEspaco> ValidarClubeAsync(
        MySqlConnection conexao, MySqlTransaction transacao, long unidadeId,
        long usuarioId, bool adminGeral, CancellationToken ct)
    {
        using var cmd = new MySqlCommand("""
            SELECT u.status,
              (@admin=TRUE OR EXISTS (
                SELECT 1 FROM papel_usuario pu
                WHERE pu.usuario_id=@usuario AND pu.unidade_id=u.id
                  AND pu.papel IN ('FuncionarioUnidade','PresidenteClube')
              )) AS permitido
            FROM unidade u WHERE u.id=@unidade FOR UPDATE
            """, conexao, transacao);
        cmd.Parameters.AddWithValue("@admin", adminGeral);
        cmd.Parameters.AddWithValue("@usuario", usuarioId);
        cmd.Parameters.AddWithValue("@unidade", unidadeId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return ResultadoEspaco.NaoEncontrado;
        if (!reader.GetBoolean(1)) return ResultadoEspaco.ForaDoEscopo;
        return reader.GetString(0) == "Ativa"
            ? ResultadoEspaco.Concluido : ResultadoEspaco.UnidadeInativa;
    }

    private static async Task<(ResultadoEspaco Resultado, long UnidadeId)>
        ObterClubeEspacoAsync(MySqlConnection conexao, MySqlTransaction transacao,
        long id, long usuarioId, bool adminGeral, CancellationToken ct)
    {
        using var cmd = new MySqlCommand("""
            SELECT e.unidade_id,
              (@admin=TRUE OR EXISTS (
                SELECT 1 FROM papel_usuario pu
                WHERE pu.usuario_id=@usuario AND pu.unidade_id=e.unidade_id
                  AND pu.papel IN ('FuncionarioUnidade','PresidenteClube')
              )) AS permitido
            FROM espaco e WHERE e.id=@id FOR UPDATE
            """, conexao, transacao);
        cmd.Parameters.AddWithValue("@admin", adminGeral);
        cmd.Parameters.AddWithValue("@usuario", usuarioId);
        cmd.Parameters.AddWithValue("@id", id);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return (ResultadoEspaco.NaoEncontrado, 0);
        var unidadeId = reader.GetInt64(0);
        return reader.GetBoolean(1)
            ? (ResultadoEspaco.Concluido, unidadeId)
            : (ResultadoEspaco.ForaDoEscopo, unidadeId);
    }

    private static void Preencher(MySqlCommand cmd, EspacoEntrada entrada)
    {
        cmd.Parameters.AddWithValue("@unidade", entrada.UnidadeId);
        cmd.Parameters.AddWithValue("@nome", entrada.Nome.Trim());
        cmd.Parameters.AddWithValue("@tipo", entrada.Tipo);
        cmd.Parameters.AddWithValue("@capacidade", entrada.Capacidade);
        cmd.Parameters.AddWithValue("@taxa", entrada.TaxaBase);
    }

    private static EspacoResumo LerEspaco(MySqlDataReader reader) =>
        new(reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2),
            reader.GetString(3), reader.GetString(4), reader.GetInt32(5),
            reader.GetDecimal(6), reader.GetBoolean(7));
}
