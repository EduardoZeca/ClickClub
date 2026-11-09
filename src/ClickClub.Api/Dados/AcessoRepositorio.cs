using ClickClub.Api.Seguranca;
using MySqlConnector;

namespace ClickClub.Api.Dados;

public sealed class AcessoRepositorio
{
    private readonly string _connectionString;

    public AcessoRepositorio(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("ClickClub")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:ClickClub não foi configurada.");
    }

    public Task<ContaAcesso?> BuscarPorLoginAsync(
        string login, CancellationToken cancellationToken = default) =>
        BuscarAsync("u.login = @valor", login, cancellationToken);

    public Task<ContaAcesso?> BuscarPorIdAsync(
        long id, CancellationToken cancellationToken = default) =>
        BuscarAsync("u.id = @valor", id, cancellationToken);

    private async Task<ContaAcesso?> BuscarAsync(
        string condicao, object valor, CancellationToken cancellationToken)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(cancellationToken);

        using var comando = new MySqlCommand($"""
            SELECT u.id, u.pessoa_id, p.nome, u.login,
                   u.senha_hash, u.ativo, p.status
            FROM usuario u
            INNER JOIN pessoa p ON p.id = u.pessoa_id
            WHERE {condicao}
            LIMIT 1
            """, conexao);
        comando.Parameters.AddWithValue("@valor", valor);

        ContaAcesso? conta = null;
        await using (var leitor = await comando.ExecuteReaderAsync(cancellationToken))
        {
            if (await leitor.ReadAsync(cancellationToken))
            {
                conta = new ContaAcesso
                {
                    Id = leitor.GetInt64(0),
                    PessoaId = leitor.GetInt64(1),
                    Nome = leitor.GetString(2),
                    Login = leitor.GetString(3),
                    SenhaHash = leitor.GetString(4),
                    Ativo = leitor.GetBoolean(5),
                    StatusPessoa = leitor.GetString(6)
                };
            }
        }

        if (conta is null)
            return null;

        using var comandoPapeis = new MySqlCommand("""
            SELECT papel, unidade_id
            FROM papel_usuario
            WHERE usuario_id = @usuarioId
              AND (
                papel NOT IN ('Titular', 'Dependente')
                OR (papel = 'Titular' AND EXISTS (
                    SELECT 1 FROM associacao a
                    WHERE a.titular_pessoa_id = @pessoaId
                      AND a.status <> 'Cancelada'
                ))
                OR (papel = 'Dependente' AND EXISTS (
                    SELECT 1 FROM dependente_associacao d
                    JOIN associacao a ON a.id = d.associacao_id
                    WHERE d.dependente_pessoa_id = @pessoaId
                      AND d.ativo = TRUE AND a.status <> 'Cancelada'
                ))
              )
            ORDER BY papel, unidade_id
            """, conexao);
        comandoPapeis.Parameters.AddWithValue("@usuarioId", conta.Id);
        comandoPapeis.Parameters.AddWithValue("@pessoaId", conta.PessoaId);

        var papeis = new List<PapelAcesso>();
        await using (var leitor = await comandoPapeis.ExecuteReaderAsync(cancellationToken))
        {
            while (await leitor.ReadAsync(cancellationToken))
            {
                papeis.Add(new PapelAcesso(
                    leitor.GetString(0),
                    leitor.IsDBNull(1) ? null : leitor.GetInt64(1)));
            }
        }

        conta.Papeis = papeis;
        return conta;
    }

    public async Task<bool> ExisteAdministradorGeralAsync(
        CancellationToken cancellationToken = default)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(cancellationToken);

        using var comando = new MySqlCommand("""
            SELECT EXISTS (
                SELECT 1 FROM papel_usuario
                WHERE papel = 'AdministradorGeral'
            )
            """, conexao);
        return Convert.ToInt32(
            await comando.ExecuteScalarAsync(cancellationToken)) == 1;
    }

    public async Task CriarAdministradorInicialAsync(
        string nome, string login, string senhaHash,
        CancellationToken cancellationToken = default)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(cancellationToken);
        await using var transacao = await conexao.BeginTransactionAsync(cancellationToken);

        try
        {
            using var criarPessoa = new MySqlCommand("""
                INSERT INTO pessoa (nome, status) VALUES (@nome, 'Ativa')
                """, conexao, transacao);
            criarPessoa.Parameters.AddWithValue("@nome", nome);
            await criarPessoa.ExecuteNonQueryAsync(cancellationToken);
            var pessoaId = criarPessoa.LastInsertedId;

            using var criarUsuario = new MySqlCommand("""
                INSERT INTO usuario (pessoa_id, login, senha_hash, ativo)
                VALUES (@pessoaId, @login, @hash, TRUE)
                """, conexao, transacao);
            criarUsuario.Parameters.AddWithValue("@pessoaId", pessoaId);
            criarUsuario.Parameters.AddWithValue("@login", login);
            criarUsuario.Parameters.AddWithValue("@hash", senhaHash);
            await criarUsuario.ExecuteNonQueryAsync(cancellationToken);
            var usuarioId = criarUsuario.LastInsertedId;

            using var criarPapel = new MySqlCommand("""
                INSERT INTO papel_usuario (usuario_id, papel, unidade_id)
                VALUES (@usuarioId, 'AdministradorGeral', NULL)
                """, conexao, transacao);
            criarPapel.Parameters.AddWithValue("@usuarioId", usuarioId);
            await criarPapel.ExecuteNonQueryAsync(cancellationToken);

            using var auditar = new MySqlCommand("""
                INSERT INTO log_auditoria
                    (usuario_id, unidade_id, acao, entidade,
                     identificador_entidade, data_hora, resumo_alteracao)
                VALUES
                    (@usuarioId, NULL, 'CriacaoAdminInicial', 'Usuario',
                     @identificador, @agora, 'Criação segura do primeiro administrador')
                """, conexao, transacao);
            auditar.Parameters.AddWithValue("@usuarioId", usuarioId);
            auditar.Parameters.AddWithValue("@identificador", usuarioId.ToString());
            auditar.Parameters.AddWithValue("@agora", DateTime.UtcNow);
            await auditar.ExecuteNonQueryAsync(cancellationToken);

            await transacao.CommitAsync(cancellationToken);
        }
        catch
        {
            await transacao.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task AtualizarSenhaHashAsync(
        long usuarioId, string novoHash,
        CancellationToken cancellationToken = default)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(cancellationToken);
        using var comando = new MySqlCommand("""
            UPDATE usuario SET senha_hash = @hash WHERE id = @id
            """, conexao);
        comando.Parameters.AddWithValue("@hash", novoHash);
        comando.Parameters.AddWithValue("@id", usuarioId);
        await comando.ExecuteNonQueryAsync(cancellationToken);
    }
}
