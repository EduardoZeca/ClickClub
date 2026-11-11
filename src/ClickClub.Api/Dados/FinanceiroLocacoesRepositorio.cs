using ClickClub.Api.Contratos;
using MySqlConnector;

namespace ClickClub.Api.Dados;

// O Tesoureiro enxerga somente as locações prestadas pelas unidades atribuídas.
// Uma locação interclubes não dá acesso à fatura da associação de outro clube.
public sealed class FinanceiroLocacoesRepositorio(IConfiguration configuration)
{
    private readonly string _connectionString = configuration
        .GetConnectionString("ClickClub")
        ?? throw new InvalidOperationException("Conexão MySQL não configurada.");

    public async Task<List<LocacaoFinanceiraResposta>> ListarAsync(
        long usuarioId, bool administrador, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        using var cmd = new MySqlCommand("""
            SELECT r.id, prestadora.id, prestadora.nome, origem.nome,
                   e.nome, r.inicio_utc, prestadora.fuso_horario,
                   r.status, c.status, c.modalidade,
                   c.valor_original, c.valor_acrescimo, c.competencia_destino
            FROM cobranca_reserva c
            JOIN reserva r ON r.id=c.reserva_id
            JOIN espaco e ON e.id=r.espaco_id
            JOIN unidade prestadora ON prestadora.id=e.unidade_id
            JOIN associacao a ON a.id=r.associacao_id
            JOIN unidade origem ON origem.id=a.unidade_origem_id
            WHERE @admin=TRUE OR EXISTS (
                SELECT 1 FROM papel_usuario pu
                WHERE pu.usuario_id=@usuario AND pu.papel='Tesoureiro'
                  AND pu.unidade_id=prestadora.id)
            ORDER BY r.criada_em_utc DESC, r.id DESC
            LIMIT 250
            """, conexao);
        cmd.Parameters.AddWithValue("@admin", administrador);
        cmd.Parameters.AddWithValue("@usuario", usuarioId);

        var lista = new List<LocacaoFinanceiraResposta>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var fuso = reader.GetString(6);
            if (!TimeZoneInfo.TryFindSystemTimeZoneById(fuso, out var zona))
                throw new InvalidOperationException(
                    "Fuso horário inválido no clube prestador da locação.");
            var inicio = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(reader.GetDateTime(5), DateTimeKind.Utc), zona);
            var principal = reader.GetDecimal(10);
            var acrescimo = reader.GetDecimal(11);
            lista.Add(new LocacaoFinanceiraResposta(
                reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2),
                reader.GetString(3), reader.GetString(4), inicio,
                principal, acrescimo, principal + acrescimo,
                reader.GetString(7), reader.GetString(8), reader.GetString(9),
                reader.IsDBNull(12) ? null : reader.GetDateTime(12)));
        }
        return lista;
    }
}
