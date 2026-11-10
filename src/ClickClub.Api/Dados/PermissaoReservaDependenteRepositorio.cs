using ClickClub.Api.Contratos;
using MySqlConnector;

namespace ClickClub.Api.Dados;

public sealed class PermissaoReservaDependenteRepositorio(IConfiguration configuration)
{
    private readonly string _connectionString = configuration
        .GetConnectionString("ClickClub")
        ?? throw new InvalidOperationException("Conexão MySQL não configurada.");

    public async Task<List<PermissaoReservaDependenteResposta>> ListarAsync(
        long usuarioTitularId, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        using var comando = new MySqlCommand("""
            SELECT d.id, p.nome, p.data_nascimento,
                   COALESCE(per.permitir_reserva_paga, FALSE)
            FROM usuario u
            JOIN associacao a ON a.titular_pessoa_id=u.pessoa_id
            JOIN dependente_associacao d ON d.associacao_id=a.id AND d.ativo=TRUE
            JOIN pessoa p ON p.id=d.dependente_pessoa_id AND p.status='Ativa'
            LEFT JOIN autorizacao_reserva_dependente per
                ON per.dependente_associacao_id=d.id
            WHERE u.id=@usuario AND a.status='Ativa'
            ORDER BY p.nome, d.id
            """, conexao);
        comando.Parameters.AddWithValue("@usuario", usuarioTitularId);
        var lista = new List<PermissaoReservaDependenteResposta>();
        await using var reader = await comando.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var nascimento = reader.IsDBNull(2) ? (DateTime?)null : reader.GetDateTime(2);
            lista.Add(new PermissaoReservaDependenteResposta(reader.GetInt64(0),
                reader.GetString(1), nascimento,
                PrecisaAutorizacao(nascimento), Convert.ToInt32(reader.GetValue(3)) == 1));
        }
        return lista;
    }

    public async Task<bool> AlterarAsync(long vinculoId, long usuarioTitularId,
        bool permitir, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);

        long unidadeId;
        using (var verificar = new MySqlCommand("""
            SELECT a.unidade_origem_id
            FROM usuario u
            JOIN associacao a ON a.titular_pessoa_id=u.pessoa_id
            JOIN dependente_associacao d ON d.associacao_id=a.id
            JOIN pessoa p ON p.id=d.dependente_pessoa_id
            WHERE u.id=@usuario AND a.status='Ativa'
              AND d.id=@vinculo AND d.ativo=TRUE AND p.status='Ativa'
            FOR UPDATE
            """, conexao, transacao))
        {
            verificar.Parameters.AddWithValue("@usuario", usuarioTitularId);
            verificar.Parameters.AddWithValue("@vinculo", vinculoId);
            var valor = await verificar.ExecuteScalarAsync(ct);
            if (valor is null) return false;
            unidadeId = Convert.ToInt64(valor);
        }

        using (var alterar = new MySqlCommand("""
            INSERT INTO autorizacao_reserva_dependente
                (dependente_associacao_id, permitir_reserva_paga,
                 titular_usuario_id, atualizado_em_utc)
            VALUES (@vinculo, @permitir, @titular, UTC_TIMESTAMP(6))
            ON DUPLICATE KEY UPDATE
                permitir_reserva_paga=@permitir,
                titular_usuario_id=@titular,
                atualizado_em_utc=UTC_TIMESTAMP(6)
            """, conexao, transacao))
        {
            alterar.Parameters.AddWithValue("@vinculo", vinculoId);
            alterar.Parameters.AddWithValue("@permitir", permitir);
            alterar.Parameters.AddWithValue("@titular", usuarioTitularId);
            await alterar.ExecuteNonQueryAsync(ct);
        }

        await LogAuditoria.RegistrarAsync(conexao, transacao,
            usuarioTitularId, unidadeId,
            permitir ? "ConcederPermissaoReservaPaga" : "RevogarPermissaoReservaPaga",
            "DependenteAssociacao", vinculoId,
            "Permissão de futuras locações pagas do dependente alterada pelo titular.", ct);
        await transacao.CommitAsync(ct);
        return true;
    }

    internal static bool PrecisaAutorizacao(DateTime? nascimento)
    {
        if (!nascimento.HasValue) return true; // Falta de data: negar por segurança.
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var data = DateOnly.FromDateTime(nascimento.Value);
        return data > hoje.AddYears(-18);
    }
}
