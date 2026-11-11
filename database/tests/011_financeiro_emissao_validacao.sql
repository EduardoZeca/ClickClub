-- CLICKCLUB - Componente 4 / Etapa 2 (somente consultas de leitura).
-- Rodar APÓS emitir faturas de teste na Web. Não altera dados.
USE clickclub_db;

-- 1. Faturas emitidas: competência e vencimento sempre no mesmo mês.
SELECT f.id, a.matricula, f.competencia, f.data_vencimento,
       f.valor_total, f.status, p.valor_mensal AS mensalidade_politica
FROM fatura f
JOIN associacao a ON a.id=f.associacao_id
LEFT JOIN politica_financeira p ON p.id=f.politica_financeira_id
ORDER BY f.id DESC;

-- 2. Deve retornar ZERO linhas: emissão duplicada para a mesma associação/mês.
SELECT associacao_id, tipo, competencia, COUNT(*) AS quantidade
FROM fatura
GROUP BY associacao_id, tipo, competencia
HAVING COUNT(*) > 1;

-- 3. Deve retornar ZERO linhas: mensalidade-base duplicada na mesma fatura.
SELECT fatura_id, COUNT(*) AS quantidade
FROM item_fatura
WHERE tipo='MensalidadeBase'
GROUP BY fatura_id
HAVING COUNT(*) > 1;

-- 4. Deve retornar ZERO linhas: fatura emitida sem mensalidade-base.
SELECT f.id, f.status
FROM fatura f
WHERE f.status='EmAberto' AND NOT EXISTS (
    SELECT 1 FROM item_fatura i
    WHERE i.fatura_id=f.id AND i.tipo='MensalidadeBase'
);

-- 5. Deve retornar ZERO linhas: total diferente da soma dos itens.
SELECT f.id, f.valor_total, COALESCE(SUM(i.valor), 0) AS soma_itens
FROM fatura f
LEFT JOIN item_fatura i ON i.fatura_id=f.id
GROUP BY f.id, f.valor_total
HAVING ROUND(f.valor_total - COALESCE(SUM(i.valor), 0), 2) <> 0;

-- 6. Deve retornar ZERO linhas: fatura anterior à primeira competência
-- ou vencimento fora do dia 10 da própria competência.
SELECT f.id, a.data_inicio, f.competencia, f.data_vencimento
FROM fatura f
JOIN associacao a ON a.id=f.associacao_id
WHERE f.competencia < DATE_ADD(
    DATE_SUB(a.data_inicio, INTERVAL (DAYOFMONTH(a.data_inicio) - 1) DAY),
    INTERVAL 1 MONTH)
  OR f.data_vencimento <> DATE_ADD(f.competencia, INTERVAL 9 DAY);

-- 7. Rastreio financeiro da emissão, sem quitação nem multa nesta etapa.
SELECT l.id, l.fatura_id, l.item_fatura_id, l.tipo,
       l.valor, l.data_hora_utc
FROM lancamento_financeiro l
WHERE l.tipo='EmissaoMensalidade'
ORDER BY l.id DESC;
