using ClickClub.Api.Contratos;
using MySqlConnector;

namespace ClickClub.Api.Dados;

public sealed class CategoriaRepositorio(IConfiguration configuration)
{
    private readonly string _connectionString = configuration
        .GetConnectionString("ClickClub")
        ?? throw new InvalidOperationException("Conexão MySQL não configurada.");

    public async Task<List<CategoriaResposta>> ListarAsync(CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        using var comando = new MySqlCommand("""
            SELECT id, nome, ativa FROM categoria_associativa ORDER BY nome
            """, conexao);
        var lista = new List<CategoriaResposta>();
        await using var reader = await comando.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            lista.Add(new CategoriaResposta(
                reader.GetInt64(0), reader.GetString(1), reader.GetBoolean(2)));
        return lista;
    }

    public async Task<CategoriaResposta?> BuscarAsync(long id, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        using var comando = new MySqlCommand("""
            SELECT id, nome, ativa FROM categoria_associativa WHERE id = @id
            """, conexao);
        comando.Parameters.AddWithValue("@id", id);
        await using var reader = await comando.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)
            ? new CategoriaResposta(reader.GetInt64(0),
                reader.GetString(1), reader.GetBoolean(2))
            : null;
    }

    public async Task<long> CriarAsync(string nome, long autorId,
        CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);
        using var comando = new MySqlCommand("""
            INSERT INTO categoria_associativa (nome, ativa)
            VALUES (@nome, TRUE)
            """, conexao, transacao);
        comando.Parameters.AddWithValue("@nome", nome.Trim());
        await comando.ExecuteNonQueryAsync(ct);
        var id = comando.LastInsertedId;
        await LogAuditoria.RegistrarAsync(conexao, transacao, autorId, null,
            "Criar", "CategoriaAssociativa", id,
            "Categoria associativa cadastrada.", ct);
        await transacao.CommitAsync(ct);
        return id;
    }

    public async Task<bool> RenomearAsync(long id, string nome, long autorId,
        CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);

        using (var consultar = new MySqlCommand("""
            SELECT nome FROM categoria_associativa WHERE id=@id FOR UPDATE
            """, conexao, transacao))
        {
            consultar.Parameters.AddWithValue("@id", id);
            var nomeAtual = await consultar.ExecuteScalarAsync(ct) as string;
            if (nomeAtual is null) return false;

            // Impede transformar uma categoria com famílias ativas em Individual.
            if (!EhIndividual(nomeAtual) && EhIndividual(nome))
            {
                using var dependentes = new MySqlCommand("""
                    SELECT EXISTS(
                        SELECT 1 FROM associacao a
                        JOIN dependente_associacao d
                          ON d.associacao_id=a.id AND d.ativo=TRUE
                        WHERE a.categoria_id=@categoria
                    )
                    """, conexao, transacao);
                dependentes.Parameters.AddWithValue("@categoria", id);
                if (Convert.ToInt32(await dependentes.ExecuteScalarAsync(ct)) == 1)
                    throw new InvalidOperationException(
                        "Não é possível renomear esta categoria para Individual " +
                        "enquanto houver dependentes ativos em suas associações.");
            }
        }

        using var comando = new MySqlCommand("""
            UPDATE categoria_associativa SET nome = @nome WHERE id = @id
            """, conexao, transacao);
        comando.Parameters.AddWithValue("@id", id);
        comando.Parameters.AddWithValue("@nome", nome.Trim());
        if (await comando.ExecuteNonQueryAsync(ct) == 0)
            return false;

        await LogAuditoria.RegistrarAsync(conexao, transacao, autorId, null,
            "Atualizar", "CategoriaAssociativa", id,
            "Nome da categoria alterado.", ct);
        await transacao.CommitAsync(ct);
        return true;
    }

    private static bool EhIndividual(string nome) =>
        string.Equals(nome.Trim(), "Individual",
            StringComparison.OrdinalIgnoreCase);

    public async Task<bool> AlterarSituacaoAsync(long id, bool ativa,
        long autorId, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);
        using var comando = new MySqlCommand("""
            UPDATE categoria_associativa SET ativa = @ativa WHERE id = @id
            """, conexao, transacao);
        comando.Parameters.AddWithValue("@id", id);
        comando.Parameters.AddWithValue("@ativa", ativa);
        if (await comando.ExecuteNonQueryAsync(ct) == 0)
            return false;

        await LogAuditoria.RegistrarAsync(conexao, transacao, autorId, null,
            "AlterarSituacao", "CategoriaAssociativa", id,
            ativa ? "Categoria reativada." : "Categoria desativada.", ct);
        await transacao.CommitAsync(ct);
        return true;
    }
}
