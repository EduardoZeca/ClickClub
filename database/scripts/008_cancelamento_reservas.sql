-- CLICKCLUB - Componente 3 / Etapa 5
-- Histórico de cancelamentos e acertos financeiros exclusivamente simulados.
-- Executar UMA VEZ após 007, com conta MySQL administrativa e backup prévio.
USE clickclub_db;

CREATE TABLE IF NOT EXISTS cancelamento_reserva (
    id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    reserva_id BIGINT UNSIGNED NOT NULL,
    usuario_responsavel_id BIGINT UNSIGNED NOT NULL,
    cancelada_em_utc DATETIME(6) NOT NULL,
    valor_original DECIMAL(12,2) NOT NULL,
    situacao_original_cobranca ENUM('Gratuita','EmAberto','QuitadaSimulada') NOT NULL,
    multa_simulada DECIMAL(12,2) NOT NULL DEFAULT 0,
    credito_simulado_pendente DECIMAL(12,2) NOT NULL DEFAULT 0,
    situacao_financeira ENUM('SemAcerto','MultaPendente','CreditoPendente')
        NOT NULL DEFAULT 'SemAcerto',
    CONSTRAINT uq_cancelamento_reserva UNIQUE (reserva_id),
    CONSTRAINT fk_cancelamento_reserva FOREIGN KEY (reserva_id) REFERENCES reserva(id),
    CONSTRAINT fk_cancelamento_usuario FOREIGN KEY (usuario_responsavel_id) REFERENCES usuario(id),
    CONSTRAINT ck_cancelamento_valores CHECK (
        valor_original >= 0 AND multa_simulada >= 0
        AND credito_simulado_pendente >= 0
        AND multa_simulada + credito_simulado_pendente <= valor_original
    )
) ENGINE=InnoDB;

-- EmAberto: encerrar a cobrança original, preservar multa pendente separadamente.
-- QuitadaSimulada: preservar crédito pendente; a multa é retida do valor original.
-- Transferida: cancelar/ajustar fatura somente no componente financeiro.
