-- CLICKCLUB - Componente 3 / Etapa 4
-- Cadastro de convidados para salão de festas e churrasqueira.
-- Executar UMA VEZ depois do 006; fazer backup antes.
-- Usar conta administrativa MySQL (não usar clickclub_api para DDL).
USE clickclub_db;

CREATE TABLE IF NOT EXISTS convidado_reserva (
    id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    reserva_id BIGINT UNSIGNED NOT NULL,
    nome VARCHAR(150) NOT NULL,
    identificacao_minima VARCHAR(60) NOT NULL,
    situacao ENUM('Autorizado','Removido') NOT NULL DEFAULT 'Autorizado',
    criado_em_utc DATETIME(6) NOT NULL,
    atualizado_em_utc DATETIME(6) NOT NULL,
    CONSTRAINT fk_convidado_reserva FOREIGN KEY (reserva_id) REFERENCES reserva(id),
    UNIQUE KEY uq_convidado_reserva_identificacao (reserva_id, identificacao_minima),
    INDEX ix_convidado_reserva_situacao (reserva_id, situacao)
) ENGINE=InnoDB;

-- O período para alterar os convidados é verificado pela API
-- usando inicio_utc da reserva e 24 horas exatas de antecedência.
