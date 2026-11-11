-- CLICKCLUB - Componente 6 / Etapa 2
-- Executar UMA VEZ após 014_atividades_turmas.sql, com backup prévio.
-- Usar uma conta MySQL administrativa, nunca clickclub_api.
USE clickclub_db;

CREATE TABLE inscricao_turma (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    turma_id BIGINT UNSIGNED NOT NULL,
    pessoa_id BIGINT UNSIGNED NOT NULL,
    status ENUM('Confirmada','EmEspera','Cancelada') NOT NULL,
    inscrita_em_utc DATETIME(6) NOT NULL,
    cancelada_em_utc DATETIME(6) NULL,

    -- Impede duas inscrições simultaneamente ativas na mesma turma.
    -- Permite histórico de cancelamentos e posterior nova inscrição.
    pessoa_ativa_id BIGINT UNSIGNED GENERATED ALWAYS AS (
        IF(status IN ('Confirmada','EmEspera'), pessoa_id, NULL)
    ) STORED,
    UNIQUE KEY uq_inscricao_turma_ativa (turma_id, pessoa_ativa_id),
    INDEX ix_inscricao_turma_fila (turma_id, status, inscrita_em_utc, id),
    INDEX ix_inscricao_pessoa_status (pessoa_id, status, turma_id),
    CONSTRAINT fk_inscricao_turma_turma FOREIGN KEY (turma_id) REFERENCES turma(id),
    CONSTRAINT fk_inscricao_turma_pessoa FOREIGN KEY (pessoa_id) REFERENCES pessoa(id)
) ENGINE=InnoDB;

-- PosicaoEspera é calculada por (inscrita_em_utc, id) ao consultar,
-- sem campo duplicado que exigiria renumerar toda a fila após alterações.
