-- CLICKCLUB - Componente 4 / Etapa 1 - validacao SOMENTE DE LEITURA.
-- Executar APOS 010_financeiro_base.sql, sem alterar dados.
-- Esperado: 5 tabelas novas, 1 FK da cobranca e indices de unicidade.
USE clickclub_db;

-- Deve listar as cinco tabelas com resultado EXISTE.
SELECT nome.tabela,
       IF(t.TABLE_NAME IS NULL, 'AUSENTE', 'EXISTE') AS resultado
FROM (
    SELECT 'politica_financeira' AS tabela
    UNION ALL SELECT 'fatura'
    UNION ALL SELECT 'credito_financeiro'
    UNION ALL SELECT 'item_fatura'
    UNION ALL SELECT 'lancamento_financeiro'
) AS nome
LEFT JOIN information_schema.TABLES t
    ON t.TABLE_SCHEMA = DATABASE() AND t.TABLE_NAME = nome.tabela
ORDER BY nome.tabela;

-- Deve retornar a chave estrangeira fk_cobranca_reserva_fatura_destino.
SELECT CONSTRAINT_NAME, TABLE_NAME, REFERENCED_TABLE_NAME
FROM information_schema.REFERENTIAL_CONSTRAINTS
WHERE CONSTRAINT_SCHEMA = DATABASE()
  AND TABLE_NAME = 'cobranca_reserva'
  AND CONSTRAINT_NAME = 'fk_cobranca_reserva_fatura_destino';

-- Deve listar os indices de unicidade das faturas, itens e creditos.
SELECT TABLE_NAME, INDEX_NAME,
       GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX SEPARATOR ', ') AS colunas
FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA = DATABASE() AND NON_UNIQUE = 0
  AND INDEX_NAME IN (
    'uq_fatura_associacao_competencia',
    'uq_item_fatura_base', 'uq_item_fatura_multa_atraso',
    'uq_item_fatura_reserva_tipo', 'uq_item_fatura_credito',
    'uq_credito_financeiro_cancelamento'
  )
GROUP BY TABLE_NAME, INDEX_NAME
ORDER BY TABLE_NAME, INDEX_NAME;

-- Contagem de registros existentes: anote os valores ANTES e DEPOIS do 010.
-- O script 010 nao insere nem modifica reservas ou cobrancas anteriores.
SELECT 'reserva' AS origem, COUNT(*) AS registros FROM reserva
UNION ALL
SELECT 'cobranca_reserva', COUNT(*) FROM cobranca_reserva
UNION ALL
SELECT 'cancelamento_reserva', COUNT(*) FROM cancelamento_reserva;

-- Esperado na Etapa 1: nenhuma fatura/credito/lancamento gerado pelo script.
SELECT (SELECT COUNT(*) FROM fatura) AS faturas,
       (SELECT COUNT(*) FROM item_fatura) AS itens,
       (SELECT COUNT(*) FROM credito_financeiro) AS creditos,
       (SELECT COUNT(*) FROM lancamento_financeiro) AS lancamentos;
