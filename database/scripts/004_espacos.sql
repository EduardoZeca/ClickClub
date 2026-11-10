-- CLICKCLUB - Componente 3, Etapa 1
-- Cadastro de espaços. Executar uma única vez após 001, 002 e 003.
-- Antes da execução, fazer backup. Usar conta administrativa MySQL.
USE clickclub_db;

CREATE TABLE IF NOT EXISTS espaco (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    unidade_id BIGINT UNSIGNED NOT NULL,
    nome VARCHAR(150) NOT NULL,
    tipo ENUM('SalaoFestas', 'Churrasqueira', 'Quadra') NOT NULL,
    capacidade INT UNSIGNED NOT NULL,
    taxa_base DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    ativo BOOLEAN NOT NULL DEFAULT TRUE,
    CONSTRAINT fk_espaco_unidade FOREIGN KEY (unidade_id)
        REFERENCES unidade(id),
    CONSTRAINT ck_espaco_capacidade CHECK (capacidade BETWEEN 1 AND 10000),
    CONSTRAINT ck_espaco_taxa CHECK (taxa_base >= 0),
    UNIQUE KEY uq_espaco_unidade_nome (unidade_id, nome),
    INDEX ix_espaco_unidade_ativo_tipo (unidade_id, ativo, tipo)
) ENGINE=InnoDB;

-- A criação de horários, manutenções e reservas será feita
-- nas próximas etapas do Componente 3, sem alterar esta migração.
