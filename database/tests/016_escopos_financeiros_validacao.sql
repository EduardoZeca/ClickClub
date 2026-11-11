-- CLICKCLUB C4/E6 - consultas SOMENTE LEITURA para preparar a verificação
-- de permissões por API/Web. Não concede autorização nem cria dados.
USE clickclub_db;

-- 1. Confira a quais clubes os Tesoureiros de teste pertencem.
SELECT pu.usuario_id, us.login, pu.unidade_id, un.nome AS clube
FROM papel_usuario pu
JOIN usuario us ON us.id=pu.usuario_id
JOIN unidade un ON un.id=pu.unidade_id
WHERE pu.papel='Tesoureiro'
ORDER BY us.login, un.nome;

-- 2. Faturas por clube de origem (somente esse clube pode ter Tesoureiro
-- habilitado a ver e quitar a mensalidade).
SELECT f.id AS fatura_id, f.unidade_origem_id,
       un.nome AS clube_origem, f.competencia,
       f.status, f.valor_total
FROM fatura f
JOIN unidade un ON un.id=f.unidade_origem_id
ORDER BY f.id DESC LIMIT 40;

-- 3. Locações por clube prestador, com origem da associação.
SELECT r.id AS reserva_id, prestadora.id AS prestadora_id,
       prestadora.nome AS clube_prestador,
       origem.id AS origem_id, origem.nome AS clube_origem,
       c.status AS status_cobranca, c.modalidade,
       c.valor_original, c.valor_acrescimo, c.competencia_destino
FROM cobranca_reserva c
JOIN reserva r ON r.id=c.reserva_id
JOIN espaco e ON e.id=r.espaco_id
JOIN unidade prestadora ON prestadora.id=e.unidade_id
JOIN associacao a ON a.id=r.associacao_id
JOIN unidade origem ON origem.id=a.unidade_origem_id
ORDER BY r.id DESC LIMIT 40;

-- 4. Transferências interclubes para usar nos testes HTTP 404/200.
SELECT r.id AS reserva_id, c.fatura_destino_id,
       prestadora.nome AS clube_prestador,
       origem.nome AS clube_da_fatura
FROM cobranca_reserva c
JOIN reserva r ON r.id=c.reserva_id
JOIN espaco e ON e.id=r.espaco_id
JOIN unidade prestadora ON prestadora.id=e.unidade_id
JOIN associacao a ON a.id=r.associacao_id
JOIN unidade origem ON origem.id=a.unidade_origem_id
WHERE c.fatura_destino_id IS NOT NULL
  AND prestadora.id<>origem.id
ORDER BY r.id DESC LIMIT 30;
