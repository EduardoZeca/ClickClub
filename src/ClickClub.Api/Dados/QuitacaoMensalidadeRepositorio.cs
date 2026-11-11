using ClickClub.Api.Contratos;
using MySqlConnector;

namespace ClickClub.Api.Dados;

// Quitação exclusivamente simulada; não processa pagamentos reais.
public sealed class QuitacaoMensalidadeRepositorio(IConfiguration configuration)
{
    private readonly string _connectionString = configuration
        .GetConnectionString("ClickClub")
        ?? throw new InvalidOperationException("Conexão MySQL não configurada.");

    public async Task<QuitacaoMensalidadeResposta?> QuitarAsync(
        long faturaId, long usuarioId, bool administrador,
        bool tesoureiro, CancellationToken ct)
    {
        await using var conexao = new MySqlConnection(_connectionString);
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);

        long associacaoId;
        using (var procurar = new MySqlCommand("""
            SELECT associacao_id FROM fatura WHERE id=@fatura
            """, conexao, transacao))
        {
            procurar.Parameters.AddWithValue("@fatura", faturaId);
            var resposta = await procurar.ExecuteScalarAsync(ct);
            if (resposta is null) return null;
            associacaoId = Convert.ToInt64(resposta);
        }

        // Todas as mutações financeiras da mesma associação bloqueiam
        // a associação antes da fatura, assim como a emissão e a reserva.
        long unidadeId;
        string fuso;
        long titularPessoaId;
        using (var associacao = new MySqlCommand("""
            SELECT a.unidade_origem_id, u.fuso_horario, a.titular_pessoa_id
            FROM associacao a JOIN unidade u ON u.id=a.unidade_origem_id
            WHERE a.id=@associacao FOR UPDATE
            """, conexao, transacao))
        {
            associacao.Parameters.AddWithValue("@associacao", associacaoId);
            await using var leitor = await associacao.ExecuteReaderAsync(ct);
            if (!await leitor.ReadAsync(ct)) return null;
            unidadeId = leitor.GetInt64(0);
            fuso = leitor.GetString(1);
            titularPessoaId = leitor.GetInt64(2);
        }

        if (!administrador)
        {
            using var verificar = new MySqlCommand("""
                SELECT EXISTS(SELECT 1 FROM usuario us
                    WHERE us.id=@usuario AND us.pessoa_id=@titular)
                    OR (@tesoureiro=TRUE AND EXISTS (
                        SELECT 1 FROM papel_usuario pu
                        WHERE pu.usuario_id=@usuario AND pu.papel='Tesoureiro'
                          AND pu.unidade_id=@unidade))
                """, conexao, transacao);
            verificar.Parameters.AddWithValue("@usuario", usuarioId);
            verificar.Parameters.AddWithValue("@titular", titularPessoaId);
            verificar.Parameters.AddWithValue("@tesoureiro", tesoureiro);
            verificar.Parameters.AddWithValue("@unidade", unidadeId);
            if (Convert.ToInt32(await verificar.ExecuteScalarAsync(ct)) != 1)
                return null;
        }

        string status;
        DateTime vencimento;
        decimal total;
        using (var fatura = new MySqlCommand("""
            SELECT status, data_vencimento, valor_total
            FROM fatura WHERE id=@fatura AND associacao_id=@associacao
            FOR UPDATE
            """, conexao, transacao))
        {
            fatura.Parameters.AddWithValue("@fatura", faturaId);
            fatura.Parameters.AddWithValue("@associacao", associacaoId);
            await using var leitor = await fatura.ExecuteReaderAsync(ct);
            if (!await leitor.ReadAsync(ct)) return null;
            status = leitor.GetString(0);
            vencimento = leitor.GetDateTime(1);
            total = leitor.GetDecimal(2);
        }

        if (status == "QuitadaSimulada")
            return new QuitacaoMensalidadeResposta(faturaId, true, total, 0m);
        if (status != "EmAberto")
            throw new InvalidOperationException(
                "Somente faturas emitidas e em aberto podem ser quitadas.");

