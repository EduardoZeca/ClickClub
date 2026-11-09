using ClickClub.Api.Contratos;
using MySqlConnector;

namespace ClickClub.Api.Dados;

public sealed class MinhaAssociacaoRepositorio(IConfiguration configuration)
{
    private readonly string _connectionString = configuration
        .GetConnectionString("ClickClub")
        ?? throw new InvalidOperationException("Conexão MySQL não configurada.");

    public async Task<MinhaAssociacaoResposta?> ConsultarAsync(
        long usuarioId, bool titular, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);

        var sql = titular
            ? """
              SELECT p.nome, a.matricula, u.nome, c.nome, a.status
              FROM usuario us JOIN pessoa p ON p.id = us.pessoa_id
              JOIN associacao a ON a.titular_pessoa_id = p.id
              JOIN unidade u ON u.id = a.unidade_origem_id
              JOIN categoria_associativa c ON c.id = a.categoria_id
              WHERE us.id = @usuario AND a.status <> 'Cancelada'
              LIMIT 1
              """
            : """
              SELECT p.nome, a.matricula, u.nome, c.nome, a.status
              FROM usuario us JOIN pessoa p ON p.id = us.pessoa_id
              JOIN dependente_associacao d ON d.dependente_pessoa_id = p.id
              JOIN associacao a ON a.id = d.associacao_id
              JOIN unidade u ON u.id = a.unidade_origem_id
              JOIN categoria_associativa c ON c.id = a.categoria_id
              WHERE us.id = @usuario AND d.ativo = TRUE
                AND a.status <> 'Cancelada'
              LIMIT 1
              """;

        using var comando = new MySqlCommand(sql, conexao);
        comando.Parameters.AddWithValue("@usuario", usuarioId);
        await using var reader = await comando.ExecuteReaderAsync(ct);

        return await reader.ReadAsync(ct)
            ? new MinhaAssociacaoResposta(
                reader.GetString(0), titular ? "Titular" : "Dependente",
                reader.GetString(1), reader.GetString(2),
                reader.GetString(3), reader.GetString(4))
            : null;
    }
}
