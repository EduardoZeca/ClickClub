-- CLICKCLUB - Componente 3 / Etapa 7
-- Permissão geral, revogável, do titular para locações pagas por dependentes menores.
-- Executar uma vez após 008, usando conta MySQL administrativa e backup prévio.
USE clickclub_db;

CREATE TABLE IF NOT EXISTS autorizacao_reserva_dependente (
    id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    dependente_associacao_id BIGINT UNSIGNED NOT NULL,
    permitir_reserva_paga BOOLEAN NOT NULL DEFAULT FALSE,
    titular_usuario_id BIGINT UNSIGNED NOT NULL,
    atualizado_em_utc DATETIME(6) NOT NULL,
    CONSTRAINT uq_autorizacao_reserva_vinculo UNIQUE (dependente_associacao_id),
    CONSTRAINT fk_autorizacao_reserva_vinculo FOREIGN KEY (dependente_associacao_id)
        REFERENCES dependente_associacao(id),
    CONSTRAINT fk_autorizacao_reserva_titular FOREIGN KEY (titular_usuario_id)
        REFERENCES usuario(id)
) ENGINE=InnoDB;

-- Não existe permissão implícita: ausência de registro significa FALSE.
-- O histórico de concessões e revogações também é registrado em log_auditoria.
