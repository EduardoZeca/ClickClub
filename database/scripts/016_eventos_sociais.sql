-- CLICKCLUB - Componente 6 / Etapa 3A
-- Executar UMA VEZ após 015_inscricoes_turmas.sql, com backup prévio.
USE clickclub_db;

CREATE TABLE evento_social (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    unidade_id BIGINT UNSIGNED NOT NULL,
    nome VARCHAR(150) NOT NULL,
    descricao VARCHAR(2000) NULL,
    inicio_utc DATETIME(6) NOT NULL,
    fim_utc DATETIME(6) NOT NULL,
    publico_alvo ENUM('SomenteAssociados','PublicoGeral') NOT NULL,
    tipo ENUM('Social','Torneio') NOT NULL DEFAULT 'Social',
    capacidade INT UNSIGNED NULL,
    status ENUM('Publicado','Encerrado','Cancelado') NOT NULL DEFAULT 'Publicado',
    INDEX ix_evento_clube_periodo (unidade_id, inicio_utc, status),
    INDEX ix_evento_publico_periodo (publico_alvo, inicio_utc, status),
    CONSTRAINT fk_evento_unidade FOREIGN KEY (unidade_id) REFERENCES unidade(id),
    CONSTRAINT ck_evento_periodo CHECK (fim_utc > inicio_utc),
    CONSTRAINT ck_evento_capacidade CHECK (capacidade IS NULL OR capacidade BETWEEN 1 AND 10000),
    CONSTRAINT ck_torneio_capacidade CHECK (tipo<>'Torneio' OR capacidade IS NOT NULL)
) ENGINE=InnoDB;

-- Inscrição nominal de associado com vagas, inclusive para torneio simples.
-- PublicoGeral apenas controla divulgação nesta etapa; não autoriza entrada
-- de visitante sem conferência online específica na portaria.
CREATE TABLE inscricao_evento (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    evento_id BIGINT UNSIGNED NOT NULL,
    pessoa_id BIGINT UNSIGNED NOT NULL,
    status ENUM('Confirmada','Cancelada') NOT NULL,
    inscrita_em_utc DATETIME(6) NOT NULL,
    cancelada_em_utc DATETIME(6) NULL,
    pessoa_ativa_id BIGINT UNSIGNED GENERATED ALWAYS AS (
        IF(status='Confirmada', pessoa_id, NULL)
    ) STORED,
    UNIQUE KEY uq_evento_inscricao_ativa (evento_id, pessoa_ativa_id),
    INDEX ix_evento_inscricao_pessoa (pessoa_id, status),
    INDEX ix_evento_inscricao_evento (evento_id, status),
    CONSTRAINT fk_inscricao_evento_evento FOREIGN KEY (evento_id) REFERENCES evento_social(id),
    CONSTRAINT fk_inscricao_evento_pessoa FOREIGN KEY (pessoa_id) REFERENCES pessoa(id)
) ENGINE=InnoDB;
