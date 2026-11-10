-- CLICKCLUB - Componente 3 / Etapa 2
-- Horários semanais e bloqueios por manutenção.
-- Executar UMA VEZ após 004_espacos.sql, com backup prévio.
-- Requer conta MySQL administrativa; o usuário da API não executa DDL.
USE clickclub_db;

CREATE TABLE IF NOT EXISTS horario_espaco (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    espaco_id BIGINT UNSIGNED NOT NULL,
    dia_semana TINYINT UNSIGNED NOT NULL,
    inicio TIME NOT NULL,
    fim TIME NOT NULL,
    fim_dia_seguinte BOOLEAN NOT NULL DEFAULT FALSE,
    ativo BOOLEAN NOT NULL DEFAULT TRUE,
    CONSTRAINT fk_horario_espaco FOREIGN KEY (espaco_id) REFERENCES espaco(id),
    CONSTRAINT ck_horario_dia CHECK (dia_semana BETWEEN 1 AND 7),
    CONSTRAINT ck_horario_inicio CHECK (inicio >= '00:00:00' AND inicio < '24:00:00'),
    CONSTRAINT ck_horario_fim CHECK (fim >= '00:00:00' AND fim < '24:00:00'),
    CONSTRAINT ck_horario_periodo CHECK (
        (fim_dia_seguinte = FALSE AND fim > inicio) OR
        (fim_dia_seguinte = TRUE AND fim <= inicio)
    ),
    INDEX ix_horario_espaco_dia (espaco_id, dia_semana, ativo)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS indisponibilidade_espaco (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    espaco_id BIGINT UNSIGNED NOT NULL,
    inicio_utc DATETIME(6) NOT NULL,
    fim_utc DATETIME(6) NOT NULL,
    motivo VARCHAR(500) NOT NULL,
    ativo BOOLEAN NOT NULL DEFAULT TRUE,
    CONSTRAINT fk_indisponibilidade_espaco
        FOREIGN KEY (espaco_id) REFERENCES espaco(id),
    CONSTRAINT ck_indisponibilidade_periodo CHECK (fim_utc > inicio_utc),
    INDEX ix_indisponibilidade_espaco_intervalo
        (espaco_id, ativo, inicio_utc, fim_utc)
) ENGINE=InnoDB;

-- Dia da semana: 1=segunda, ..., 7=domingo.
-- Fim_dia_seguinte permite sexta 18:00 a sábado 02:00.
-- Manutenção armazenada em UTC; entrada/saída da API usa
-- horário local no fuso definido para a unidade do espaço.
