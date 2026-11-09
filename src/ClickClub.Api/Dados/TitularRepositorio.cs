using ClickClub.Api.Contratos;
using MySqlConnector;

namespace ClickClub.Api.Dados;

public sealed class TitularRepositorio(IConfiguration configuration)
{
    private readonly string _connectionString = configuration
        .GetConnectionString("ClickClub")
        ?? throw new InvalidOperationException("Conexão MySQL não configurada.");

    public async Task<List<TitularResumo>> ListarAsync(CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        using var comando = new MySqlCommand("""
            SELECT a.id, p.nome, RIGHT(p.cpf, 4), a.matricula,
                   u.nome, c.nome, tv.nome, a.status
            FROM associacao a
            JOIN pessoa p ON p.id = a.titular_pessoa_id
            JOIN unidade u ON u.id = a.unidade_origem_id
            JOIN categoria_associativa c ON c.id = a.categoria_id
            LEFT JOIN tipo_vinculo_associativo tv ON tv.id = a.tipo_vinculo_id
            ORDER BY a.id DESC
            """, conexao);

        var lista = new List<TitularResumo>();
        await using var reader = await comando.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            lista.Add(new TitularResumo(
                reader.GetInt64(0), reader.GetString(1),
                reader.IsDBNull(2) ? "—" : reader.GetString(2),
                reader.GetString(3), reader.GetString(4),
                reader.GetString(5), reader.IsDBNull(6) ? "Não informado" : reader.GetString(6),
                reader.GetString(7)));
        return lista;
    }

    public async Task<TitularDetalhe?> BuscarAsync(long id, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        using var comando = new MySqlCommand("""
            SELECT a.id, p.id, p.nome, p.cpf, p.data_nascimento,
                   p.endereco, p.bairro, p.cidade, p.uf, p.sexo,
                   p.estado_civil, p.telefone, p.email, a.matricula,
                   a.unidade_origem_id, a.categoria_id, a.tipo_vinculo_id,
                   a.data_inicio, a.status
            FROM associacao a
            JOIN pessoa p ON p.id = a.titular_pessoa_id
            WHERE a.id = @id
            """, conexao);
        comando.Parameters.AddWithValue("@id", id);

        TitularDetalhe? detalhe;
        await using (var reader = await comando.ExecuteReaderAsync(ct))
        {
            if (!await reader.ReadAsync(ct)) return null;
            detalhe = new TitularDetalhe(
                reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2),
                reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                DataOpcional(reader, 4), TextoOpcional(reader, 5),
                TextoOpcional(reader, 6), TextoOpcional(reader, 7),
                TextoOpcional(reader, 8), TextoOpcional(reader, 9),
                TextoOpcional(reader, 10), TextoOpcional(reader, 11),
                TextoOpcional(reader, 12), reader.GetString(13),
                reader.GetInt64(14), reader.GetInt64(15),
                reader.IsDBNull(16) ? 0 : reader.GetInt64(16),
                reader.GetDateTime(17), reader.GetString(18), []);
        }

