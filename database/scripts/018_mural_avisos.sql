-- CLICKCLUB - Componente 7 / Etapa 1: Mural de avisos
-- Executar UMA VEZ após 017_visitantes_eventos_portaria.sql.
-- Fazer backup do clickclub_db. Executar com conta administrativa MySQL.
USE clickclub_db;

CREATE TABLE aviso (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    unidade_id BIGINT UNSIGNED NULL, -- NULL: aviso geral da organização.
    titulo VARCHAR(150) NOT NULL,
    corpo TEXT NOT NULL,
    inicio_utc DATETIME(6) NOT NULL,
    fim_utc DATETIME(6) NOT NULL,
    ativo BOOLEAN NOT NULL DEFAULT TRUE,
    autor_usuario_id BIGINT UNSIGNED NOT NULL,
    atualizado_em_utc DATETIME(6) NOT NULL,
    INDEX ix_aviso_publicado (ativo, inicio_utc, fim_utc),
    INDEX ix_aviso_unidade (unidade_id, ativo, inicio_utc),
    CONSTRAINT fk_aviso_unidade FOREIGN KEY (unidade_id)
        REFERENCES unidade(id),
    CONSTRAINT fk_aviso_autor FOREIGN KEY (autor_usuario_id)
        REFERENCES usuario(id),
    CONSTRAINT ck_aviso_periodo CHECK (fim_utc > inicio_utc),
    CONSTRAINT ck_aviso_titulo CHECK (CHAR_LENGTH(TRIM(titulo)) >= 2),
    CONSTRAINT ck_aviso_corpo CHECK (CHAR_LENGTH(TRIM(corpo)) >= 2)
) ENGINE=InnoDB;

-- Sem exclusão física: retirar aviso de circulação marca ativo=FALSE.
-- Leitura de avisos ativos depende do período de publicação e da unidade ativa.
