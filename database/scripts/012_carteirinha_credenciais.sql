-- CLICKCLUB - Componente 5 / Etapa 1
-- Executar UMA VEZ, após 011_cancelamentos_creditos.sql, com usuário
-- administrativo do MySQL 8.4 e após backup do clickclub_db.
-- Não executar como clickclub_api. DDL faz commits implícitos.
-- Não altera registros das tabelas existentes.

USE clickclub_db;

CREATE TABLE credencial_carteirinha (
    id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    pessoa_id BIGINT UNSIGNED NOT NULL,
    associacao_id BIGINT UNSIGNED NOT NULL,
    identificador_publico CHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    emitida_em_utc DATETIME(6) NOT NULL,
    valida_ate_utc DATETIME(6) NOT NULL,
    revogada_em_utc DATETIME(6) NULL,
    versao INT UNSIGNED NOT NULL,

    -- Uma única credencial não revogada por pessoa. Credenciais expiradas
    -- são revogadas na próxima emissão/renovação, preservando o histórico.
    pessoa_credencial_atual BIGINT UNSIGNED
        GENERATED ALWAYS AS (
            IF(revogada_em_utc IS NULL, pessoa_id, NULL)
        ) STORED,

    UNIQUE KEY uq_carteirinha_identificador (identificador_publico),
    UNIQUE KEY uq_carteirinha_atual (pessoa_credencial_atual),
    UNIQUE KEY uq_carteirinha_pessoa_versao (pessoa_id, versao),
    INDEX ix_carteirinha_associacao (associacao_id),
    CONSTRAINT fk_carteirinha_pessoa
        FOREIGN KEY (pessoa_id) REFERENCES pessoa(id),
    CONSTRAINT fk_carteirinha_associacao
        FOREIGN KEY (associacao_id) REFERENCES associacao(id),
    CONSTRAINT ck_carteirinha_prazo CHECK (valida_ate_utc > emitida_em_utc),
    CONSTRAINT ck_carteirinha_versao CHECK (versao > 0)
) ENGINE=InnoDB;

-- O QR não é armazenado nem registrado em logs. A API assina um payload
-- com o identificador público, versão e instantes UTC, usando chave privada
-- ECDSA P-256 mantida fora do Git.
-- A tabela de registro de acessos será criada na Etapa 2.
