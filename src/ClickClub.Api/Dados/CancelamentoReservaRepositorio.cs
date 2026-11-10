using ClickClub.Api.Contratos;
using MySqlConnector;

namespace ClickClub.Api.Dados;

public sealed class CancelamentoReservaRepositorio(IConfiguration configuration)
{
    private readonly string _connectionString = configuration
        .GetConnectionString("ClickClub")
        ?? throw new InvalidOperationException("Conexão MySQL não configurada.");

    public async Task<(ResultadoCancelamento Resultado, CancelamentoReservaResposta? Dados)>
        CancelarAsync(long reservaId, long usuarioId, bool titular,
            CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);

        // Mesmo padrão do cadastro: travar primeiro a associação.
        // Cancelamento e nova reserva do mesmo titular ficam serializados.
        long associacaoId;
        using (var identificar = new MySqlCommand("""
            SELECT a.id FROM associacao a
            JOIN usuario u ON u.id=@usuario
            WHERE u.id=@usuario AND EXISTS (
                SELECT 1 FROM reserva r
                WHERE r.id=@reserva AND r.associacao_id=a.id
                  AND ((@titular=TRUE AND u.pessoa_id=a.titular_pessoa_id)
                    OR (@titular=FALSE AND r.responsavel_pessoa_id=u.pessoa_id
                      AND EXISTS (SELECT 1 FROM dependente_associacao d
                          WHERE d.associacao_id=a.id AND d.dependente_pessoa_id=u.pessoa_id
                            AND d.ativo=TRUE))))
            FOR UPDATE
            """, conexao, transacao))
        {
            identificar.Parameters.AddWithValue("@usuario", usuarioId);
            identificar.Parameters.AddWithValue("@reserva", reservaId);
            identificar.Parameters.AddWithValue("@titular", titular);
            var valor = await identificar.ExecuteScalarAsync(ct);
            if (valor is null)
                return (ResultadoCancelamento.ReservaNaoEncontrada, null);
            associacaoId = Convert.ToInt64(valor);
        }

        DateTime inicioUtc;
        decimal valorOriginal;
        string tipo, status, statusCobranca;
        long unidadeId;

        using (var reserva = new MySqlCommand("""
            SELECT r.inicio_utc, r.valor_contratado,
                   r.tipo_espaco_contratado, r.status,
                   e.unidade_id, c.status
            FROM reserva r
            JOIN espaco e ON e.id=r.espaco_id
            LEFT JOIN cobranca_reserva c ON c.reserva_id=r.id
            WHERE r.id=@reserva AND r.associacao_id=@associacao
            FOR UPDATE
            """, conexao, transacao))
        {
            reserva.Parameters.AddWithValue("@reserva", reservaId);
            reserva.Parameters.AddWithValue("@associacao", associacaoId);
            await using var reader = await reserva.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                return (ResultadoCancelamento.ReservaNaoEncontrada, null);

            inicioUtc = DateTime.SpecifyKind(reader.GetDateTime(0), DateTimeKind.Utc);
            valorOriginal = reader.GetDecimal(1);
            tipo = reader.GetString(2);
            status = reader.GetString(3);
            unidadeId = reader.GetInt64(4);
            statusCobranca = reader.IsDBNull(5) ? "Gratuita" : reader.GetString(5);
        }

        if (status != "Confirmada")
            return (ResultadoCancelamento.JaCancelada, null);

        var agoraUtc = DateTime.UtcNow;
        if (agoraUtc >= inicioUtc)
            return (ResultadoCancelamento.UtilizacaoIniciada, null);

        // O ajuste de lançamentos já transferidos exige a fatura e será
        // realizado no componente financeiro, para não gerar crédito duplo.
        if (statusCobranca == "Transferida")
            return (ResultadoCancelamento.CobrancaTransferida, null);

        var multa = tipo == "SalaoFestas" &&
            agoraUtc > inicioUtc.AddHours(-24)
            ? Math.Min(50m, valorOriginal)
            : 0m;

        var credito = statusCobranca == "QuitadaSimulada"
            ? valorOriginal - multa : 0m;
        var situacao = credito > 0 ? "CreditoPendente"
            : multa > 0 && statusCobranca == "EmAberto"
                ? "MultaPendente" : "SemAcerto";

        using (var cancelar = new MySqlCommand("""
            UPDATE reserva SET status='Cancelada',
                cancelada_em_utc=UTC_TIMESTAMP(6)
            WHERE id=@reserva AND status='Confirmada'
            """, conexao, transacao))
        {
            cancelar.Parameters.AddWithValue("@reserva", reservaId);
            if (await cancelar.ExecuteNonQueryAsync(ct) != 1)
                throw new InvalidOperationException("Reserva modificada durante o cancelamento.");
        }

        if (statusCobranca != "Gratuita")
        {
            using var encerrar = new MySqlCommand("""
                UPDATE cobranca_reserva SET status='Cancelada'
                WHERE reserva_id=@reserva AND status IN ('EmAberto','QuitadaSimulada')
                """, conexao, transacao);
            encerrar.Parameters.AddWithValue("@reserva", reservaId);
            if (await encerrar.ExecuteNonQueryAsync(ct) != 1)
                throw new InvalidOperationException("Cobrança modificada durante o cancelamento.");
        }

        using (var registrar = new MySqlCommand("""
            INSERT INTO cancelamento_reserva
                (reserva_id, usuario_responsavel_id, cancelada_em_utc,
                 valor_original, situacao_original_cobranca,
                 multa_simulada, credito_simulado_pendente,
                 situacao_financeira)
            VALUES (@reserva, @usuario, UTC_TIMESTAMP(6),
                    @original, @cobrancaAnterior, @multa, @credito, @situacao)
            """, conexao, transacao))
        {
            registrar.Parameters.AddWithValue("@reserva", reservaId);
            registrar.Parameters.AddWithValue("@usuario", usuarioId);
            registrar.Parameters.AddWithValue("@original", valorOriginal);
            registrar.Parameters.AddWithValue("@cobrancaAnterior", statusCobranca);
            registrar.Parameters.AddWithValue("@multa", multa);
            registrar.Parameters.AddWithValue("@credito", credito);
            registrar.Parameters.AddWithValue("@situacao", situacao);
            await registrar.ExecuteNonQueryAsync(ct);
        }

        await LogAuditoria.RegistrarAsync(conexao, transacao, usuarioId,
            unidadeId, "Cancelar", "Reserva", reservaId,
            $"Reserva cancelada; multa simulada: {multa:F2}; situação: {situacao}.", ct);

        await transacao.CommitAsync(ct);
        return (ResultadoCancelamento.Concluido,
            new CancelamentoReservaResposta(reservaId, multa, credito, situacao));
    }
}
