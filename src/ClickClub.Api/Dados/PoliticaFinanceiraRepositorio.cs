using ClickClub.Api.Contratos;
using MySqlConnector;

namespace ClickClub.Api.Dados;

public sealed class PoliticaFinanceiraRepositorio(IConfiguration configuration)
{
    private readonly string _connectionString = configuration
        .GetConnectionString("ClickClub")
        ?? throw new InvalidOperationException("Conexão MySQL não configurada.");

    public async Task<FinanceiroOpcoesResposta> OpcoesAsync(CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);

        var unidades = new List<UnidadeFinanceiraOpcao>();
        using (var comando = new MySqlCommand("""
            SELECT id, nome FROM unidade
            WHERE status='Ativa' ORDER BY nome
            """, conexao))
        await using (var reader = await comando.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct))
                unidades.Add(new(reader.GetInt64(0), reader.GetString(1)));

        var categorias = new List<CategoriaFinanceiraOpcao>();
        using (var comando = new MySqlCommand("""
            SELECT id, nome FROM categoria_associativa
            WHERE ativa=TRUE ORDER BY nome
            """, conexao))
        await using (var reader = await comando.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct))
                categorias.Add(new(reader.GetInt64(0), reader.GetString(1)));

        var associacoes = new List<AssociacaoFinanceiraOpcao>();
        using (var comando = new MySqlCommand("""
            SELECT a.id, a.matricula, p.nome, u.nome, c.nome,
                   a.data_inicio, a.status
            FROM associacao a
            JOIN pessoa p ON p.id=a.titular_pessoa_id
            JOIN unidade u ON u.id=a.unidade_origem_id
            JOIN categoria_associativa c ON c.id=a.categoria_id
            WHERE a.status <> 'Cancelada'
            ORDER BY p.nome, a.id
            """, conexao))
        await using (var reader = await comando.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct))
                associacoes.Add(new(reader.GetInt64(0), reader.GetString(1),
                    reader.GetString(2), reader.GetString(3), reader.GetString(4),
                    reader.GetDateTime(5), reader.GetString(6)));

        return new FinanceiroOpcoesResposta(unidades, categorias, associacoes);
    }

    public async Task<List<PoliticaFinanceiraResposta>> ListarAsync(CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        using var comando = new MySqlCommand("""
            SELECT pf.id, pf.unidade_id, u.nome, pf.categoria_id, c.nome,
                   pf.valor_mensal, pf.vigencia_inicio, pf.vigencia_fim
            FROM politica_financeira pf
            JOIN unidade u ON u.id=pf.unidade_id
            JOIN categoria_associativa c ON c.id=pf.categoria_id
            ORDER BY u.nome, c.nome, pf.vigencia_inicio DESC
            """, conexao);

        var lista = new List<PoliticaFinanceiraResposta>();
        await using var reader = await comando.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            lista.Add(new(reader.GetInt64(0), reader.GetInt64(1),
                reader.GetString(2), reader.GetInt64(3), reader.GetString(4),
                reader.GetDecimal(5), reader.GetDateTime(6),
                reader.IsDBNull(7) ? null : reader.GetDateTime(7)));
        return lista;
    }

    public async Task<long> CriarAsync(PoliticaFinanceiraEntrada entrada,
        long usuarioId, CancellationToken ct)
    {
        if (entrada.VigenciaInicio is null ||
            entrada.VigenciaInicio.Value.Day != 1 ||
            entrada.VigenciaInicio.Value.TimeOfDay != TimeSpan.Zero)
            throw new ArgumentException(
                "A vigência deve começar no primeiro dia de um mês.");
        if (entrada.ValorMensal < 0)
            throw new ArgumentException("O valor mensal não pode ser negativo.");

        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);

        using (var verificar = new MySqlCommand("""
            SELECT COUNT(*) FROM unidade u
            JOIN categoria_associativa c ON c.id=@categoria
            WHERE u.id=@unidade AND u.status='Ativa' AND c.ativa=TRUE
            """, conexao, transacao))
        {
            verificar.Parameters.AddWithValue("@unidade", entrada.UnidadeId);
            verificar.Parameters.AddWithValue("@categoria", entrada.CategoriaId);
            if (Convert.ToInt32(await verificar.ExecuteScalarAsync(ct)) == 0)
                throw new ArgumentException("Clube ou categoria inexistente/inativo.");
        }

        using var inserir = new MySqlCommand("""
            INSERT INTO politica_financeira
                (unidade_id, categoria_id, valor_mensal, vigencia_inicio,
                 vigencia_fim, dia_vencimento, dias_tolerancia, percentual_multa)
            VALUES (@unidade, @categoria, @valor, @vigencia,
                    NULL, 10, 10, 2.00)
            """, conexao, transacao);
        inserir.Parameters.AddWithValue("@unidade", entrada.UnidadeId);
        inserir.Parameters.AddWithValue("@categoria", entrada.CategoriaId);
        inserir.Parameters.AddWithValue("@valor", entrada.ValorMensal);
        inserir.Parameters.AddWithValue("@vigencia", entrada.VigenciaInicio.Value.Date);
        await inserir.ExecuteNonQueryAsync(ct);
        var id = inserir.LastInsertedId;

        await LogAuditoria.RegistrarAsync(conexao, transacao, usuarioId,
            entrada.UnidadeId, "Criar", "PoliticaFinanceira", id,
            "Política financeira mensal cadastrada com vigência versionada.", ct);
        await transacao.CommitAsync(ct);
        return id;
    }
}
