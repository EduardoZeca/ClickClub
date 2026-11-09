using ClickClub.Api.Contratos;
using MySqlConnector;

namespace ClickClub.Api.Dados;

public sealed class UnidadeRepositorio(IConfiguration configuration)
{
    private readonly string _connectionString = configuration
        .GetConnectionString("ClickClub")
        ?? throw new InvalidOperationException("Conexão MySQL não configurada.");

    private MySqlConnection CriarConexao() => new(_connectionString);

    private const string Campos = """
        id, nome, sigla, endereco, bairro, municipio, uf,
        telefone, email, fuso_horario, status
        """;

    private static UnidadeResposta Ler(MySqlDataReader reader) => new(
        reader.GetInt64(0), reader.GetString(1),
        reader.IsDBNull(2) ? null : reader.GetString(2),
        reader.GetString(3),
        reader.IsDBNull(4) ? null : reader.GetString(4),
        reader.GetString(5), reader.GetString(6),
        reader.IsDBNull(7) ? null : reader.GetString(7),
        reader.IsDBNull(8) ? null : reader.GetString(8),
        reader.GetString(9), reader.GetString(10));

    public async Task<List<UnidadeResposta>> ListarAsync(CancellationToken ct)
    {
        await using var conexao = CriarConexao();
        await conexao.OpenAsync(ct);
        using var comando = new MySqlCommand(
            $"SELECT {Campos} FROM unidade ORDER BY nome", conexao);

        var lista = new List<UnidadeResposta>();
        await using var reader = await comando.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            lista.Add(Ler(reader));
        return lista;
    }

    public async Task<UnidadeResposta?> BuscarAsync(long id, CancellationToken ct)
    {
        await using var conexao = CriarConexao();
        await conexao.OpenAsync(ct);
        using var comando = new MySqlCommand(
            $"SELECT {Campos} FROM unidade WHERE id = @id", conexao);
        comando.Parameters.AddWithValue("@id", id);

        await using var reader = await comando.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Ler(reader) : null;
    }

    public async Task<long> CriarAsync(UnidadeEntrada unidade, long autorId,
        CancellationToken ct)
    {
        await using var conexao = CriarConexao();
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);
        using var comando = new MySqlCommand("""
            INSERT INTO unidade
                (nome, sigla, endereco, bairro, municipio, uf,
                 telefone, email, fuso_horario, status)
            VALUES
                (@nome, @sigla, @endereco, @bairro, @municipio, @uf,
                 @telefone, @email, @fuso, 'Ativa')
            """, conexao, transacao);
        PreencherParametros(comando, unidade);
        await comando.ExecuteNonQueryAsync(ct);
        var id = comando.LastInsertedId;
        await LogAuditoria.RegistrarAsync(conexao, transacao, autorId, id,
            "Criar", "Unidade", id, "Clube cadastrado.", ct);
        await transacao.CommitAsync(ct);
        return id;
    }

    public async Task<bool> AtualizarAsync(long id, UnidadeEntrada unidade,
        long autorId, CancellationToken ct)
    {
        await using var conexao = CriarConexao();
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);
        using var comando = new MySqlCommand("""
            UPDATE unidade SET
                nome = @nome, sigla = @sigla, endereco = @endereco,
                bairro = @bairro, municipio = @municipio, uf = @uf,
                telefone = @telefone, email = @email, fuso_horario = @fuso
            WHERE id = @id
            """, conexao, transacao);
        comando.Parameters.AddWithValue("@id", id);
        PreencherParametros(comando, unidade);
        if (await comando.ExecuteNonQueryAsync(ct) == 0)
            return false;

        await LogAuditoria.RegistrarAsync(conexao, transacao, autorId, id,
            "Atualizar", "Unidade", id, "Dados do clube atualizados.", ct);
        await transacao.CommitAsync(ct);
        return true;
    }

    public async Task<bool> AlterarSituacaoAsync(long id, bool ativa,
        long autorId, CancellationToken ct)
    {
        await using var conexao = CriarConexao();
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);
        using var comando = new MySqlCommand("""
            UPDATE unidade SET status = @status WHERE id = @id
            """, conexao, transacao);
        comando.Parameters.AddWithValue("@id", id);
        comando.Parameters.AddWithValue("@status", ativa ? "Ativa" : "Inativa");
        if (await comando.ExecuteNonQueryAsync(ct) == 0)
            return false;

        await LogAuditoria.RegistrarAsync(conexao, transacao, autorId, id,
            "AlterarSituacao", "Unidade", id,
            ativa ? "Clube reativado." : "Clube desativado.", ct);
        await transacao.CommitAsync(ct);
        return true;
    }

    private static void PreencherParametros(MySqlCommand comando, UnidadeEntrada unidade)
    {
        comando.Parameters.AddWithValue("@nome", unidade.Nome.Trim());
        comando.Parameters.AddWithValue("@sigla", unidade.Sigla.Trim().ToUpperInvariant());
        comando.Parameters.AddWithValue("@endereco", unidade.Endereco.Trim());
        comando.Parameters.AddWithValue("@bairro", DbNulo(unidade.Bairro));
        comando.Parameters.AddWithValue("@municipio", unidade.Municipio.Trim());
        comando.Parameters.AddWithValue("@uf", unidade.Uf.Trim().ToUpperInvariant());
        comando.Parameters.AddWithValue("@telefone", DbNulo(unidade.Telefone));
        comando.Parameters.AddWithValue("@email", DbNulo(unidade.Email));
        comando.Parameters.AddWithValue("@fuso", unidade.FusoHorario.Trim());
    }

    private static object DbNulo(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? DBNull.Value : valor.Trim();
}
