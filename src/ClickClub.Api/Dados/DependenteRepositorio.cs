using ClickClub.Api.Contratos;
using MySqlConnector;

namespace ClickClub.Api.Dados;

public sealed class DependenteRepositorio(IConfiguration configuration)
{
    private readonly string _connectionString = configuration
        .GetConnectionString("ClickClub")
        ?? throw new InvalidOperationException("Conexão MySQL não configurada.");

    public async Task<List<DependenteResumo>> ListarAsync(
        long associacaoId, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        using var comando = new MySqlCommand("""
            SELECT d.id, p.id, d.associacao_id, p.nome, RIGHT(p.cpf, 4),
                   p.data_nascimento, d.ativo, u.id, u.login
            FROM dependente_associacao d
            JOIN pessoa p ON p.id = d.dependente_pessoa_id
            LEFT JOIN usuario u ON u.pessoa_id = p.id
            WHERE d.associacao_id = @associacao
            ORDER BY p.nome
            """, conexao);
        comando.Parameters.AddWithValue("@associacao", associacaoId);

        var lista = new List<DependenteResumo>();
        await using var reader = await comando.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            lista.Add(new DependenteResumo(
                reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2),
                reader.GetString(3), TextoOpcional(reader, 4),
                reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                reader.GetBoolean(6),
                reader.IsDBNull(7) ? null : reader.GetInt64(7),
                TextoOpcional(reader, 8)));
        return lista;
    }

    public async Task<DependenteDetalhe?> BuscarAsync(long id, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        using var comando = new MySqlCommand("""
            SELECT d.id, p.id, d.associacao_id, p.nome, p.cpf,
                   p.data_nascimento, p.endereco, p.bairro, p.cidade, p.uf,
                   p.sexo, p.estado_civil, p.telefone, p.email, d.ativo
            FROM dependente_associacao d
            JOIN pessoa p ON p.id = d.dependente_pessoa_id
            WHERE d.id = @id
            """, conexao);
        comando.Parameters.AddWithValue("@id", id);
        await using var reader = await comando.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        return new DependenteDetalhe(
            reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2),
            reader.GetString(3), TextoOpcional(reader, 4),
            reader.IsDBNull(5) ? null : reader.GetDateTime(5),
            TextoOpcional(reader, 6),
            TextoOpcional(reader, 7), TextoOpcional(reader, 8),
            TextoOpcional(reader, 9), TextoOpcional(reader, 10),
            TextoOpcional(reader, 11), TextoOpcional(reader, 12),
            TextoOpcional(reader, 13), reader.GetBoolean(14));
    }

    public async Task<long> CriarAsync(DependenteEntrada entrada, long autorId,
        CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);
        var unidadeId = await VerificarAssociacaoAsync(
            conexao, transacao, entrada.AssociacaoId,
            validarNovoVinculo: true, ct: ct);

        using var pessoa = new MySqlCommand("""
            INSERT INTO pessoa
                (nome, cpf, data_nascimento, endereco, bairro, cidade, uf,
                 sexo, estado_civil, telefone, email, status)
            VALUES
                (@nome, @cpf, @nascimento, @endereco, @bairro, @cidade, @uf,
                 @sexo, @civil, @telefone, @email, 'Ativa')
            """, conexao, transacao);
        PreencherPessoa(pessoa, entrada);
        await pessoa.ExecuteNonQueryAsync(ct);
        var pessoaId = pessoa.LastInsertedId;

        using var vinculo = new MySqlCommand("""
            INSERT INTO dependente_associacao
                (associacao_id, dependente_pessoa_id, ativo, autorizacoes)
            VALUES (@associacao, @pessoa, TRUE, NULL)
            """, conexao, transacao);
        vinculo.Parameters.AddWithValue("@associacao", entrada.AssociacaoId);
        vinculo.Parameters.AddWithValue("@pessoa", pessoaId);
        await vinculo.ExecuteNonQueryAsync(ct);
        var id = vinculo.LastInsertedId;

        await LogAuditoria.RegistrarAsync(conexao, transacao, autorId,
            unidadeId, "Criar", "DependenteAssociacao", id,
            "Dependente vinculado à associação.", ct);
        await transacao.CommitAsync(ct);
        return id;
    }

    public async Task<bool> AtualizarAsync(long id, DependenteEntrada entrada,
        long autorId, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);

        using var consulta = new MySqlCommand("""
            SELECT dependente_pessoa_id, associacao_id
            FROM dependente_associacao WHERE id = @id FOR UPDATE
            """, conexao, transacao);
        consulta.Parameters.AddWithValue("@id", id);
        long pessoaId, associacaoId;
        await using (var reader = await consulta.ExecuteReaderAsync(ct))
        {
            if (!await reader.ReadAsync(ct)) return false;
            pessoaId = reader.GetInt64(0);
            associacaoId = reader.GetInt64(1);
        }

        if (entrada.AssociacaoId != associacaoId)
            throw new ArgumentException(
                "O vínculo não pode ser transferido pela edição cadastral.");

        var unidadeId = await VerificarAssociacaoAsync(
            conexao, transacao, associacaoId,
            validarNovoVinculo: false, ct: ct);

        using var pessoa = new MySqlCommand("""
            UPDATE pessoa SET nome=@nome, cpf=@cpf, data_nascimento=@nascimento,
                endereco=@endereco, bairro=@bairro, cidade=@cidade, uf=@uf,
                sexo=@sexo, estado_civil=@civil, telefone=@telefone, email=@email
            WHERE id = @pessoa
            """, conexao, transacao);
        PreencherPessoa(pessoa, entrada);
        pessoa.Parameters.AddWithValue("@pessoa", pessoaId);
        await pessoa.ExecuteNonQueryAsync(ct);

        await LogAuditoria.RegistrarAsync(conexao, transacao, autorId,
            unidadeId, "Atualizar", "DependenteAssociacao", id,
            "Dados cadastrais de dependente alterados.", ct);
        await transacao.CommitAsync(ct);
        return true;
    }

    public async Task<bool> AlterarSituacaoAsync(long id, bool ativo,
        long autorId, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);

        // Identifica o vínculo sem travá-lo; a associação é bloqueada primeiro,
        // como nos cadastros, para serializar reativação e troca de categoria.
        using var identificar = new MySqlCommand("""
            SELECT associacao_id FROM dependente_associacao WHERE id=@id
            """, conexao, transacao);
        identificar.Parameters.AddWithValue("@id", id);
        var identificacao = await identificar.ExecuteScalarAsync(ct);
        if (identificacao is null) return false;
        var associacaoId = Convert.ToInt64(identificacao);

        long unidadeId;
        string statusAssociacao, categoria;
        using (var associacao = new MySqlCommand("""
            SELECT a.unidade_origem_id, a.status, c.nome
            FROM associacao a
            JOIN categoria_associativa c ON c.id=a.categoria_id
            WHERE a.id=@associacao FOR UPDATE
            """, conexao, transacao))
        {
            associacao.Parameters.AddWithValue("@associacao", associacaoId);
            await using var reader = await associacao.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return false;
            unidadeId = reader.GetInt64(0);
            statusAssociacao = reader.GetString(1);
            categoria = reader.GetString(2);
        }

        bool situacaoAtual;
        using (var consulta = new MySqlCommand("""
            SELECT ativo FROM dependente_associacao
            WHERE id=@id AND associacao_id=@associacao FOR UPDATE
            """, conexao, transacao))
        {
            consulta.Parameters.AddWithValue("@id", id);
            consulta.Parameters.AddWithValue("@associacao", associacaoId);
            var valor = await consulta.ExecuteScalarAsync(ct);
            if (valor is null) return false;
            situacaoAtual = Convert.ToBoolean(valor);
        }

        if (ativo && statusAssociacao == "Cancelada")
            throw new InvalidOperationException(
                "Não é possível reativar dependente de associação cancelada.");
        if (situacaoAtual == ativo) return true;
        if (ativo && EhIndividual(categoria))
            throw new InvalidOperationException(
                "A categoria Individual não permite reativar dependentes. " +
                "Regularize a categoria da associação antes de reativar o vínculo.");

        using var comando = new MySqlCommand("""
            UPDATE dependente_associacao SET ativo=@ativo WHERE id=@id
            """, conexao, transacao);
        comando.Parameters.AddWithValue("@ativo", ativo);
        comando.Parameters.AddWithValue("@id", id);
        await comando.ExecuteNonQueryAsync(ct);

        await LogAuditoria.RegistrarAsync(conexao, transacao, autorId,
            unidadeId, "AlterarSituacao", "DependenteAssociacao", id,
            ativo ? "Dependente reativado." : "Dependente desvinculado.", ct);
        await transacao.CommitAsync(ct);
        return true;
    }

    private static async Task<long> VerificarAssociacaoAsync(
        MySqlConnection conexao, MySqlTransaction transacao,
        long associacaoId, bool validarNovoVinculo, CancellationToken ct)
    {
        using var consulta = new MySqlCommand("""
            SELECT a.unidade_origem_id, c.nome
            FROM associacao a
            JOIN categoria_associativa c ON c.id=a.categoria_id
            WHERE a.id=@id AND a.status <> 'Cancelada' FOR UPDATE
            """, conexao, transacao);
        consulta.Parameters.AddWithValue("@id", associacaoId);
        await using var reader = await consulta.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            throw new ArgumentException("Associação inexistente ou cancelada.");
        if (validarNovoVinculo && EhIndividual(reader.GetString(1)))
            throw new ArgumentException(
                "A categoria Individual não permite cadastrar dependentes. " +
                "Altere a categoria da associação para Familiar antes do cadastro.");
        return reader.GetInt64(0);
    }

    private static bool EhIndividual(string categoria) =>
        string.Equals(categoria.Trim(), "Individual",
            StringComparison.OrdinalIgnoreCase);

    private static void PreencherPessoa(MySqlCommand comando, DependenteEntrada e)
    {
        comando.Parameters.AddWithValue("@nome", e.Nome.Trim());
        comando.Parameters.AddWithValue("@cpf", Opcional(e.Cpf));
        comando.Parameters.AddWithValue("@nascimento", e.DataNascimento.Date);
        comando.Parameters.AddWithValue("@endereco", Opcional(e.Endereco));
        comando.Parameters.AddWithValue("@bairro", Opcional(e.Bairro));
        comando.Parameters.AddWithValue("@cidade", Opcional(e.Cidade));
        comando.Parameters.AddWithValue("@uf", Opcional(e.Uf?.ToUpperInvariant()));
        comando.Parameters.AddWithValue("@sexo", Opcional(e.Sexo));
        comando.Parameters.AddWithValue("@civil", Opcional(e.EstadoCivil));
        comando.Parameters.AddWithValue("@telefone", Opcional(e.Telefone));
        comando.Parameters.AddWithValue("@email", Opcional(e.Email));
    }

    private static object Opcional(string? s) =>
        string.IsNullOrWhiteSpace(s) ? DBNull.Value : s.Trim();

    private static string? TextoOpcional(MySqlDataReader r, int i) =>
        r.IsDBNull(i) ? null : r.GetString(i);
}
