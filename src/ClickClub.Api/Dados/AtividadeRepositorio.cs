using ClickClub.Api.Contratos;
using MySqlConnector;

namespace ClickClub.Api.Dados;

public sealed class AtividadeRepositorio(IConfiguration configuration)
{
    private readonly string _connectionString = configuration
        .GetConnectionString("ClickClub")
        ?? throw new InvalidOperationException("Conexão MySQL não configurada.");

    public async Task<List<AtividadeItem>> ListarAsync(CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        using var cmd = new MySqlCommand("""
            SELECT id, nome, descricao, tipo FROM atividade ORDER BY nome, tipo, id
            """, conexao);
        var itens = new List<AtividadeItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) itens.Add(Ler(reader));
        return itens;
    }

    public async Task<AtividadeItem?> BuscarAsync(long id, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        using var cmd = new MySqlCommand("""
            SELECT id, nome, descricao, tipo FROM atividade WHERE id=@id
            """, conexao);
        cmd.Parameters.AddWithValue("@id", id);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Ler(reader) : null;
    }

    public async Task<long> CriarAsync(AtividadeEntrada entrada, long usuarioId,
        CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);
        using var cmd = new MySqlCommand("""
            INSERT INTO atividade (nome, descricao, tipo)
            VALUES (@nome, @descricao, @tipo)
            """, conexao, transacao);
        Preencher(cmd, entrada);
        await cmd.ExecuteNonQueryAsync(ct);
        var id = cmd.LastInsertedId;
        await LogAuditoria.RegistrarAsync(conexao, transacao, usuarioId,
            null, "Criar", "Atividade", id, "Atividade cadastrada no catálogo.", ct);
        await transacao.CommitAsync(ct);
        return id;
    }

    public async Task<bool> AtualizarAsync(long id, AtividadeEntrada entrada,
        long usuarioId, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);
        using (var verificar = new MySqlCommand(
            "SELECT id FROM atividade WHERE id=@id FOR UPDATE", conexao, transacao))
        {
            verificar.Parameters.AddWithValue("@id", id);
            if (await verificar.ExecuteScalarAsync(ct) is null) return false;
        }
        using var cmd = new MySqlCommand("""
            UPDATE atividade SET nome=@nome, descricao=@descricao, tipo=@tipo
            WHERE id=@id
            """, conexao, transacao);
        cmd.Parameters.AddWithValue("@id", id);
        Preencher(cmd, entrada);
        await cmd.ExecuteNonQueryAsync(ct);
        await LogAuditoria.RegistrarAsync(conexao, transacao, usuarioId,
            null, "Atualizar", "Atividade", id, "Catálogo de atividade alterado.", ct);
        await transacao.CommitAsync(ct);
        return true;
    }

    private static void Preencher(MySqlCommand cmd, AtividadeEntrada entrada)
    {
        cmd.Parameters.AddWithValue("@nome", entrada.Nome.Trim());
        cmd.Parameters.AddWithValue("@descricao", string.IsNullOrWhiteSpace(entrada.Descricao)
            ? DBNull.Value : entrada.Descricao.Trim());
        cmd.Parameters.AddWithValue("@tipo", entrada.Tipo.Trim());
    }

    private static AtividadeItem Ler(MySqlDataReader reader) => new(
        reader.GetInt64(0), reader.GetString(1),
        reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetString(3));
}
