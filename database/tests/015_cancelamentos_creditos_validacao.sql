-- CLICKCLUB - C4/E5 - verificação SOMENTE LEITURA.
-- Após aplicar 011 e executar os cenários na Web, rodar como usuário
-- autorizado no MySQL Workbench. Não apaga nem modifica registros.
USE clickclub_db;

-- A) Cancelamentos mais recentes; principal, acréscimo, multa e saldo futuro.
SELECT c.id AS cancelamento_id, c.reserva_id, c.situacao_original_cobranca,
       c.valor_original, c.valor_acrescimo_transferido,
       c.multa_simulada, c.credito_simulado_pendente,
       c.situacao_financeira, f.id AS credito_id,
       f.valor_original AS credito_total, f.saldo_disponivel,
       f.competencia_minima
FROM cancelamento_reserva c
LEFT JOIN credito_financeiro f ON f.cancelamento_reserva_id=c.id
ORDER BY c.id DESC LIMIT 30;

-- B) Faturas: não devem conter total abaixo de zero.
SELECT f.id, f.competencia, f.status, f.valor_total,
       a.matricula, un.nome AS clube_origem
FROM fatura f
JOIN associacao a ON a.id=f.associacao_id
JOIN unidade un ON un.id=f.unidade_origem_id
ORDER BY f.id DESC LIMIT 30;

-- C) Cada crédito aplicado tem item negativo em fatura elegível.
SELECT i.fatura_id, fa.competencia, fa.status, i.id AS item_id,
       i.valor AS desconto, i.credito_financeiro_id, c.saldo_disponivel
FROM item_fatura i
JOIN fatura fa ON fa.id=i.fatura_id
JOIN credito_financeiro c ON c.id=i.credito_financeiro_id
WHERE i.tipo='Credito'
ORDER BY i.id DESC LIMIT 30;

-- D) Invariantes: as consultas abaixo devem retornar ZERO linhas.
SELECT id, valor_total FROM fatura WHERE valor_total<0;
SELECT id, valor_original, saldo_disponivel
FROM credito_financeiro
WHERE saldo_disponivel<0 OR saldo_disponivel>valor_original;
SELECT fatura_id, credito_financeiro_id, COUNT(*) AS duplicados
FROM item_fatura
WHERE tipo='Credito'
GROUP BY fatura_id, credito_financeiro_id HAVING COUNT(*)>1;

-- E) Itens de cada reserva transferida permanecem no histórico;
-- o estorno deve aparecer como crédito negativo, não como novo débito.
SELECT i.reserva_id, i.fatura_id, i.tipo, i.valor,
       fa.status AS situacao_fatura
FROM item_fatura i
JOIN fatura fa ON fa.id=i.fatura_id
WHERE i.reserva_id IS NOT NULL
ORDER BY i.reserva_id DESC, i.id DESC LIMIT 50;