        // Créditos gerados após a emissão e ainda disponíveis são compensados
        // antes do cálculo de eventual multa e da quitação simulada.
        await CreditoFinanceiroServico.AplicarAsync(conexao, transacao,
            associacaoId, faturaId, usuarioId, ct);
        using (var conferirTotal = new MySqlCommand("""
            SELECT valor_total FROM fatura WHERE id=@fatura
            """, conexao, transacao))
        {
            conferirTotal.Parameters.AddWithValue("@fatura", faturaId);
            total = Convert.ToDecimal(await conferirTotal.ExecuteScalarAsync(ct));
        }

        using var buscarMulta = new MySqlCommand("""
            SELECT EXISTS(SELECT 1 FROM item_fatura
                WHERE fatura_id=@fatura AND tipo='MultaAtraso')
            """, conexao, transacao);
        buscarMulta.Parameters.AddWithValue("@fatura", faturaId);
        var multaJaLancada = Convert.ToInt32(
            await buscarMulta.ExecuteScalarAsync(ct)) == 1;

        var hojeLocal = RegraInadimplencia.DataLocal(DateTime.UtcNow, fuso);
        var multa = RegraInadimplencia.MultaPendente(
            status, total, vencimento, hojeLocal, multaJaLancada);

        if (multa > 0)
        {
            using var item = new MySqlCommand("""
                INSERT INTO item_fatura
                    (fatura_id, tipo, descricao, valor, origem_unidade_id)
                VALUES (@fatura, 'MultaAtraso',
                        'Multa única de 2% por atraso na mensalidade',
                        @valor, @unidade)
                """, conexao, transacao);
            item.Parameters.AddWithValue("@fatura", faturaId);
            item.Parameters.AddWithValue("@valor", multa);
            item.Parameters.AddWithValue("@unidade", unidadeId);
            await item.ExecuteNonQueryAsync(ct);
            var itemId = item.LastInsertedId;

            using var lancarMulta = new MySqlCommand("""
                INSERT INTO lancamento_financeiro
                    (fatura_id, item_fatura_id, unidade_referencia_id,
                     tipo, valor, usuario_responsavel_id, observacao)
                VALUES (@fatura, @item, @unidade, 'MultaAtraso',
                        @valor, @usuario,
                        'Multa simulada única após o décimo dia de atraso')
                """, conexao, transacao);
            lancarMulta.Parameters.AddWithValue("@fatura", faturaId);
            lancarMulta.Parameters.AddWithValue("@item", itemId);
            lancarMulta.Parameters.AddWithValue("@unidade", unidadeId);
            lancarMulta.Parameters.AddWithValue("@valor", multa);
            lancarMulta.Parameters.AddWithValue("@usuario", usuarioId);
            await lancarMulta.ExecuteNonQueryAsync(ct);
            total += multa;
        }

        using (var quitar = new MySqlCommand("""
            UPDATE fatura SET valor_total=@total,
                status='QuitadaSimulada', quitada_em_utc=UTC_TIMESTAMP(6)
            WHERE id=@fatura AND status='EmAberto'
            """, conexao, transacao))
        {
            quitar.Parameters.AddWithValue("@fatura", faturaId);
            quitar.Parameters.AddWithValue("@total", total);
            if (await quitar.ExecuteNonQueryAsync(ct) != 1)
                throw new InvalidOperationException("Fatura modificada durante a quitação.");
        }

        using (var registrar = new MySqlCommand("""
            INSERT INTO lancamento_financeiro
                (fatura_id, unidade_referencia_id, tipo, valor,
                 usuario_responsavel_id, observacao)
            VALUES (@fatura, @unidade, 'QuitacaoMensalidade',
                    @valor, @usuario, 'Quitação integral exclusivamente simulada')
            """, conexao, transacao))
        {
            registrar.Parameters.AddWithValue("@fatura", faturaId);
            registrar.Parameters.AddWithValue("@unidade", unidadeId);
            registrar.Parameters.AddWithValue("@valor", -total);
            registrar.Parameters.AddWithValue("@usuario", usuarioId);
            await registrar.ExecuteNonQueryAsync(ct);
        }

        await LogAuditoria.RegistrarAsync(conexao, transacao,
            usuarioId, unidadeId, "Quitar", "Fatura", faturaId,
            "Quitação integral de mensalidade registrada em modo simulado.", ct);
        await transacao.CommitAsync(ct);
        return new QuitacaoMensalidadeResposta(faturaId, false, total, multa);
    }
}
