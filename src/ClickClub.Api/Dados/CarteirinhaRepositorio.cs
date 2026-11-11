using ClickClub.Api.Contratos;
using ClickClub.Api.Seguranca;
using MySqlConnector;
using System.Data;

namespace ClickClub.Api.Dados;

public sealed class CarteirinhaRepositorio
{
    private readonly string _connectionString;
    private readonly CarteirinhaAssinaturaServico _assinatura;

    public CarteirinhaRepositorio(
        IConfiguration configuration, CarteirinhaAssinaturaServico assinatura)
    {
        _connectionString = configuration.GetConnectionString("ClickClub")
            ?? throw new InvalidOperationException("Conexão MySQL não configurada.");
        _assinatura = assinatura;
    }

    // Apenas consulta: não substitui uma credencial expirada ou revogada.
    public async Task<CarteirinhaResposta?> ConsultarMinhaAsync(
        long usuarioId, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);

        var vinculo = await BuscarVinculoAtivoAsync(conexao, null, usuarioId, ct);
        if (vinculo is null)
            return null;

        var ultima = await BuscarUltimaAsync(conexao, null, vinculo.Value.PessoaId, ct);
        var agora = DateTime.UtcNow;
        return ultima is not null && ultima.RevogadaEmUtc is null &&
               ultima.AssociacaoId == vinculo.Value.AssociacaoId &&
               ultima.ValidaAteUtc > agora
            ? Converter(ultima, false)
            : null;
    }

    // Reaproveita a credencial com mais de uma hora restante; caso contrário,
    // revoga a anterior e emite a próxima versão. A trava da pessoa serializa
    // pedidos simultâneos, preservando uma única credencial atual.
    public async Task<CarteirinhaResposta?> EmitirOuRenovarMinhaAsync(
        long usuarioId, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        // READ COMMITTED faz uma segunda solicitação enxergar a credencial
        // inserida pela primeira após aguardar a trava FOR UPDATE da pessoa.
        await using var transacao = await conexao.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, ct);

        var vinculo = await BuscarVinculoAtivoAsync(
            conexao, transacao, usuarioId, ct);
        if (vinculo is null)
            return null;

        using (var travarPessoa = new MySqlCommand(
            "SELECT id FROM pessoa WHERE id=@pessoa FOR UPDATE", conexao, transacao))
        {
            travarPessoa.Parameters.AddWithValue("@pessoa", vinculo.Value.PessoaId);
            await travarPessoa.ExecuteScalarAsync(ct);
        }

        // Depois da trava, cada concorrente vê a credencial deixada pelo anterior.
        var ultima = await BuscarUltimaAsync(
            conexao, transacao, vinculo.Value.PessoaId, ct);
        var agora = DateTime.UtcNow;

        if (ultima is not null && ultima.RevogadaEmUtc is null &&
            ultima.AssociacaoId == vinculo.Value.AssociacaoId &&
            ultima.ValidaAteUtc > agora.AddHours(1))
        {
            await transacao.CommitAsync(ct);
            return Converter(ultima, false);
        }

        using (var revogar = new MySqlCommand("""
            UPDATE credencial_carteirinha
            SET revogada_em_utc=@agora
            WHERE pessoa_id=@pessoa AND revogada_em_utc IS NULL
            """, conexao, transacao))
        {
            revogar.Parameters.AddWithValue("@pessoa", vinculo.Value.PessoaId);
            revogar.Parameters.AddWithValue("@agora", agora);
            await revogar.ExecuteNonQueryAsync(ct);
        }

        var nova = new CredencialArmazenada(
            0, vinculo.Value.AssociacaoId, Guid.NewGuid().ToString("N"),
            agora, agora.AddHours(24), null,
            checked((ultima?.Versao ?? 0) + 1));

        using (var inserir = new MySqlCommand("""
            INSERT INTO credencial_carteirinha
                (pessoa_id, associacao_id, identificador_publico,
                 emitida_em_utc, valida_ate_utc, versao)
            VALUES (@pessoa, @associacao, @identificador,
                    @emissao, @validade, @versao)
            """, conexao, transacao))
        {
            inserir.Parameters.AddWithValue("@pessoa", vinculo.Value.PessoaId);
            inserir.Parameters.AddWithValue("@associacao", nova.AssociacaoId);
            inserir.Parameters.AddWithValue("@identificador", nova.IdentificadorPublico);
            inserir.Parameters.AddWithValue("@emissao", nova.EmitidaEmUtc);
            inserir.Parameters.AddWithValue("@validade", nova.ValidaAteUtc);
            inserir.Parameters.AddWithValue("@versao", nova.Versao);
            await inserir.ExecuteNonQueryAsync(ct);

            await LogAuditoria.RegistrarAsync(
                conexao, transacao, usuarioId, null,
                ultima is null ? "EmitirCarteirinha" : "RenovarCarteirinha",
                "CredencialCarteirinha", inserir.LastInsertedId,
                "Credencial emitida sem registrar o QR ou dados pessoais.", ct);
        }

        await transacao.CommitAsync(ct);
        return Converter(nova, true);
    }

    private CarteirinhaResposta Converter(
        CredencialArmazenada atual, bool emitidaAgora)
    {
        var payload = new CarteirinhaPayload(
            atual.Versao, atual.IdentificadorPublico,
            new DateTimeOffset(atual.EmitidaEmUtc).ToUnixTimeSeconds(),
            new DateTimeOffset(atual.ValidaAteUtc).ToUnixTimeSeconds());

        return new CarteirinhaResposta(
            _assinatura.Assinar(payload), atual.IdentificadorPublico,
            atual.EmitidaEmUtc, atual.ValidaAteUtc,
            atual.Versao, emitidaAgora);
    }

    private static async Task<(long PessoaId, long AssociacaoId)?>
        BuscarVinculoAtivoAsync(MySqlConnection conexao,
            MySqlTransaction? transacao, long usuarioId, CancellationToken ct)
    {
        using var comando = new MySqlCommand("""
            SELECT DISTINCT u.pessoa_id, a.id
            FROM usuario u
            JOIN pessoa p ON p.id=u.pessoa_id
            JOIN associacao a ON a.status='Ativa'
            WHERE u.id=@usuario AND u.ativo=TRUE AND p.status='Ativa'
              AND (
                a.titular_pessoa_id=u.pessoa_id
                OR EXISTS (
                    SELECT 1 FROM dependente_associacao d
                    WHERE d.associacao_id=a.id
                      AND d.dependente_pessoa_id=u.pessoa_id
                      AND d.ativo=TRUE
                )
              )
            LIMIT 2
            """, conexao, transacao);
        comando.Parameters.AddWithValue("@usuario", usuarioId);

        await using var leitor = await comando.ExecuteReaderAsync(ct);
        if (!await leitor.ReadAsync(ct))
            return null;

        var pessoaId = leitor.GetInt64(0);
        var associacaoId = leitor.GetInt64(1);
        if (await leitor.ReadAsync(ct))
            throw new InvalidOperationException(
                "A pessoa possui múltiplos vínculos ativos; regularize o cadastro antes de emitir a carteirinha.");

        return (pessoaId, associacaoId);
    }

    private static async Task<CredencialArmazenada?> BuscarUltimaAsync(
        MySqlConnection conexao, MySqlTransaction? transacao,
        long pessoaId, CancellationToken ct)
    {
        using var comando = new MySqlCommand("""
            SELECT id, associacao_id, identificador_publico,
                   emitida_em_utc, valida_ate_utc, revogada_em_utc, versao
            FROM credencial_carteirinha
            WHERE pessoa_id=@pessoa
            ORDER BY id DESC
            LIMIT 1
            """, conexao, transacao);
        comando.Parameters.AddWithValue("@pessoa", pessoaId);
        await using var leitor = await comando.ExecuteReaderAsync(ct);

        if (!await leitor.ReadAsync(ct))
            return null;

        return new CredencialArmazenada(
            leitor.GetInt64(0), leitor.GetInt64(1), leitor.GetString(2),
            DateTime.SpecifyKind(leitor.GetDateTime(3), DateTimeKind.Utc),
            DateTime.SpecifyKind(leitor.GetDateTime(4), DateTimeKind.Utc),
            leitor.IsDBNull(5) ? null : DateTime.SpecifyKind(
                leitor.GetDateTime(5), DateTimeKind.Utc),
            leitor.GetInt32(6));
    }

    private sealed record CredencialArmazenada(
        long Id, long AssociacaoId, string IdentificadorPublico,
        DateTime EmitidaEmUtc, DateTime ValidaAteUtc,
        DateTime? RevogadaEmUtc, int Versao);
}
