using ClickClub.Api.Contratos;
using ClickClub.Api.Seguranca;
using Microsoft.AspNetCore.Identity;
using MySqlConnector;

namespace ClickClub.Api.Dados;

public sealed class EquipeRepositorio(
    IConfiguration configuration, IPasswordHasher<ContaAcesso> hasher)
{
    private readonly string _connectionString = configuration
        .GetConnectionString("ClickClub")
        ?? throw new InvalidOperationException("Conexão MySQL não configurada.");

    public static bool PapelLocalValido(string? papel) => papel is
        "FuncionarioUnidade" or "Porteiro" or "PresidenteClube" or "Tesoureiro";

    public async Task<List<EquipeUsuarioResposta>> ListarAsync(CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        using var comando = new MySqlCommand("""
            SELECT u.id, p.nome, u.login, u.ativo
            FROM usuario u
            JOIN pessoa p ON p.id = u.pessoa_id
            WHERE EXISTS (
                SELECT 1 FROM papel_usuario pu
                WHERE pu.usuario_id = u.id AND pu.papel IN
                    ('FuncionarioUnidade', 'Porteiro', 'PresidenteClube', 'Tesoureiro')
            ) OR EXISTS (
                SELECT 1 FROM log_auditoria l
                WHERE l.entidade = 'Usuario' AND l.acao = 'CriarEquipe'
                  AND l.identificador_entidade = CAST(u.id AS CHAR)
            )
            ORDER BY p.nome
            """, conexao);

        var pessoas = new List<(long Id, string Nome, string Login, bool Ativo)>();
        await using (var reader = await comando.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct))
                pessoas.Add((reader.GetInt64(0), reader.GetString(1),
                    reader.GetString(2), reader.GetBoolean(3)));

        var lista = new List<EquipeUsuarioResposta>();
        foreach (var pessoa in pessoas)
            lista.Add(new EquipeUsuarioResposta(
                pessoa.Id, pessoa.Nome, pessoa.Login, pessoa.Ativo,
                await ListarAtribuicoesAsync(conexao, pessoa.Id, ct)));
        return lista;
    }

    public async Task<EquipeUsuarioResposta?> BuscarAsync(
        long id, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        using var comando = new MySqlCommand("""
            SELECT u.id, p.nome, u.login, u.ativo
            FROM usuario u JOIN pessoa p ON p.id=u.pessoa_id
            WHERE u.id=@id AND (
                EXISTS (SELECT 1 FROM papel_usuario pu
                        WHERE pu.usuario_id=u.id AND pu.papel IN
                        ('FuncionarioUnidade', 'Porteiro', 'PresidenteClube', 'Tesoureiro'))
                OR EXISTS (SELECT 1 FROM log_auditoria l
                           WHERE l.entidade='Usuario' AND l.acao='CriarEquipe'
                             AND l.identificador_entidade=CAST(u.id AS CHAR))
            )
            """, conexao);
        comando.Parameters.AddWithValue("@id", id);

        long usuarioId;
        string nome, login;
        bool ativo;
        await using (var reader = await comando.ExecuteReaderAsync(ct))
        {
            if (!await reader.ReadAsync(ct)) return null;
            usuarioId = reader.GetInt64(0);
            nome = reader.GetString(1);
            login = reader.GetString(2);
            ativo = reader.GetBoolean(3);
        }

        return new EquipeUsuarioResposta(usuarioId, nome, login, ativo,
            await ListarAtribuicoesAsync(conexao, usuarioId, ct));
    }

    public async Task<long> CriarAsync(EquipeCriacaoEntrada entrada,
        long autorId, CancellationToken ct)
    {
        if (!PapelLocalValido(entrada.Papel))
            throw new ArgumentException("Perfil funcional inválido.");

        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);
        await VerificarUnidadeAtivaAsync(
            conexao, transacao, entrada.UnidadeId, ct);

        using var pessoa = new MySqlCommand("""
            INSERT INTO pessoa (nome, telefone, email, status)
            VALUES (@nome, @telefone, @email, 'Ativa')
            """, conexao, transacao);
        pessoa.Parameters.AddWithValue("@nome", entrada.Nome.Trim());
        pessoa.Parameters.AddWithValue("@telefone", Opcional(entrada.Telefone));
        pessoa.Parameters.AddWithValue("@email", Opcional(entrada.Email));
        await pessoa.ExecuteNonQueryAsync(ct);
        var pessoaId = pessoa.LastInsertedId;

        var conta = new ContaAcesso
        {
            PessoaId = pessoaId,
            Login = entrada.Login.Trim(),
            StatusPessoa = "Ativa",
            Ativo = true
        };
        var hash = hasher.HashPassword(conta, entrada.Senha);
        using var usuario = new MySqlCommand("""
            INSERT INTO usuario (pessoa_id, login, senha_hash, ativo)
            VALUES (@pessoa, @login, @hash, TRUE)
            """, conexao, transacao);
        usuario.Parameters.AddWithValue("@pessoa", pessoaId);
        usuario.Parameters.AddWithValue("@login", entrada.Login.Trim());
        usuario.Parameters.AddWithValue("@hash", hash);
        await usuario.ExecuteNonQueryAsync(ct);
        var usuarioId = usuario.LastInsertedId;

        using var papel = new MySqlCommand("""
            INSERT INTO papel_usuario (usuario_id, papel, unidade_id)
            VALUES (@usuario, @papel, @unidade)
            """, conexao, transacao);
        papel.Parameters.AddWithValue("@usuario", usuarioId);
        papel.Parameters.AddWithValue("@papel", entrada.Papel);
        papel.Parameters.AddWithValue("@unidade", entrada.UnidadeId);
        await papel.ExecuteNonQueryAsync(ct);

        await LogAuditoria.RegistrarAsync(conexao, transacao, autorId,
            entrada.UnidadeId, "CriarEquipe", "Usuario", usuarioId,
            "Conta funcional e atribuição inicial criadas.", ct);
        await transacao.CommitAsync(ct);
        return usuarioId;
    }

    public async Task<bool> AtribuirAsync(long usuarioId,
        EquipeAtribuicaoEntrada entrada, long autorId, CancellationToken ct)
    {
        if (!PapelLocalValido(entrada.Papel))
            throw new ArgumentException("Perfil funcional inválido.");

        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);
        if (!await EUsuarioEquipeAsync(conexao, transacao, usuarioId, ct))
            return false;
        await VerificarUnidadeAtivaAsync(conexao, transacao, entrada.UnidadeId, ct);

        using var comando = new MySqlCommand("""
            INSERT INTO papel_usuario (usuario_id, papel, unidade_id)
            VALUES (@usuario, @papel, @unidade)
            """, conexao, transacao);
        comando.Parameters.AddWithValue("@usuario", usuarioId);
        comando.Parameters.AddWithValue("@papel", entrada.Papel);
        comando.Parameters.AddWithValue("@unidade", entrada.UnidadeId);
        await comando.ExecuteNonQueryAsync(ct);
        var atribuicaoId = comando.LastInsertedId;

        await LogAuditoria.RegistrarAsync(conexao, transacao, autorId,
            entrada.UnidadeId, "ConcederPapel", "PapelUsuario", atribuicaoId,
            "Permissão funcional concedida para unidade.", ct);
        await transacao.CommitAsync(ct);
        return true;
    }

    public async Task<bool> RevogarAsync(long usuarioId,
        EquipeAtribuicaoEntrada entrada, long autorId, CancellationToken ct)
    {
        if (!PapelLocalValido(entrada.Papel))
            throw new ArgumentException("Perfil funcional inválido.");

        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);
        using var consulta = new MySqlCommand("""
            SELECT id FROM papel_usuario
            WHERE usuario_id=@usuario AND papel=@papel AND unidade_id=@unidade
            FOR UPDATE
            """, conexao, transacao);
        consulta.Parameters.AddWithValue("@usuario", usuarioId);
        consulta.Parameters.AddWithValue("@papel", entrada.Papel);
        consulta.Parameters.AddWithValue("@unidade", entrada.UnidadeId);
        var resultado = await consulta.ExecuteScalarAsync(ct);
        if (resultado is null) return false;
        var atribuicaoId = Convert.ToInt64(resultado);

        using var comando = new MySqlCommand("""
            DELETE FROM papel_usuario WHERE id=@id
            """, conexao, transacao);
        comando.Parameters.AddWithValue("@id", atribuicaoId);
        await comando.ExecuteNonQueryAsync(ct);

        await LogAuditoria.RegistrarAsync(conexao, transacao, autorId,
            entrada.UnidadeId, "RevogarPapel", "PapelUsuario", atribuicaoId,
            "Permissão funcional revogada da unidade.", ct);
        await transacao.CommitAsync(ct);
        return true;
    }

    public async Task<bool> AlterarSituacaoAsync(long usuarioId, bool ativo,
        long autorId, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);
        if (!await EUsuarioEquipeAsync(conexao, transacao, usuarioId, ct))
            return false;

        using var comando = new MySqlCommand("""
            UPDATE usuario SET ativo=@ativo WHERE id=@id
            """, conexao, transacao);
        comando.Parameters.AddWithValue("@id", usuarioId);
        comando.Parameters.AddWithValue("@ativo", ativo);
        await comando.ExecuteNonQueryAsync(ct);

        await LogAuditoria.RegistrarAsync(conexao, transacao, autorId,
            null, "AlterarSituacao", "Usuario", usuarioId,
            ativo ? "Conta funcional reativada." : "Conta funcional desativada.", ct);
        await transacao.CommitAsync(ct);
        return true;
    }

    public async Task<bool> RedefinirSenhaAsync(long usuarioId, string novaSenha,
        long autorId, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);
        if (!await EUsuarioEquipeAsync(conexao, transacao, usuarioId, ct))
            return false;

        using var consulta = new MySqlCommand("""
            SELECT pessoa_id, login FROM usuario WHERE id=@id FOR UPDATE
            """, conexao, transacao);
        consulta.Parameters.AddWithValue("@id", usuarioId);
        long pessoaId;
        string login;
        await using (var reader = await consulta.ExecuteReaderAsync(ct))
        {
            if (!await reader.ReadAsync(ct)) return false;
            pessoaId = reader.GetInt64(0);
            login = reader.GetString(1);
        }

        var conta = new ContaAcesso
        {
            Id = usuarioId,
            PessoaId = pessoaId,
            Login = login
        };
        var hash = hasher.HashPassword(conta, novaSenha);
        using var comando = new MySqlCommand("""
            UPDATE usuario SET senha_hash=@hash WHERE id=@id
            """, conexao, transacao);
        comando.Parameters.AddWithValue("@id", usuarioId);
        comando.Parameters.AddWithValue("@hash", hash);
        await comando.ExecuteNonQueryAsync(ct);

        await LogAuditoria.RegistrarAsync(conexao, transacao, autorId,
            null, "RedefinirSenha", "Usuario", usuarioId,
            "Senha funcional redefinida, sem armazenar o segredo.", ct);
        await transacao.CommitAsync(ct);
        return true;
    }

    public async Task<List<ClubeAtribuidoResposta>> ListarClubesAsync(
        long usuarioId, bool adminGeral, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        var sql = adminGeral
            ? """
              SELECT id, nome, sigla, municipio, uf, status
              FROM unidade ORDER BY nome
              """
            : """
              SELECT DISTINCT u.id, u.nome, u.sigla, u.municipio, u.uf, u.status
              FROM unidade u JOIN papel_usuario pu ON pu.unidade_id=u.id
              WHERE pu.usuario_id=@usuario AND pu.papel IN
                  ('FuncionarioUnidade', 'Porteiro', 'PresidenteClube', 'Tesoureiro')
              ORDER BY u.nome
              """;
        using var comando = new MySqlCommand(sql, conexao);
        if (!adminGeral) comando.Parameters.AddWithValue("@usuario", usuarioId);
        var clubes = new List<ClubeAtribuidoResposta>();
        await using var reader = await comando.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            clubes.Add(new ClubeAtribuidoResposta(
                reader.GetInt64(0), reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetString(3), reader.GetString(4), reader.GetString(5)));
        return clubes;
    }

    private static async Task<List<EquipeAtribuicaoResposta>>
        ListarAtribuicoesAsync(MySqlConnection conexao, long usuarioId,
            CancellationToken ct)
    {
        using var comando = new MySqlCommand("""
            SELECT pu.papel, pu.unidade_id, u.nome
            FROM papel_usuario pu JOIN unidade u ON u.id=pu.unidade_id
            WHERE pu.usuario_id=@usuario AND pu.papel IN
                ('FuncionarioUnidade', 'Porteiro', 'PresidenteClube', 'Tesoureiro')
            ORDER BY u.nome, pu.papel
            """, conexao);
        comando.Parameters.AddWithValue("@usuario", usuarioId);
        var papeis = new List<EquipeAtribuicaoResposta>();
        await using var reader = await comando.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            papeis.Add(new EquipeAtribuicaoResposta(
                reader.GetString(0), reader.GetInt64(1), reader.GetString(2)));
        return papeis;
    }

    private static async Task<bool> EUsuarioEquipeAsync(
        MySqlConnection conexao, MySqlTransaction transacao,
        long usuarioId, CancellationToken ct)
    {
        using var comando = new MySqlCommand("""
            SELECT EXISTS (
                SELECT 1 FROM usuario u WHERE u.id=@id AND (
                    EXISTS (SELECT 1 FROM papel_usuario pu
                            WHERE pu.usuario_id=u.id AND pu.papel IN
                            ('FuncionarioUnidade', 'Porteiro', 'PresidenteClube', 'Tesoureiro'))
                    OR EXISTS (SELECT 1 FROM log_auditoria l
                               WHERE l.entidade='Usuario' AND l.acao='CriarEquipe'
                                 AND l.identificador_entidade=CAST(u.id AS CHAR))
                )
            )
            """, conexao, transacao);
        comando.Parameters.AddWithValue("@id", usuarioId);
        return Convert.ToInt32(await comando.ExecuteScalarAsync(ct)) == 1;
    }

    private static async Task VerificarUnidadeAtivaAsync(
        MySqlConnection conexao, MySqlTransaction transacao,
        long unidadeId, CancellationToken ct)
    {
        using var comando = new MySqlCommand("""
            SELECT EXISTS (SELECT 1 FROM unidade WHERE id=@id AND status='Ativa')
            """, conexao, transacao);
        comando.Parameters.AddWithValue("@id", unidadeId);
        if (Convert.ToInt32(await comando.ExecuteScalarAsync(ct)) != 1)
            throw new ArgumentException("O clube selecionado precisa estar ativo.");
    }

    private static object Opcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? DBNull.Value : valor.Trim();
}
