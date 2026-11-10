using ClickClub.Api.Contratos;
using MySqlConnector;

namespace ClickClub.Api.Dados;

internal static class PersistenciaConvidados
{
    public static bool ListaValida(
        IReadOnlyList<ConvidadoReservaEntrada>? convidados,
        int quantidadeParticipantes)
    {
        if (convidados is null || quantidadeParticipantes < 1 ||
            convidados.Count > quantidadeParticipantes - 1)
            return false;

        var documentos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in convidados)
        {
            if (item is null || item.Nome is null ||
                item.IdentificacaoMinima is null)
                return false;
            var nome = item.Nome.Trim();
            var documento = item.IdentificacaoMinima.Trim();
            if (nome.Length is < 2 or > 150 ||
                documento.Length is < 3 or > 60 ||
                !documentos.Add(documento))
                return false;
        }
        return true;
    }

    // Substitui a lista ativa sem apagar os registros históricos:
    // convidados removidos permanecem com situação "Removido".
    public static async Task SincronizarAsync(
        MySqlConnection conexao, MySqlTransaction transacao, long reservaId,
        IEnumerable<ConvidadoReservaEntrada> convidados, CancellationToken ct)
    {
        using (var desativar = new MySqlCommand("""
            UPDATE convidado_reserva
            SET situacao='Removido', atualizado_em_utc=UTC_TIMESTAMP(6)
            WHERE reserva_id=@reserva AND situacao='Autorizado'
            """, conexao, transacao))
        {
            desativar.Parameters.AddWithValue("@reserva", reservaId);
            await desativar.ExecuteNonQueryAsync(ct);
        }

        foreach (var convidado in convidados)
        {
            using var cmd = new MySqlCommand("""
                INSERT INTO convidado_reserva
                    (reserva_id, nome, identificacao_minima, situacao,
                     criado_em_utc, atualizado_em_utc)
                VALUES (@reserva, @nome, @documento, 'Autorizado',
                        UTC_TIMESTAMP(6), UTC_TIMESTAMP(6))
                ON DUPLICATE KEY UPDATE nome=@nome, situacao='Autorizado',
                                        atualizado_em_utc=UTC_TIMESTAMP(6)
                """, conexao, transacao);
            cmd.Parameters.AddWithValue("@reserva", reservaId);
            cmd.Parameters.AddWithValue("@nome", convidado.Nome.Trim());
            cmd.Parameters.AddWithValue("@documento",
                convidado.IdentificacaoMinima.Trim());
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }
}
