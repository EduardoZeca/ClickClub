-- ClickClub - Componente 4 / Etapa 4: verificações SOMENTE DE LEITURA.
-- Execute depois de testar quitação avulsa e transferência, sem alterar dados.
USE clickclub_db;

-- 1. Visão das decisões e do destino das cobranças simuladas.
SELECT c.id AS cobranca_id, c.reserva_id, c.status, c.modalidade,
       c.valor_original, c.percentual_acrescimo, c.valor_acrescimo,
       c.competencia_destino, c.fatura_destino_id,
       f.status AS status_fatura, f.valor_total AS total_fatura
FROM cobranca_reserva c LEFT JOIN fatura f ON f.id=c.fatura_destino_id
ORDER BY c.id DESC;

-- 2. Deve retornar ZERO linhas: transferência sem os dois itens,
-- valor dos itens incompatível com a cobrança, ou fatura indevida.
SELECT c.id, c.reserva_id, c.fatura_destino_id,
       SUM(CASE WHEN i.tipo='LocacaoPrincipal' THEN 1 ELSE 0 END) AS principais,
       SUM(CASE WHEN i.tipo='AcrescimoTransferencia' THEN 1 ELSE 0 END) AS acrescimos
FROM cobranca_reserva c
JOIN reserva r ON r.id=c.reserva_id
LEFT JOIN item_fatura i ON i.reserva_id=r.id
WHERE c.status='Transferida'
GROUP BY c.id, c.reserva_id, c.fatura_destino_id,
         c.valor_original, c.valor_acrescimo
HAVING principais <> 1 OR acrescimos <> 1
    OR SUM(CASE WHEN i.tipo='LocacaoPrincipal' AND
                      i.fatura_id=c.fatura_destino_id AND
                      i.valor=c.valor_original THEN 1 ELSE 0 END) <> 1
    OR SUM(CASE WHEN i.tipo='AcrescimoTransferencia' AND
                      i.fatura_id=c.fatura_destino_id AND
                      i.valor=c.valor_acrescimo THEN 1 ELSE 0 END) <> 1;

-- 3. Deve retornar ZERO linhas: transferência com fatura de outra
-- associação, competência diferente ou sem acréscimo de 10%.
SELECT c.id, c.reserva_id, c.fatura_destino_id
FROM cobranca_reserva c
JOIN reserva r ON r.id=c.reserva_id
LEFT JOIN fatura f ON f.id=c.fatura_destino_id
WHERE c.status='Transferida' AND (
    f.id IS NULL OR f.associacao_id<>r.associacao_id
    OR f.competencia<>c.competencia_destino
    OR c.modalidade<>'Mensalidade'
    OR c.percentual_acrescimo<>10.00
    OR c.valor_acrescimo<>ROUND(c.valor_original*0.10, 2));

-- 4. Deve retornar ZERO linhas: cobrança não transferida com destino,
-- ou cobrança avulsa quitada com modalidade incorreta.
SELECT id, status, modalidade, fatura_destino_id, competencia_destino
FROM cobranca_reserva
WHERE (status<>'Transferida' AND
       (fatura_destino_id IS NOT NULL OR competencia_destino IS NOT NULL))
   OR (status='QuitadaSimulada' AND modalidade<>'Avulsa');

-- 5. Deve retornar ZERO linhas: total da fatura difere da soma dos itens.
SELECT f.id, f.valor_total, COALESCE(SUM(i.valor),0) AS soma_itens
FROM fatura f LEFT JOIN item_fatura i ON i.fatura_id=f.id
GROUP BY f.id, f.valor_total
HAVING ROUND(f.valor_total-COALESCE(SUM(i.valor),0),2) <> 0;

-- 6. Deve retornar ZERO linhas: quitação avulsa repetida na cobrança.
SELECT cobranca_reserva_id, COUNT(*) AS quitacoes
FROM lancamento_financeiro
WHERE tipo='QuitacaoLocacaoAvulsa'
GROUP BY cobranca_reserva_id HAVING COUNT(*) > 1;

-- 7. Deve retornar ZERO linhas: item de locação acrescentado após
-- o registro de quitação de uma fatura.
SELECT i.id, i.fatura_id, i.criado_em_utc, f.quitada_em_utc
FROM item_fatura i JOIN fatura f ON f.id=i.fatura_id
WHERE f.status='QuitadaSimulada' AND
      i.criado_em_utc > f.quitada_em_utc AND
      i.tipo IN ('LocacaoPrincipal','AcrescimoTransferencia');

-- 8. Lançamentos relacionados às cobranças de reserva.
SELECT l.id, l.cobranca_reserva_id, l.fatura_id,
       l.item_fatura_id, l.tipo, l.valor, l.data_hora_utc
FROM lancamento_financeiro l
WHERE l.cobranca_reserva_id IS NOT NULL
ORDER BY l.id DESC;
