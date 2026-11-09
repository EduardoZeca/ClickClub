using ClickClub.Api.Contratos;
using ClickClub.Api.Seguranca;
using Microsoft.AspNetCore.Identity;
using MySqlConnector;

namespace ClickClub.Api.Dados;

public sealed class ContaAdministrativaRepositorio(
    IConfiguration configuration, IPasswordHasher<ContaAcesso> hasher)
{
    private readonly string _connectionString = configuration
        .GetConnectionString("ClickClub")
        ?? throw new InvalidOperationException("Conexão MySQL não configurada.");

    public async Task<ContaAdministrativaResposta?> BuscarPorPessoaAsync(
        long pessoaId, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        using var comando = new MySqlCommand("""
            SELECT u.id, u.pessoa_id, u.login, u.ativo,
                   COALESCE((SELECT pu.papel FROM papel_usuario pu
                             WHERE pu.usuario_id = u.id
                               AND pu.papel IN ('Titular', 'Dependente')
                             ORDER BY pu.id LIMIT 1), 'Outro')
            FROM usuario u WHERE u.pessoa_id = @pessoa
            """, conexao);
        comando.Parameters.AddWithValue("@pessoa", pessoaId);
        await using var reader = await comando.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)
            ? new ContaAdministrativaResposta(reader.GetInt64(0),
                reader.GetInt64(1), reader.GetString(2),
                reader.GetBoolean(3), reader.GetString(4))
            : null;
    }

    public async Task<long> CriarAsync(
        ContaCriacaoEntrada entrada, long autorId, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);

        var unidadeId = await VerificarVinculoAsync(
            conexao, transacao, entrada.PessoaId, entrada.Papel, ct);

        var conta = new ContaAcesso
        {
            PessoaId = entrada.PessoaId,
            Login = entrada.Login.Trim(),
            Ativo = true,
            StatusPessoa = "Ativa"
        };
        var senhaHash = hasher.HashPassword(conta, entrada.Senha);

        using var usuario = new MySqlCommand("""
            INSERT INTO usuario (pessoa_id, login, senha_hash, ativo)
            VALUES (@pessoa, @login, @hash, TRUE)
            """, conexao, transacao);
        usuario.Parameters.AddWithValue("@pessoa", entrada.PessoaId);
        usuario.Parameters.AddWithValue("@login", entrada.Login.Trim());
        usuario.Parameters.AddWithValue("@hash", senhaHash);
        await usuario.ExecuteNonQueryAsync(ct);
        var usuarioId = usuario.LastInsertedId;

        using var papel = new MySqlCommand("""
            INSERT INTO papel_usuario (usuario_id, papel, unidade_id)
            VALUES (@usuario, @papel, NULL)
            """, conexao, transacao);
        papel.Parameters.AddWithValue("@usuario", usuarioId);
        papel.Parameters.AddWithValue("@papel", entrada.Papel);
        await papel.ExecuteNonQueryAsync(ct);

        await LogAuditoria.RegistrarAsync(conexao, transacao, autorId,
            unidadeId, "Criar", "Usuario", usuarioId,
            "Conta associativa e perfil de acesso criados.", ct);
        await transacao.CommitAsync(ct);
        return usuarioId;
    }

    public async Task<bool> AlterarSituacaoAsync(long id, bool ativo,
        long autorId, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);

        using var consulta = new MySqlCommand("""
            SELECT u.ativo, u.pessoa_id,
                   (SELECT pu.papel FROM papel_usuario pu
                    WHERE pu.usuario_id = u.id AND
                          pu.papel IN ('Titular', 'Dependente')
                    LIMIT 1)
            FROM usuario u WHERE u.id = @id FOR UPDATE
            """, conexao, transacao);
        consulta.Parameters.AddWithValue("@id", id);

        bool anterior;
        long pessoaId;
        string papel;
        await using (var reader = await consulta.ExecuteReaderAsync(ct))
        {
            if (!await reader.ReadAsync(ct)) return false;
            anterior = reader.GetBoolean(0);
            pessoaId = reader.GetInt64(1);
            if (reader.IsDBNull(2))
                throw new InvalidOperationException(
                    "Somente contas associativas podem ser alteradas nesta etapa.");
            papel = reader.GetString(2);
        }

        long? unidadeId = null;
        if (ativo)
            unidadeId = await VerificarVinculoAsync(
                conexao, transacao, pessoaId, papel, ct);
        if (anterior == ativo) return true;

        using var comando = new MySqlCommand("""
            UPDATE usuario SET ativo=@ativo WHERE id=@id
            """, conexao, transacao);
        comando.Parameters.AddWithValue("@ativo", ativo);
        comando.Parameters.AddWithValue("@id", id);
        await comando.ExecuteNonQueryAsync(ct);

        await LogAuditoria.RegistrarAsync(conexao, transacao, autorId,
            unidadeId, "AlterarSituacao", "Usuario", id,
            ativo ? "Conta associativa reativada." : "Conta associativa desativada.", ct);
        await transacao.CommitAsync(ct);
        return true;
    }

    public async Task<bool> RedefinirSenhaAsync(long id, string novaSenha,
        long autorId, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);

        using var consulta = new MySqlCommand("""
            SELECT u.pessoa_id, u.login FROM usuario u
            WHERE u.id = @id AND EXISTS
                  (SELECT 1 FROM papel_usuario pu
                   WHERE pu.usuario_id = u.id
                     AND pu.papel IN ('Titular', 'Dependente'))
            FOR UPDATE
            """, conexao, transacao);
        consulta.Parameters.AddWithValue("@id", id);
        long pessoaId;
        string login;
        await using (var reader = await consulta.ExecuteReaderAsync(ct))
        {
            if (!await reader.ReadAsync(ct)) return false;
            pessoaId = reader.GetInt64(0);
            login = reader.GetString(1);
        }

        var conta = new ContaAcesso { Id = id, PessoaId = pessoaId, Login = login };
        var senhaHash = hasher.HashPassword(conta, novaSenha);
        using var atualizacao = new MySqlCommand("""
            UPDATE usuario SET senha_hash=@hash WHERE id=@id
            """, conexao, transacao);
        atualizacao.Parameters.AddWithValue("@hash", senhaHash);
        atualizacao.Parameters.AddWithValue("@id", id);
        await atualizacao.ExecuteNonQueryAsync(ct);

        await LogAuditoria.RegistrarAsync(conexao, transacao, autorId,
            null, "RedefinirSenha", "Usuario", id,
            "Senha redefinida por administrador, sem armazenar segredo.", ct);
        await transacao.CommitAsync(ct);
        return true;
    }

    private static async Task<long> VerificarVinculoAsync(
        MySqlConnection conexao, MySqlTransaction transacao,
        long pessoaId, string papel, CancellationToken ct)
    {
        var sql = papel == "Titular"
            ? """
              SELECT a.unidade_origem_id
              FROM associacao a JOIN pessoa p ON p.id = a.titular_pessoa_id
              WHERE p.id=@pessoa AND p.status='Ativa'
                AND a.status <> 'Cancelada'
              LIMIT 1
              """
            : """
              SELECT a.unidade_origem_id
              FROM dependente_associacao d
              JOIN associacao a ON a.id = d.associacao_id
              JOIN pessoa p ON p.id = d.dependente_pessoa_id
              WHERE p.id=@pessoa AND p.status='Ativa'
                AND d.ativo=TRUE AND a.status <> 'Cancelada'
              LIMIT 1
              """;

        using var comando = new MySqlCommand(sql, conexao, transacao);
        comando.Parameters.AddWithValue("@pessoa", pessoaId);
        var unidadeId = await comando.ExecuteScalarAsync(ct);
        if (unidadeId is null)
            throw new ArgumentException(
                "A pessoa precisa possuir vínculo associativo válido para esse perfil.");
        return Convert.ToInt64(unidadeId);
    }
}
