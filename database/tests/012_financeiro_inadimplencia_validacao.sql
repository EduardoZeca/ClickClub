-- ClickClub / Componente 4 / Etapa 3
-- SOMENTE LEITURA. Executar após testes de quitação no ambiente de teste.
USE clickclub_db;

-- 1. Conferir situações, datas, valores e multa persistida após quitação.
SELECT f.id, a.matricula, f.competencia, f.data_vencimento,
       f.status, f.valor_total, f.quitada_em_utc,
       COALESCE(SUM(CASE WHEN i.tipo='MultaAtraso'
           THEN i.valor ELSE 0 END), 0) AS multa_aplicada
FROM fatura f
JOIN associacao a ON a.id=f.associacao_id
LEFT JOIN item_fatura i ON i.fatura_id=f.id
GROUP BY f.id, a.matricula, f.competencia, f.data_vencimento,
         f.status, f.valor_total, f.quitada_em_utc
ORDER BY f.id DESC;

-- 2. Deve retornar ZERO linhas: duas multas para a mesma fatura.
SELECT fatura_id, COUNT(*) AS multas
FROM item_fatura
WHERE tipo='MultaAtraso'
GROUP BY fatura_id HAVING COUNT(*) > 1;

-- 3. Deve retornar ZERO linhas: mais de um lançamento de quitação por fatura.
SELECT fatura_id, COUNT(*) AS quitacoes
FROM lancamento_financeiro
WHERE tipo='QuitacaoMensalidade'
GROUP BY fatura_id HAVING COUNT(*) > 1;

-- 4. Deve retornar ZERO linhas: fatura quitada sem data de quitação.
SELECT id, status, quitada_em_utc FROM fatura
WHERE status='QuitadaSimulada' AND quitada_em_utc IS NULL;

-- 5. Deve retornar ZERO linhas: valor total difere da soma de itens.
SELECT f.id, f.valor_total, COALESCE(SUM(i.valor),0) AS soma_itens
FROM fatura f LEFT JOIN item_fatura i ON i.fatura_id=f.id
GROUP BY f.id, f.valor_total
HAVING ROUND(f.valor_total-COALESCE(SUM(i.valor),0), 2) <> 0;

-- 6. Deve retornar ZERO linhas: lançamentos de quitação que não
-- correspondem ao valor integral da fatura quitada.
SELECT l.fatura_id, l.valor AS lancamento, f.valor_total
FROM lancamento_financeiro l JOIN fatura f ON f.id=l.fatura_id
WHERE l.tipo='QuitacaoMensalidade' AND l.valor <> -f.valor_total;

-- 7. Conferir histórico de eventos simulados (sem dados bancários).
SELECT l.id, l.fatura_id, l.tipo, l.valor, l.data_hora_utc
FROM lancamento_financeiro l
WHERE l.tipo IN ('MultaAtraso','QuitacaoMensalidade')
ORDER BY l.id DESC;