        using var interessesCmd = new MySqlCommand("""
            SELECT i.nome FROM pessoa_interesse pi
            JOIN interesse i ON i.id = pi.interesse_id
            WHERE pi.pessoa_id = @pessoa
            ORDER BY i.nome
            """, conexao);
        interessesCmd.Parameters.AddWithValue("@pessoa", detalhe.PessoaId);
        var interesses = new List<string>();
        await using (var reader = await interessesCmd.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct))
                interesses.Add(reader.GetString(0));

        return detalhe with { Interesses = interesses };
    }

    public async Task<TitularOpcoes> ListarOpcoesAsync(CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);

        var unidades = new List<UnidadeCadastroOpcao>();
        using (var cmd = new MySqlCommand("""
            SELECT id, nome, status FROM unidade ORDER BY nome
            """, conexao))
        {
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                unidades.Add(new(reader.GetInt64(0), reader.GetString(1), reader.GetString(2)));
        }

        var categorias = new List<CategoriaCadastroOpcao>();
        using (var cmd = new MySqlCommand("""
            SELECT id, nome, ativa FROM categoria_associativa ORDER BY nome
            """, conexao))
        {
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                categorias.Add(new(reader.GetInt64(0), reader.GetString(1), reader.GetBoolean(2)));
        }

        var tipos = new List<TipoVinculoCadastroOpcao>();
        using (var cmd = new MySqlCommand("""
            SELECT id, nome, direito_voto, ativo
            FROM tipo_vinculo_associativo ORDER BY id
            """, conexao))
        {
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                tipos.Add(new(reader.GetInt64(0), reader.GetString(1),
                    reader.GetBoolean(2), reader.GetBoolean(3)));
        }

        return new TitularOpcoes(unidades, categorias, tipos);
    }

    public async Task<long> CriarAsync(TitularEntrada entrada, long autorId,
        CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);

        await ValidarReferenciasAsync(conexao, transacao, entrada, ct);

        using var pessoaCmd = new MySqlCommand("""
            INSERT INTO pessoa
                (nome, cpf, data_nascimento, endereco, bairro, cidade, uf,
                 sexo, estado_civil, telefone, email, status)
            VALUES
                (@nome, @cpf, @nasc, @endereco, @bairro, @cidade, @uf,
                 @sexo, @civil, @telefone, @email, 'Ativa')
            """, conexao, transacao);
        PreencherPessoa(pessoaCmd, entrada);
        await pessoaCmd.ExecuteNonQueryAsync(ct);
        var pessoaId = pessoaCmd.LastInsertedId;

        using var associacaoCmd = new MySqlCommand("""
            INSERT INTO associacao
                (titular_pessoa_id, categoria_id, tipo_vinculo_id,
                 unidade_origem_id, matricula, status, data_inicio)
            VALUES
                (@pessoa, @categoria, @tipo, @unidade, @matricula, 'Ativa', @inicio)
            """, conexao, transacao);
        associacaoCmd.Parameters.AddWithValue("@pessoa", pessoaId);
        PreencherAssociacao(associacaoCmd, entrada);
        await associacaoCmd.ExecuteNonQueryAsync(ct);
        var associacaoId = associacaoCmd.LastInsertedId;

        await SincronizarInteressesAsync(conexao, transacao, pessoaId,
            entrada.Interesses, ct);
        await LogAuditoria.RegistrarAsync(conexao, transacao, autorId,
            entrada.UnidadeOrigemId, "Criar", "Associacao", associacaoId,
            "Titular e associação cadastrados.", ct);

        await transacao.CommitAsync(ct);
        return associacaoId;
    }

    public async Task<bool> AtualizarAsync(long id, TitularEntrada entrada,
        long autorId, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);

        using var consulta = new MySqlCommand("""
            SELECT titular_pessoa_id FROM associacao
            WHERE id = @id AND status <> 'Cancelada' FOR UPDATE
            """, conexao, transacao);
        consulta.Parameters.AddWithValue("@id", id);
        var resultado = await consulta.ExecuteScalarAsync(ct);
        if (resultado is null) return false;
        var pessoaId = Convert.ToInt64(resultado);

        await ValidarReferenciasAsync(conexao, transacao, entrada, ct);

        using var pessoaCmd = new MySqlCommand("""
            UPDATE pessoa SET nome=@nome, cpf=@cpf, data_nascimento=@nasc,
                endereco=@endereco, bairro=@bairro, cidade=@cidade, uf=@uf,
                sexo=@sexo, estado_civil=@civil, telefone=@telefone, email=@email
            WHERE id = @pessoa
            """, conexao, transacao);
        PreencherPessoa(pessoaCmd, entrada);
        pessoaCmd.Parameters.AddWithValue("@pessoa", pessoaId);
        await pessoaCmd.ExecuteNonQueryAsync(ct);

        using var associacaoCmd = new MySqlCommand("""
            UPDATE associacao SET categoria_id=@categoria, tipo_vinculo_id=@tipo,
                unidade_origem_id=@unidade, matricula=@matricula, data_inicio=@inicio
            WHERE id = @id
            """, conexao, transacao);
        associacaoCmd.Parameters.AddWithValue("@id", id);
        PreencherAssociacao(associacaoCmd, entrada);
        await associacaoCmd.ExecuteNonQueryAsync(ct);

        await SincronizarInteressesAsync(conexao, transacao, pessoaId,
            entrada.Interesses, ct);
        await LogAuditoria.RegistrarAsync(conexao, transacao, autorId,
            entrada.UnidadeOrigemId, "Atualizar", "Associacao", id,
            "Cadastro do titular e associação atualizados.", ct);

        await transacao.CommitAsync(ct);
        return true;
    }

    public async Task<bool> AlterarSituacaoAsync(long id, bool ativar,
        long autorId, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);

        using var consulta = new MySqlCommand("""
            SELECT unidade_origem_id, status FROM associacao
            WHERE id = @id FOR UPDATE
            """, conexao, transacao);
        consulta.Parameters.AddWithValue("@id", id);
        long unidadeId;
        string status;
        await using (var reader = await consulta.ExecuteReaderAsync(ct))
        {
            if (!await reader.ReadAsync(ct)) return false;
            unidadeId = reader.GetInt64(0);
            status = reader.GetString(1);
        }

        if (status == "Cancelada")
            throw new InvalidOperationException(
                "Associação cancelada não pode ser reativada nesta etapa.");

        var novoStatus = ativar ? "Ativa" : "Suspensa";
        if (status == novoStatus) return true;

        using var atualizacao = new MySqlCommand("""
            UPDATE associacao SET status = @status WHERE id = @id
            """, conexao, transacao);
        atualizacao.Parameters.AddWithValue("@id", id);
        atualizacao.Parameters.AddWithValue("@status", novoStatus);
        await atualizacao.ExecuteNonQueryAsync(ct);

        await LogAuditoria.RegistrarAsync(conexao, transacao, autorId,
            unidadeId, "AlterarSituacao", "Associacao", id,
            ativar ? "Associação reativada." : "Associação suspensa.", ct);
        await transacao.CommitAsync(ct);
        return true;
    }

    private static async Task ValidarReferenciasAsync(
        MySqlConnection conexao, MySqlTransaction transacao,
        TitularEntrada entrada, CancellationToken ct)
    {
        using var cmd = new MySqlCommand("""
            SELECT
                (SELECT COUNT(*) FROM unidade
                 WHERE id=@unidade AND status='Ativa'),
                (SELECT COUNT(*) FROM categoria_associativa
                 WHERE id=@categoria AND ativa=TRUE),
                (SELECT COUNT(*) FROM tipo_vinculo_associativo
                 WHERE id=@tipo AND ativo=TRUE)
            """, conexao, transacao);
        cmd.Parameters.AddWithValue("@unidade", entrada.UnidadeOrigemId);
        cmd.Parameters.AddWithValue("@categoria", entrada.CategoriaId);
        cmd.Parameters.AddWithValue("@tipo", entrada.TipoVinculoId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        if (reader.GetInt64(0) != 1 || reader.GetInt64(1) != 1 ||
            reader.GetInt64(2) != 1)
            throw new ArgumentException(
                "Clube, categoria e tipo de vínculo precisam estar ativos.");
    }

    private static void PreencherPessoa(MySqlCommand cmd, TitularEntrada e)
    {
        cmd.Parameters.AddWithValue("@nome", e.Nome.Trim());
        cmd.Parameters.AddWithValue("@cpf", e.Cpf);
        cmd.Parameters.AddWithValue("@nasc",
            e.DataNascimento.HasValue ? (object)e.DataNascimento.Value.Date : DBNull.Value);
        cmd.Parameters.AddWithValue("@endereco", Opcional(e.Endereco));
        cmd.Parameters.AddWithValue("@bairro", Opcional(e.Bairro));
        cmd.Parameters.AddWithValue("@cidade", Opcional(e.Cidade));
        cmd.Parameters.AddWithValue("@uf", Opcional(e.Uf?.ToUpperInvariant()));
        cmd.Parameters.AddWithValue("@sexo", Opcional(e.Sexo));
        cmd.Parameters.AddWithValue("@civil", Opcional(e.EstadoCivil));
        cmd.Parameters.AddWithValue("@telefone", Opcional(e.Telefone));
        cmd.Parameters.AddWithValue("@email", Opcional(e.Email));
    }

    private static void PreencherAssociacao(MySqlCommand cmd, TitularEntrada e)
    {
        cmd.Parameters.AddWithValue("@categoria", e.CategoriaId);
        cmd.Parameters.AddWithValue("@tipo", e.TipoVinculoId);
        cmd.Parameters.AddWithValue("@unidade", e.UnidadeOrigemId);
        cmd.Parameters.AddWithValue("@matricula", e.Matricula.Trim().ToUpperInvariant());
        cmd.Parameters.AddWithValue("@inicio", e.DataInicio.Date);
    }

    private static async Task SincronizarInteressesAsync(
        MySqlConnection conexao, MySqlTransaction transacao,
        long pessoaId, IEnumerable<string> interesses, CancellationToken ct)
    {
        using (var apagar = new MySqlCommand("""
            DELETE FROM pessoa_interesse WHERE pessoa_id = @pessoa
            """, conexao, transacao))
        {
            apagar.Parameters.AddWithValue("@pessoa", pessoaId);
            await apagar.ExecuteNonQueryAsync(ct);
        }

        foreach (var nome in interesses.Select(x => x.Trim())
                     .Where(x => x.Length > 0)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            using var catalogo = new MySqlCommand("""
                INSERT INTO interesse (nome, ativo) VALUES (@nome, TRUE)
                ON DUPLICATE KEY UPDATE id = LAST_INSERT_ID(id), ativo = TRUE
                """, conexao, transacao);
            catalogo.Parameters.AddWithValue("@nome", nome);
            await catalogo.ExecuteNonQueryAsync(ct);
            var interesseId = catalogo.LastInsertedId;

            using var vinculo = new MySqlCommand("""
                INSERT IGNORE INTO pessoa_interesse (pessoa_id, interesse_id)
                VALUES (@pessoa, @interesse)
                """, conexao, transacao);
            vinculo.Parameters.AddWithValue("@pessoa", pessoaId);
            vinculo.Parameters.AddWithValue("@interesse", interesseId);
            await vinculo.ExecuteNonQueryAsync(ct);
        }
    }

    private static object Opcional(string? s) =>
        string.IsNullOrWhiteSpace(s) ? DBNull.Value : s.Trim();

    private static DateTime? DataOpcional(MySqlDataReader r, int i) =>
        r.IsDBNull(i) ? null : r.GetDateTime(i);

    private static string? TextoOpcional(MySqlDataReader r, int i) =>
        r.IsDBNull(i) ? null : r.GetString(i);
}
