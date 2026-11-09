using System.Security.Cryptography;
using System.Text;
using ClickClub.Api.Contratos;
using MySqlConnector;

namespace ClickClub.Api.Dados;

public sealed class AdesaoRepositorio(IConfiguration configuration)
{
    private readonly string _connectionString = configuration
        .GetConnectionString("ClickClub")
        ?? throw new InvalidOperationException("Conexão MySQL não configurada.");

    public async Task<AdesaoOpcoesPublicas> OpcoesPublicasAsync(CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        var clubes = new List<UnidadeAdesaoOpcao>();
        using (var cmd = new MySqlCommand("""
            SELECT id, nome FROM unidade WHERE status='Ativa' ORDER BY nome
            """, conexao))
        {
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                clubes.Add(new(reader.GetInt64(0), reader.GetString(1)));
        }

        var tipos = new List<TipoAdesaoOpcao>();
        using (var cmd = new MySqlCommand("""
            SELECT id, nome FROM tipo_vinculo_associativo
            WHERE ativo=TRUE AND nome IN ('Convidado', 'Simpatizante')
            ORDER BY nome
            """, conexao))
        {
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                tipos.Add(new(reader.GetInt64(0), reader.GetString(1)));
        }
        return new AdesaoOpcoesPublicas(clubes, tipos);
    }

    public async Task<AdesaoSolicitacaoCriada> CriarAsync(
        AdesaoSolicitacaoEntrada entrada, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);

        using (var verificar = new MySqlCommand("""
            SELECT EXISTS (
                SELECT 1 FROM unidade u
                JOIN tipo_vinculo_associativo tv ON tv.id=@tipo
                WHERE u.id=@unidade AND u.status='Ativa'
                  AND tv.ativo=TRUE AND tv.nome IN ('Convidado', 'Simpatizante')
            )
            """, conexao, transacao))
        {
            verificar.Parameters.AddWithValue("@unidade", entrada.UnidadePretendidaId);
            verificar.Parameters.AddWithValue("@tipo", entrada.TipoVinculoId);
            if (Convert.ToInt32(await verificar.ExecuteScalarAsync(ct)) != 1)
                throw new ArgumentException("Clube ou tipo de vínculo indisponível.");
        }

        var codigoConsulta = Convert.ToBase64String(
                RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var codigoHash = HashCodigo(codigoConsulta);

        using var pessoa = new MySqlCommand("""
            INSERT INTO pessoa
                (nome, cpf, data_nascimento, endereco, bairro, cidade, uf,
                 sexo, estado_civil, telefone, email, status)
            VALUES
                (@nome, @cpf, @nascimento, @endereco, @bairro, @cidade, @uf,
                 @sexo, @civil, @telefone, @email, 'Ativa')
            """, conexao, transacao);
        pessoa.Parameters.AddWithValue("@nome", entrada.Nome.Trim());
        pessoa.Parameters.AddWithValue("@cpf", entrada.Cpf);
        pessoa.Parameters.AddWithValue("@nascimento",
            entrada.DataNascimento.HasValue
                ? (object)entrada.DataNascimento.Value.Date : DBNull.Value);
        pessoa.Parameters.AddWithValue("@endereco", Opcional(entrada.Endereco));
        pessoa.Parameters.AddWithValue("@bairro", Opcional(entrada.Bairro));
        pessoa.Parameters.AddWithValue("@cidade", Opcional(entrada.Cidade));
        pessoa.Parameters.AddWithValue("@uf", Opcional(entrada.Uf?.ToUpperInvariant()));
        pessoa.Parameters.AddWithValue("@sexo", Opcional(entrada.Sexo));
        pessoa.Parameters.AddWithValue("@civil", Opcional(entrada.EstadoCivil));
        pessoa.Parameters.AddWithValue("@telefone", Opcional(entrada.Telefone));
        pessoa.Parameters.AddWithValue("@email", Opcional(entrada.Email));
        await pessoa.ExecuteNonQueryAsync(ct);
        var pessoaId = pessoa.LastInsertedId;

        using var pedido = new MySqlCommand("""
            INSERT INTO solicitacao_adesao
                (pessoa_id, codigo_consulta_hash, unidade_pretendida_id,
                 tipo_vinculo_id, status, solicitada_em)
            VALUES
                (@pessoa, @codigo, @unidade, @tipo, 'Pendente', UTC_TIMESTAMP(6))
            """, conexao, transacao);
        pedido.Parameters.AddWithValue("@pessoa", pessoaId);
        pedido.Parameters.AddWithValue("@codigo", codigoHash);
        pedido.Parameters.AddWithValue("@unidade", entrada.UnidadePretendidaId);
        pedido.Parameters.AddWithValue("@tipo", entrada.TipoVinculoId);
        await pedido.ExecuteNonQueryAsync(ct);
        var pedidoId = pedido.LastInsertedId;

        await SalvarInteressesAsync(
            conexao, transacao, pessoaId, entrada.Interesses, ct);
        await LogAuditoria.RegistrarAsync(conexao, transacao, null,
            entrada.UnidadePretendidaId, "Solicitar", "SolicitacaoAdesao",
            pedidoId, "Solicitação recebida, sem ativar associação.", ct);

        await transacao.CommitAsync(ct);
        return new AdesaoSolicitacaoCriada(pedidoId, codigoConsulta, "Pendente");
    }

