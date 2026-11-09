-- =============================================================
-- CLICKCLUB - Componente 2 / Etapa 7
-- Script 003 - Consulta privada de solicitações de adesão
-- Alvo: MySQL 8.4
-- =============================================================
-- Executar UMA VEZ, depois do 002, com usuário administrativo.
-- Fazer backup antes. Não executar com clickclub_api (sem DDL).
-- Guarda somente o SHA-256 do código de consulta gerado pela API;
-- o código original é entregue ao solicitante apenas uma vez.

USE clickclub_db;

ALTER TABLE solicitacao_adesao
    ADD COLUMN codigo_consulta_hash CHAR(64) NULL AFTER pessoa_id,
    ADD UNIQUE KEY uq_solicitacao_codigo_consulta (codigo_consulta_hash);

-- Registros anteriores continuam válidos. Se houver solicitações
-- anteriores, seu codigo_consulta_hash permanecerá NULL.
