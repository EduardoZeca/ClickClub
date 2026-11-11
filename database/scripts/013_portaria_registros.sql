-- CLICKCLUB - Componente 5 / Etapa 2
-- Executar UMA VEZ após 012_carteirinha_credenciais.sql.
-- Usar conta administrativa MySQL 8.4, após backup do clickclub_db.
-- NÃO executar como clickclub_api: DDL faz commits implícitos.

USE clickclub_db;

-- Histórico de cada conferência online executada pela API.
-- Tentativas com QR inválido não identificam uma pessoa e continuam
-- registradas sem armazenar o conteúdo do QR ou do JWT do operador.
CREATE TABLE registro_acesso (
    id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    unidade_id BIGINT UNSIGNED NOT NULL,
    pessoa_id BIGINT UNSIGNED NULL,
    convidado_id BIGINT UNSIGNED NULL,
    credencial_carteirinha_id BIGINT UNSIGNED NULL,
    reserva_id BIGINT UNSIGNED NULL,
    porteiro_usuario_id BIGINT UNSIGNED NOT NULL,
    tipo ENUM('EntradaGeral','UsoReserva') NOT NULL,
    resultado ENUM('Permitido','Negado') NOT NULL,
    motivo_codigo VARCHAR(48) NOT NULL,
    data_hora_utc DATETIME(6) NOT NULL,

    INDEX ix_registro_acesso_unidade_data (unidade_id, data_hora_utc),
    INDEX ix_registro_acesso_pessoa_data (pessoa_id, data_hora_utc),
    INDEX ix_registro_acesso_operador_data (porteiro_usuario_id, data_hora_utc),
    CONSTRAINT fk_registro_acesso_unidade
        FOREIGN KEY (unidade_id) REFERENCES unidade(id),
    CONSTRAINT fk_registro_acesso_pessoa
        FOREIGN KEY (pessoa_id) REFERENCES pessoa(id),
    CONSTRAINT fk_registro_acesso_convidado
        FOREIGN KEY (convidado_id) REFERENCES convidado_reserva(id),
    CONSTRAINT fk_registro_acesso_credencial
        FOREIGN KEY (credencial_carteirinha_id)
        REFERENCES credencial_carteirinha(id),
    CONSTRAINT fk_registro_acesso_reserva
        FOREIGN KEY (reserva_id) REFERENCES reserva(id),
    CONSTRAINT fk_registro_acesso_operador
        FOREIGN KEY (porteiro_usuario_id) REFERENCES usuario(id),
    CONSTRAINT ck_registro_acesso_motivo
        CHECK (CHAR_LENGTH(TRIM(motivo_codigo)) > 0)
) ENGINE=InnoDB;

-- A tabela não oferece uma coluna de autorização manual. Apenas a API
-- decide Permitido/Negado após conferir estado atual no MySQL.
