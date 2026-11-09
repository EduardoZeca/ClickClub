using MySqlConnector;

namespace ClickClub.Api.Dados;

internal static class LogAuditoria
{
    // O registro deve participar da mesma transação da mudança administrativa.
    // Não registrar senhas, tokens ou dados pessoais nos resumos.
    public static async Task RegistrarAsync(
        MySqlConnection conexao, MySqlTransaction transacao,
        long? usuarioId, long? unidadeId, string acao, string entidade,
        long identificador, string resumo, CancellationToken ct)
    {
        using var comando = new MySqlCommand("""
            INSERT INTO log_auditoria
                (usuario_id, unidade_id, acao, entidade,
                 identificador_entidade, data_hora, resumo_alteracao)
            VALUES
                (@usuario, @unidade, @acao, @entidade,
                 @identificador, UTC_TIMESTAMP(6), @resumo)
            """, conexao, transacao);

        comando.Parameters.AddWithValue("@usuario",
            usuarioId.HasValue ? (object)usuarioId.Value : DBNull.Value);
        comando.Parameters.AddWithValue("@unidade",
            unidadeId.HasValue ? (object)unidadeId.Value : DBNull.Value);
        comando.Parameters.AddWithValue("@acao", acao);
        comando.Parameters.AddWithValue("@entidade", entidade);
        comando.Parameters.AddWithValue("@identificador", identificador.ToString());
        comando.Parameters.AddWithValue("@resumo", resumo);
        await comando.ExecuteNonQueryAsync(ct);
    }
}