    public async Task<AdesaoConsultaResposta?> ConsultarPorCodigoAsync(
        string codigo, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        using var comando = new MySqlCommand("""
            SELECT s.id, s.status, u.nome, tv.nome,
                   s.solicitada_em, s.analisada_em, s.motivo
            FROM solicitacao_adesao s
            JOIN unidade u ON u.id=s.unidade_pretendida_id
            JOIN tipo_vinculo_associativo tv ON tv.id=s.tipo_vinculo_id
            WHERE s.codigo_consulta_hash=@codigo
            """, conexao);
        comando.Parameters.AddWithValue("@codigo", HashCodigo(codigo));
        await using var reader = await comando.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)
            ? new AdesaoConsultaResposta(
                reader.GetInt64(0), reader.GetString(1),
                reader.GetString(2), reader.GetString(3),
                reader.GetDateTime(4), DataOpcional(reader, 5),
                TextoOpcional(reader, 6))
            : null;
    }

    public async Task<List<AdesaoResumo>> ListarAsync(
        long usuarioId, bool adminGeral, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        using var comando = new MySqlCommand("""
            SELECT s.id, p.nome, RIGHT(p.cpf, 4), u.nome,
                   tv.nome, s.status, s.solicitada_em
            FROM solicitacao_adesao s
            JOIN pessoa p ON p.id=s.pessoa_id
            JOIN unidade u ON u.id=s.unidade_pretendida_id
            JOIN tipo_vinculo_associativo tv ON tv.id=s.tipo_vinculo_id
            WHERE @admin=TRUE OR EXISTS (
                SELECT 1 FROM papel_usuario pu
                WHERE pu.usuario_id=@usuario AND pu.papel='PresidenteClube'
                  AND pu.unidade_id=s.unidade_pretendida_id
            )
            ORDER BY FIELD(s.status, 'Pendente', 'Aprovada', 'Rejeitada'),
                     s.solicitada_em DESC
            """, conexao);
        comando.Parameters.AddWithValue("@admin", adminGeral);
        comando.Parameters.AddWithValue("@usuario", usuarioId);
        var lista = new List<AdesaoResumo>();
        await using var reader = await comando.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            lista.Add(new AdesaoResumo(reader.GetInt64(0), reader.GetString(1),
                reader.IsDBNull(2) ? "—" : reader.GetString(2),
                reader.GetString(3), reader.GetString(4),
                reader.GetString(5), reader.GetDateTime(6)));
        return lista;
    }

    public async Task<AdesaoDetalhe?> BuscarAsync(
        long id, long usuarioId, bool adminGeral, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        using var comando = new MySqlCommand("""
            SELECT s.id, p.nome, p.cpf, p.email, p.telefone,
                   p.id, u.id, u.nome, tv.id, tv.nome,
                   s.status, s.solicitada_em, s.analisada_em, s.motivo
            FROM solicitacao_adesao s
            JOIN pessoa p ON p.id=s.pessoa_id
            JOIN unidade u ON u.id=s.unidade_pretendida_id
            JOIN tipo_vinculo_associativo tv ON tv.id=s.tipo_vinculo_id
            WHERE s.id=@id AND (
                @admin=TRUE OR EXISTS (
                    SELECT 1 FROM papel_usuario pu
                    WHERE pu.usuario_id=@usuario AND pu.papel='PresidenteClube'
                      AND pu.unidade_id=s.unidade_pretendida_id
                )
            )
            """, conexao);
        comando.Parameters.AddWithValue("@id", id);
        comando.Parameters.AddWithValue("@admin", adminGeral);
        comando.Parameters.AddWithValue("@usuario", usuarioId);
        await using var reader = await comando.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)
            ? new AdesaoDetalhe(reader.GetInt64(0), reader.GetString(1),
                reader.GetString(2), TextoOpcional(reader, 3),
                TextoOpcional(reader, 4), reader.GetInt64(5),
                reader.GetInt64(6), reader.GetString(7),
                reader.GetInt64(8), reader.GetString(9),
                reader.GetString(10), reader.GetDateTime(11),
                DataOpcional(reader, 12), TextoOpcional(reader, 13))
            : null;
    }

    public async Task<List<CategoriaAvaliacaoOpcao>> CategoriasAsync(
        CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        using var comando = new MySqlCommand("""
            SELECT id, nome FROM categoria_associativa
            WHERE ativa=TRUE ORDER BY nome
            """, conexao);
        var lista = new List<CategoriaAvaliacaoOpcao>();
        await using var reader = await comando.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            lista.Add(new CategoriaAvaliacaoOpcao(
                reader.GetInt64(0), reader.GetString(1)));
        return lista;
    }

    public async Task<ResultadoDecisaoAdesao> DecidirAsync(
        long id, AdesaoDecisaoEntrada entrada, long autorId,
        bool adminGeral, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);
        using var consulta = new MySqlCommand("""
            SELECT s.pessoa_id, s.unidade_pretendida_id, s.tipo_vinculo_id,
                   s.status, u.fuso_horario
            FROM solicitacao_adesao s
            JOIN unidade u ON u.id=s.unidade_pretendida_id
            WHERE s.id=@id FOR UPDATE
            """, conexao, transacao);
        consulta.Parameters.AddWithValue("@id", id);

        long pessoaId, unidadeId, tipoId;
        string status, fuso;
        await using (var reader = await consulta.ExecuteReaderAsync(ct))
        {
            if (!await reader.ReadAsync(ct))
                return ResultadoDecisaoAdesao.NaoEncontrada;
            pessoaId = reader.GetInt64(0);
            unidadeId = reader.GetInt64(1);
            tipoId = reader.GetInt64(2);
            status = reader.GetString(3);
            fuso = reader.GetString(4);
        }

        if (!adminGeral)
        {
            using var escopo = new MySqlCommand("""
                SELECT EXISTS (SELECT 1 FROM papel_usuario
                    WHERE usuario_id=@usuario AND papel='PresidenteClube'
                      AND unidade_id=@unidade)
                """, conexao, transacao);
            escopo.Parameters.AddWithValue("@usuario", autorId);
            escopo.Parameters.AddWithValue("@unidade", unidadeId);
            if (Convert.ToInt32(await escopo.ExecuteScalarAsync(ct)) != 1)
                return ResultadoDecisaoAdesao.ForaDoEscopo;
        }

        if (status != "Pendente") return ResultadoDecisaoAdesao.JaAnalisada;

        if (entrada.Aprovar)
        {
            if (entrada.CategoriaId is null or <= 0 ||
                string.IsNullOrWhiteSpace(entrada.Matricula) ||
                entrada.Matricula.Trim().Length is < 2 or > 30)
                return ResultadoDecisaoAdesao.DadosInvalidos;

            using var verificar = new MySqlCommand("""
                SELECT EXISTS (
                    SELECT 1 FROM categoria_associativa c
                    JOIN unidade u ON u.id=@unidade
                    JOIN pessoa p ON p.id=@pessoa
                    JOIN tipo_vinculo_associativo tv ON tv.id=@tipo
                    WHERE c.id=@categoria AND c.ativa=TRUE
                      AND u.status='Ativa' AND p.status='Ativa'
                      AND tv.ativo=TRUE
                      AND tv.nome IN ('Convidado', 'Simpatizante')
                )
                """, conexao, transacao);
            verificar.Parameters.AddWithValue("@categoria", entrada.CategoriaId.Value);
            verificar.Parameters.AddWithValue("@unidade", unidadeId);
            verificar.Parameters.AddWithValue("@pessoa", pessoaId);
            verificar.Parameters.AddWithValue("@tipo", tipoId);
            if (Convert.ToInt32(await verificar.ExecuteScalarAsync(ct)) != 1)
                return ResultadoDecisaoAdesao.DadosInvalidos;

            if (!TimeZoneInfo.TryFindSystemTimeZoneById(fuso, out var zona))
                return ResultadoDecisaoAdesao.DadosInvalidos;
            var dataLocal = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow, zona).Date;

            using var associacao = new MySqlCommand("""
                INSERT INTO associacao
                    (titular_pessoa_id, categoria_id, tipo_vinculo_id,
                     unidade_origem_id, matricula, status, data_inicio)
                VALUES
                    (@pessoa, @categoria, @tipo, @unidade,
                     @matricula, 'Ativa', @inicio)
                """, conexao, transacao);
            associacao.Parameters.AddWithValue("@pessoa", pessoaId);
            associacao.Parameters.AddWithValue("@categoria", entrada.CategoriaId.Value);
            associacao.Parameters.AddWithValue("@tipo", tipoId);
            associacao.Parameters.AddWithValue("@unidade", unidadeId);
            associacao.Parameters.AddWithValue("@matricula",
                entrada.Matricula.Trim().ToUpperInvariant());
            associacao.Parameters.AddWithValue("@inicio", dataLocal);
            await associacao.ExecuteNonQueryAsync(ct);
        }

        using var atualizar = new MySqlCommand("""
            UPDATE solicitacao_adesao
            SET status=@status, analisada_em=UTC_TIMESTAMP(6),
                analisada_por_usuario_id=@autor, motivo=@motivo
            WHERE id=@id AND status='Pendente'
            """, conexao, transacao);
        atualizar.Parameters.AddWithValue("@id", id);
        atualizar.Parameters.AddWithValue("@status",
            entrada.Aprovar ? "Aprovada" : "Rejeitada");
        atualizar.Parameters.AddWithValue("@autor", autorId);
        atualizar.Parameters.AddWithValue("@motivo", Opcional(entrada.Motivo));
        if (await atualizar.ExecuteNonQueryAsync(ct) != 1)
            return ResultadoDecisaoAdesao.JaAnalisada;

        await LogAuditoria.RegistrarAsync(conexao, transacao, autorId,
            unidadeId, entrada.Aprovar ? "Aprovar" : "Rejeitar",
            "SolicitacaoAdesao", id,
            entrada.Aprovar
                ? "Adesão aprovada e associação criada."
                : "Adesão rejeitada sem criar associação.", ct);
        await transacao.CommitAsync(ct);
        return ResultadoDecisaoAdesao.Concluida;
    }

    private static async Task SalvarInteressesAsync(
        MySqlConnection conexao, MySqlTransaction transacao,
        long pessoaId, IEnumerable<string> interesses, CancellationToken ct)
    {
        foreach (var nome in interesses.Select(x => x.Trim())
                     .Where(x => x.Length > 0)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            using var catalogo = new MySqlCommand("""
                INSERT INTO interesse (nome, ativo) VALUES (@nome, TRUE)
                ON DUPLICATE KEY UPDATE id=LAST_INSERT_ID(id), ativo=TRUE
                """, conexao, transacao);
            catalogo.Parameters.AddWithValue("@nome", nome);
            await catalogo.ExecuteNonQueryAsync(ct);
            var interesseId = catalogo.LastInsertedId;

            using var vincular = new MySqlCommand("""
                INSERT IGNORE INTO pessoa_interesse (pessoa_id, interesse_id)
                VALUES (@pessoa, @interesse)
                """, conexao, transacao);
            vincular.Parameters.AddWithValue("@pessoa", pessoaId);
            vincular.Parameters.AddWithValue("@interesse", interesseId);
            await vincular.ExecuteNonQueryAsync(ct);
        }
    }

    private static string HashCodigo(string codigo) =>
        Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(codigo))).ToLowerInvariant();

    private static object Opcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? DBNull.Value : valor.Trim();

    private static DateTime? DataOpcional(MySqlDataReader reader, int indice) =>
        reader.IsDBNull(indice) ? null : reader.GetDateTime(indice);

    private static string? TextoOpcional(MySqlDataReader reader, int indice) =>
        reader.IsDBNull(indice) ? null : reader.GetString(indice);
}
