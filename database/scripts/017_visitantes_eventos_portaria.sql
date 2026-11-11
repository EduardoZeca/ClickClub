-- CLICKCLUB - Componente 6 / Etapa 3B
-- Executar UMA VEZ após 016_eventos_sociais.sql, com backup prévio.
-- O usuário clickclub_api não deve executar DDL.
USE clickclub_db;

CREATE TABLE convidado_evento (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    evento_id BIGINT UNSIGNED NOT NULL,
    nome VARCHAR(150) NOT NULL,
    identificacao_minima VARCHAR(60) NOT NULL,
    situacao ENUM('Autorizado','Revogado') NOT NULL DEFAULT 'Autorizado',
    criado_em_utc DATETIME(6) NOT NULL,
    UNIQUE KEY uq_convidado_evento_identificacao (evento_id, identificacao_minima),
    INDEX ix_convidado_evento_situacao (evento_id, situacao),
    CONSTRAINT fk_convidado_evento_evento FOREIGN KEY (evento_id)
        REFERENCES evento_social(id)
) ENGINE=InnoDB;

-- Mantém compatibilidade com registros anteriores de QR/convidado de reserva.
ALTER TABLE registro_acesso
    MODIFY COLUMN tipo ENUM('EntradaGeral','UsoReserva','EventoPublico') NOT NULL,
    ADD COLUMN evento_id BIGINT UNSIGNED NULL,
    ADD COLUMN convidado_evento_id BIGINT UNSIGNED NULL,
    ADD CONSTRAINT fk_registro_acesso_evento
        FOREIGN KEY (evento_id) REFERENCES evento_social(id),
    ADD CONSTRAINT fk_registro_acesso_convidado_evento
        FOREIGN KEY (convidado_evento_id) REFERENCES convidado_evento(id),
    ADD INDEX ix_registro_acesso_evento (evento_id, data_hora_utc);

-- Visitante não possui carteirinha e não é liberado por registro prévio:
-- cada tentativa de entrada é validada online e auditada pela API.
