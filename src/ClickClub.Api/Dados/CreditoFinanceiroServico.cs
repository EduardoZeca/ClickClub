using MySqlConnector;

namespace ClickClub.Api.Dados;

// Utilizado somente dentro de transações que já bloquearam a associação.
// Preserva faturas quitadas e nunca permite saldo negativo ou crédito repetido.
internal static class CreditoFinanceiroServico
{
    public static async Task<long> RegistrarAsync(MySqlConnection conexao,
        MySqlTransaction transacao, long associacaoId, long cancelamentoId,
        decimal valor, DateTime competenciaMinima, long autorId,
        CancellationToken ct)
    {
        if (valor <= 0) return 0;
        using var inserir = new MySqlCommand("""
            INSERT INTO credito_financeiro
                (associacao_id, cancelamento_reserva_id, valor_original,
                 saldo_disponivel, competencia_minima, criado_por_usuario_id)
            VALUES (@associacao, @cancelamento, @valor, @valor,
                    @competencia, @usuario)
            """, conexao, transacao);
        inserir.Parameters.AddWithValue("@associacao", associacaoId);
        inserir.Parameters.AddWithValue("@cancelamento", cancelamentoId);
        inserir.Parameters.AddWithValue("@valor", valor);
        inserir.Parameters.AddWithValue("@competencia", competenciaMinima);
        inserir.Parameters.AddWithValue("@usuario", autorId);
        await inserir.ExecuteNonQueryAsync(ct);
        return inserir.LastInsertedId;
    }

    // Créditos de cancelamentos avulsos/quitados só incidem em mensalidades
    // futuras do clube de origem. Não gera fatura vazia artificialmente.
    public static async Task AplicarNaProximaFaturaAsync(
        MySqlConnection conexao, MySqlTransaction transacao,
        long associacaoId, DateTime primeiraCompetencia,
        long autorId, CancellationToken ct)
    {
        long? faturaId;
        using (var buscar = new MySqlCommand("""
            SELECT id FROM fatura
            WHERE associacao_id=@associacao AND tipo='Mensalidade'
              AND competencia>=@competencia
              AND status IN ('EmPreparacao','EmAberto')
            ORDER BY competencia, id LIMIT 1 FOR UPDATE
            """, conexao, transacao))
        {
            buscar.Parameters.AddWithValue("@associacao", associacaoId);
            buscar.Parameters.AddWithValue("@competencia", primeiraCompetencia);
            var resposta = await buscar.ExecuteScalarAsync(ct);
            faturaId = resposta is null ? null : Convert.ToInt64(resposta);
        }

        if (faturaId.HasValue)
            await AplicarAsync(conexao, transacao, associacaoId,
                faturaId.Value, autorId, ct);
    }

