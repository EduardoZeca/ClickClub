-- CLICKCLUB - Componente 3, Etapa 3
-- Reserva confirmada, taxa original simulada e cota por associação.
-- Executar UMA VEZ, após 005. Fazer backup antes.
-- Usar conta MySQL administrativa; nunca o usuário limitado da API.
USE clickclub_db;

CREATE TABLE IF NOT EXISTS reserva (
    id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    espaco_id BIGINT UNSIGNED NOT NULL,
    associacao_id BIGINT UNSIGNED NOT NULL,
    responsavel_pessoa_id BIGINT UNSIGNED NOT NULL,
    quantidade_participantes INT UNSIGNED NOT NULL,
    inicio_utc DATETIME(6) NOT NULL,
    fim_utc DATETIME(6) NOT NULL,
    inicio_local_data DATE NOT NULL,
    finalidade VARCHAR(250) NULL,
    valor_contratado DECIMAL(12,2) NOT NULL DEFAULT 0,
    tipo_espaco_contratado ENUM('SalaoFestas','Churrasqueira','Quadra') NOT NULL,
    status ENUM('Confirmada','Cancelada') NOT NULL DEFAULT 'Confirmada',
    criada_em_utc DATETIME(6) NOT NULL,
    cancelada_em_utc DATETIME(6) NULL,
    CONSTRAINT fk_reserva_espaco FOREIGN KEY (espaco_id) REFERENCES espaco(id),
    CONSTRAINT fk_reserva_associacao FOREIGN KEY (associacao_id) REFERENCES associacao(id),
    CONSTRAINT fk_reserva_responsavel FOREIGN KEY (responsavel_pessoa_id) REFERENCES pessoa(id),
    CONSTRAINT ck_reserva_intervalo CHECK (fim_utc > inicio_utc),
    CONSTRAINT ck_reserva_participantes CHECK (quantidade_participantes BETWEEN 1 AND 10000),
    CONSTRAINT ck_reserva_valor CHECK (valor_contratado >= 0),
    INDEX ix_reserva_espaco_intervalo (espaco_id, status, inicio_utc, fim_utc),
    INDEX ix_reserva_cota (associacao_id, status, inicio_local_data)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS cobranca_reserva (
    id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    reserva_id BIGINT UNSIGNED NOT NULL,
    valor_original DECIMAL(12,2) NOT NULL,
    status ENUM('EmAberto','QuitadaSimulada','Transferida','Cancelada') NOT NULL DEFAULT 'EmAberto',
    modalidade ENUM('Avulsa','Mensalidade') NOT NULL DEFAULT 'Avulsa',
    competencia_destino DATE NULL,
    fatura_destino_id BIGINT UNSIGNED NULL,
    percentual_acrescimo DECIMAL(5,2) NOT NULL DEFAULT 0,
    valor_acrescimo DECIMAL(12,2) NOT NULL DEFAULT 0,
    decisao_em_utc DATETIME(6) NULL,
    CONSTRAINT fk_cobranca_reserva FOREIGN KEY (reserva_id) REFERENCES reserva(id),
    CONSTRAINT uq_cobranca_reserva UNIQUE (reserva_id),
    CONSTRAINT ck_cobranca_valor CHECK (valor_original > 0),
    CONSTRAINT ck_cobranca_acrescimo CHECK (valor_acrescimo >= 0)
) ENGINE=InnoDB;

-- Cobrança avulsa inicial é apenas um registro simulado.
-- Quitação, transferência, mensalidades e cancelamentos virão
-- nas etapas/componentes previstos; não há movimentação real.
