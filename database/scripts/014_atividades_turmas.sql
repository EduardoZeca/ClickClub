-- CLICKCLUB - Componente 6 / Etapa 1
-- Executar UMA VEZ, após 013_portaria_registros.sql, com backup prévio.
-- Usar uma conta MySQL administrativa; não executar como clickclub_api.
USE clickclub_db;

-- Catálogo global: alterações somente por Administrador Geral na API.
CREATE TABLE atividade (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    nome VARCHAR(150) NOT NULL,
    descricao VARCHAR(1000) NULL,
    tipo VARCHAR(80) NOT NULL,
    UNIQUE KEY uq_atividade_nome_tipo (nome, tipo),
    CONSTRAINT ck_atividade_nome CHECK (CHAR_LENGTH(TRIM(nome)) >= 2),
    CONSTRAINT ck_atividade_tipo CHECK (CHAR_LENGTH(TRIM(tipo)) >= 2)
) ENGINE=InnoDB;

-- Inicio/fim guardados em UTC. A API aceita e devolve horário local da unidade.
-- Cada turma representa um período específico, sem recorrência automática.
CREATE TABLE turma (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    atividade_id BIGINT UNSIGNED NOT NULL,
    unidade_id BIGINT UNSIGNED NOT NULL,
    instrutor_pessoa_id BIGINT UNSIGNED NULL,
    inicio_utc DATETIME(6) NOT NULL,
    fim_utc DATETIME(6) NOT NULL,
    capacidade INT UNSIGNED NOT NULL,
    status ENUM('Aberta','Encerrada','Cancelada') NOT NULL DEFAULT 'Aberta',
    INDEX ix_turma_unidade_periodo (unidade_id, inicio_utc, fim_utc),
    INDEX ix_turma_atividade_status (atividade_id, status),
    CONSTRAINT fk_turma_atividade FOREIGN KEY (atividade_id) REFERENCES atividade(id),
    CONSTRAINT fk_turma_unidade FOREIGN KEY (unidade_id) REFERENCES unidade(id),
    CONSTRAINT fk_turma_instrutor FOREIGN KEY (instrutor_pessoa_id) REFERENCES pessoa(id),
    CONSTRAINT ck_turma_capacidade CHECK (capacidade BETWEEN 1 AND 10000),
    CONSTRAINT ck_turma_periodo CHECK (fim_utc > inicio_utc)
) ENGINE=InnoDB;

-- Inscrições, vagas e espera cronológica serão acrescentadas na Etapa 2.
