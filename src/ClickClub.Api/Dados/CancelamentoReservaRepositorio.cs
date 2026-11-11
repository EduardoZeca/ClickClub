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
        string fusoOrigem;
        using (var identificar = new MySqlCommand("""
            SELECT a.id, un.fuso_horario FROM associacao a
            JOIN unidade un ON un.id=a.unidade_origem_id
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
            await using var leitor = await identificar.ExecuteReaderAsync(ct);
            if (!await leitor.ReadAsync(ct))
                return (ResultadoCancelamento.ReservaNaoEncontrada, null);
            associacaoId = leitor.GetInt64(0);
            fusoOrigem = leitor.GetString(1);
        }

        DateTime inicioUtc;
        decimal valorOriginal;
        string tipo, status, statusCobranca;
        long unidadeId;
        long? cobrancaId, faturaDestinoId;
        decimal acrescimoTransferido;

        using (var reserva = new MySqlCommand("""
            SELECT r.inicio_utc, r.valor_contratado,
                   r.tipo_espaco_contratado, r.status,
                   e.unidade_id, c.status, c.id,
                   c.fatura_destino_id, c.valor_acrescimo
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
            cobrancaId = reader.IsDBNull(6) ? null : reader.GetInt64(6);
            faturaDestinoId = reader.IsDBNull(7) ? null : reader.GetInt64(7);
            acrescimoTransferido = reader.IsDBNull(8) ? 0m : reader.GetDecimal(8);
        }

        if (status != "Confirmada")
            return (ResultadoCancelamento.JaCancelada, null);

        var agoraUtc = DateTime.UtcNow;
        if (agoraUtc >= inicioUtc)
            return (ResultadoCancelamento.UtilizacaoIniciada, null);

        string? statusFatura = null;
        if (statusCobranca == "Transferida")
        {
            if (!faturaDestinoId.HasValue)
                throw new InvalidOperationException(
                    "Cobrança transferida sem fatura de destino.");
            using var consultarFatura = new MySqlCommand("""
                SELECT status FROM fatura
                WHERE id=@fatura AND associacao_id=@associacao FOR UPDATE
                """, conexao, transacao);
            consultarFatura.Parameters.AddWithValue("@fatura", faturaDestinoId.Value);
            consultarFatura.Parameters.AddWithValue("@associacao", associacaoId);
            statusFatura = await consultarFatura.ExecuteScalarAsync(ct) as string;
            if (statusFatura is not ("EmPreparacao" or "EmAberto" or "QuitadaSimulada"))
                throw new InvalidOperationException(
                    "Fatura de transferência indisponível para cancelamento.");
        }

        var multa = tipo == "SalaoFestas" &&
            agoraUtc > inicioUtc.AddHours(-24)
            ? Math.Min(50m, valorOriginal)
            : 0m;

        var credito = statusCobranca switch
        {
            "QuitadaSimulada" => valorOriginal - multa,
            "Transferida" => valorOriginal + acrescimoTransferido - multa,
            _ => 0m
        };
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
                WHERE reserva_id=@reserva
                  AND status IN ('EmAberto','QuitadaSimulada','Transferida')
                """, conexao, transacao);
            encerrar.Parameters.AddWithValue("@reserva", reservaId);
            if (await encerrar.ExecuteNonQueryAsync(ct) != 1)
                throw new InvalidOperationException("Cobrança modificada durante o cancelamento.");
        }

        using (var registrar = new MySqlCommand("""
            INSERT INTO cancelamento_reserva
                (reserva_id, usuario_responsavel_id, cancelada_em_utc,
                 valor_original, situacao_original_cobranca,
                 valor_acrescimo_transferido, multa_simulada, credito_simulado_pendente,
                 situacao_financeira)
            VALUES (@reserva, @usuario, UTC_TIMESTAMP(6),
                    @original, @cobrancaAnterior, @acrescimo, @multa, @credito, @situacao)
            """, conexao, transacao))
        {
            registrar.Parameters.AddWithValue("@reserva", reservaId);
            registrar.Parameters.AddWithValue("@usuario", usuarioId);
            registrar.Parameters.AddWithValue("@original", valorOriginal);
            registrar.Parameters.AddWithValue("@cobrancaAnterior", statusCobranca);
            registrar.Parameters.AddWithValue("@acrescimo",
                statusCobranca == "Transferida" ? acrescimoTransferido : 0m);
            registrar.Parameters.AddWithValue("@multa", multa);
            registrar.Parameters.AddWithValue("@credito", credito);
            registrar.Parameters.AddWithValue("@situacao", situacao);
            await registrar.ExecuteNonQueryAsync(ct);
        }

        if (credito > 0)
        {
            long cancelamentoId;
            using (var buscarId = new MySqlCommand("""
                SELECT id FROM cancelamento_reserva WHERE reserva_id=@reserva
                """, conexao, transacao))
            {
                buscarId.Parameters.AddWithValue("@reserva", reservaId);
                cancelamentoId = Convert.ToInt64(await buscarId.ExecuteScalarAsync(ct));
            }
            var hoje = RegraInadimplencia.DataLocal(agoraUtc, fusoOrigem);
            var proxima = new DateTime(hoje.Year, hoje.Month, 1).AddMonths(1);
            var creditoId = await CreditoFinanceiroServico.RegistrarAsync(conexao,
                transacao, associacaoId, cancelamentoId, credito,
                proxima, usuarioId, ct);

            if (statusCobranca == "Transferida" &&
                statusFatura is ("EmPreparacao" or "EmAberto"))
            {
                // Estorno na própria fatura aberta, sem remover o histórico
                // de principal e acréscimo. A multa permanece retida.
                await CreditoFinanceiroServico.AplicarAsync(conexao,
                    transacao, associacaoId, faturaDestinoId!.Value, usuarioId,
                    ct, creditoId);
            }
            else
            {
                await CreditoFinanceiroServico.AplicarNaProximaFaturaAsync(
                    conexao, transacao, associacaoId, proxima, usuarioId, ct);
            }

            // A resposta deve indicar apenas o saldo futuro ainda disponível,
            // não o valor já compensado na fatura aberta.
            using var consultarSaldo = new MySqlCommand("""
                SELECT c.saldo_disponivel, cr.situacao_financeira
                FROM credito_financeiro c
                JOIN cancelamento_reserva cr ON cr.id=c.cancelamento_reserva_id
                WHERE cr.id=@cancelamento
                """, conexao, transacao);
            consultarSaldo.Parameters.AddWithValue("@cancelamento", cancelamentoId);
            await using var leitor = await consultarSaldo.ExecuteReaderAsync(ct);
            if (await leitor.ReadAsync(ct))
            {
                credito = leitor.GetDecimal(0);
                situacao = leitor.GetString(1);
            }
        }

        if (cobrancaId.HasValue && statusCobranca == "Transferida")
        {
            using var lancarEstorno = new MySqlCommand("""
                INSERT INTO lancamento_financeiro
                    (cobranca_reserva_id, unidade_referencia_id, tipo,
                     valor, usuario_responsavel_id, observacao)
                VALUES (@cobranca, @unidade, 'CancelamentoLocacaoTransferida',
                        @valor, @usuario,
                        'Cancelamento simulado: principal e 10% revertidos; multa retida')
                """, conexao, transacao);
            lancarEstorno.Parameters.AddWithValue("@cobranca", cobrancaId.Value);
            lancarEstorno.Parameters.AddWithValue("@unidade", unidadeId);
            lancarEstorno.Parameters.AddWithValue("@valor",
                -(valorOriginal + acrescimoTransferido - multa));
            lancarEstorno.Parameters.AddWithValue("@usuario", usuarioId);
            await lancarEstorno.ExecuteNonQueryAsync(ct);
        }

        await LogAuditoria.RegistrarAsync(conexao, transacao, usuarioId,
            unidadeId, "Cancelar", "Reserva", reservaId,
            $"Reserva cancelada; multa simulada: {multa:F2}; situação: {situacao}.", ct);

        await transacao.CommitAsync(ct);
        return (ResultadoCancelamento.Concluido,
            new CancelamentoReservaResposta(reservaId, multa, credito, situacao));
    }
}