    public static async Task AplicarAsync(MySqlConnection conexao,
        MySqlTransaction transacao, long associacaoId, long faturaId,
        long autorId, CancellationToken ct, long? creditoDaFaturaCancelada = null)
    {
        decimal total;
        long unidadeOrigem;
        DateTime competencia;
        using (var consultar = new MySqlCommand("""
            SELECT valor_total, unidade_origem_id, competencia FROM fatura
            WHERE id=@fatura AND associacao_id=@associacao
              AND status IN ('EmPreparacao','EmAberto') FOR UPDATE
            """, conexao, transacao))
        {
            consultar.Parameters.AddWithValue("@fatura", faturaId);
            consultar.Parameters.AddWithValue("@associacao", associacaoId);
            await using var reader = await consultar.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return;
            total = reader.GetDecimal(0);
            unidadeOrigem = reader.GetInt64(1);
            competencia = reader.GetDateTime(2);
        }
        if (total <= 0) return;

        var creditos = new List<(long Id, long? CancelamentoId, decimal Saldo)>();
        using (var buscar = new MySqlCommand("""
            SELECT id, cancelamento_reserva_id, saldo_disponivel
            FROM credito_financeiro
            WHERE associacao_id=@associacao AND saldo_disponivel>0
              AND (competencia_minima<=@competencia OR id=@excecao)
            ORDER BY criado_em_utc, id FOR UPDATE
            """, conexao, transacao))
        {
            buscar.Parameters.AddWithValue("@associacao", associacaoId);
            buscar.Parameters.AddWithValue("@competencia", competencia);
            buscar.Parameters.AddWithValue("@excecao",
                creditoDaFaturaCancelada.HasValue
                    ? (object)creditoDaFaturaCancelada.Value : DBNull.Value);
            await using var reader = await buscar.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                creditos.Add((reader.GetInt64(0),
                    reader.IsDBNull(1) ? null : reader.GetInt64(1),
                    reader.GetDecimal(2)));
        }

        foreach (var credito in creditos)
        {
            if (total <= 0) break;
            var abatimento = Math.Min(total, credito.Saldo);
            if (abatimento <= 0) continue;

            long itemId;
            using (var existente = new MySqlCommand("""
                SELECT id FROM item_fatura
                WHERE fatura_id=@fatura AND credito_financeiro_id=@credito
                FOR UPDATE
                """, conexao, transacao))
            {
                existente.Parameters.AddWithValue("@fatura", faturaId);
                existente.Parameters.AddWithValue("@credito", credito.Id);
                var valor = await existente.ExecuteScalarAsync(ct);
                itemId = valor is null ? 0 : Convert.ToInt64(valor);
            }

            if (itemId == 0)
            {
                using var inserir = new MySqlCommand("""
                    INSERT INTO item_fatura
                        (fatura_id, tipo, descricao, valor,
                         origem_unidade_id, credito_financeiro_id)
                    VALUES (@fatura, 'Credito',
                            'Compensação de crédito de cancelamento',
                            @valor, @unidade, @credito)
                    """, conexao, transacao);
                inserir.Parameters.AddWithValue("@fatura", faturaId);
                inserir.Parameters.AddWithValue("@valor", -abatimento);
                inserir.Parameters.AddWithValue("@unidade", unidadeOrigem);
                inserir.Parameters.AddWithValue("@credito", credito.Id);
                await inserir.ExecuteNonQueryAsync(ct);
                itemId = inserir.LastInsertedId;
            }
            else
            {
                using var aumentar = new MySqlCommand("""
                    UPDATE item_fatura SET valor=valor-@valor
                    WHERE id=@item AND tipo='Credito'
                    """, conexao, transacao);
                aumentar.Parameters.AddWithValue("@item", itemId);
                aumentar.Parameters.AddWithValue("@valor", abatimento);
                if (await aumentar.ExecuteNonQueryAsync(ct) != 1)
                    throw new InvalidOperationException("Item de crédito inválido.");
            }

            using (var debitar = new MySqlCommand("""
                UPDATE credito_financeiro
                SET saldo_disponivel=saldo_disponivel-@valor
                WHERE id=@credito AND associacao_id=@associacao
                  AND saldo_disponivel>=@valor
                """, conexao, transacao))
            {
                debitar.Parameters.AddWithValue("@credito", credito.Id);
                debitar.Parameters.AddWithValue("@associacao", associacaoId);
                debitar.Parameters.AddWithValue("@valor", abatimento);
                if (await debitar.ExecuteNonQueryAsync(ct) != 1)
                    throw new InvalidOperationException("Saldo de crédito alterado.");
            }

            using (var abater = new MySqlCommand("""
                UPDATE fatura SET valor_total=valor_total-@valor
                WHERE id=@fatura AND status IN ('EmPreparacao','EmAberto')
                  AND valor_total>=@valor
                """, conexao, transacao))
            {
                abater.Parameters.AddWithValue("@fatura", faturaId);
                abater.Parameters.AddWithValue("@valor", abatimento);
                if (await abater.ExecuteNonQueryAsync(ct) != 1)
                    throw new InvalidOperationException("Saldo de fatura alterado.");
            }

            using (var lancar = new MySqlCommand("""
                INSERT INTO lancamento_financeiro
                    (fatura_id, item_fatura_id, unidade_referencia_id,
                     tipo, valor, usuario_responsavel_id, observacao)
                VALUES (@fatura, @item, @unidade,
                        'CompensacaoCredito', @valor, @usuario,
                        'Crédito simulado aplicado sem duplicidade')
                """, conexao, transacao))
            {
                lancar.Parameters.AddWithValue("@fatura", faturaId);
                lancar.Parameters.AddWithValue("@item", itemId);
                lancar.Parameters.AddWithValue("@unidade", unidadeOrigem);
                lancar.Parameters.AddWithValue("@valor", -abatimento);
                lancar.Parameters.AddWithValue("@usuario", autorId);
                await lancar.ExecuteNonQueryAsync(ct);
            }

            if (credito.CancelamentoId.HasValue)
            {
                var restante = credito.Saldo - abatimento;
                using var atualizarCancelamento = new MySqlCommand("""
                    UPDATE cancelamento_reserva
                    SET credito_simulado_pendente=@restante,
                        situacao_financeira=@situacao
                    WHERE id=@cancelamento
                    """, conexao, transacao);
                atualizarCancelamento.Parameters.AddWithValue("@restante", restante);
                atualizarCancelamento.Parameters.AddWithValue("@situacao",
                    restante == 0 ? "CreditoCompensado" : "CreditoParcial");
                atualizarCancelamento.Parameters.AddWithValue("@cancelamento",
                    credito.CancelamentoId.Value);
                await atualizarCancelamento.ExecuteNonQueryAsync(ct);
            }
            total -= abatimento;
        }
    }
}
