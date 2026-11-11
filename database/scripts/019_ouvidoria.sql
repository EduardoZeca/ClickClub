-- CLICKCLUB - Componente 7 / Etapa 2: Ouvidoria
-- Executar UMA VEZ após 018_mural_avisos.sql; fazer backup do banco.
-- Usar conta administrativa MySQL. Não executar com clickclub_api.
USE clickclub_db;

CREATE TABLE chamado_ouvidoria (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    pessoa_id BIGINT UNSIGNED NOT NULL,
    unidade_id BIGINT UNSIGNED NULL,
    tipo ENUM('Solicitacao','Reclamacao') NOT NULL,
    categoria VARCHAR(80) NOT NULL,
    assunto VARCHAR(150) NOT NULL,
    descricao VARCHAR(4000) NOT NULL,
    status ENUM('Aberto','Respondido','Encerrado') NOT NULL DEFAULT 'Aberto',
    criado_em_utc DATETIME(6) NOT NULL,
    atualizado_em_utc DATETIME(6) NOT NULL,
    encerrado_em_utc DATETIME(6) NULL,
    INDEX ix_chamado_pessoa_criacao (pessoa_id, criado_em_utc),
    INDEX ix_chamado_unidade_status (unidade_id, status, criado_em_utc),
    CONSTRAINT fk_chamado_pessoa FOREIGN KEY (pessoa_id) REFERENCES pessoa(id),
    CONSTRAINT fk_chamado_unidade FOREIGN KEY (unidade_id) REFERENCES unidade(id),
    CONSTRAINT ck_chamado_categoria CHECK (CHAR_LENGTH(TRIM(categoria)) >= 2),
    CONSTRAINT ck_chamado_assunto CHECK (CHAR_LENGTH(TRIM(assunto)) >= 2),
    CONSTRAINT ck_chamado_descricao CHECK (CHAR_LENGTH(TRIM(descricao)) >= 2)
) ENGINE=InnoDB;

CREATE TABLE resposta_chamado (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    chamado_id BIGINT UNSIGNED NOT NULL,
    autor_usuario_id BIGINT UNSIGNED NOT NULL,
    conteudo VARCHAR(4000) NOT NULL,
    data_hora_utc DATETIME(6) NOT NULL,
    INDEX ix_resposta_chamado_ordem (chamado_id, data_hora_utc, id),
    CONSTRAINT fk_resposta_chamado FOREIGN KEY (chamado_id)
        REFERENCES chamado_ouvidoria(id),
    CONSTRAINT fk_resposta_autor FOREIGN KEY (autor_usuario_id)
        REFERENCES usuario(id),
    CONSTRAINT ck_resposta_conteudo CHECK (CHAR_LENGTH(TRIM(conteudo)) >= 2)
) ENGINE=InnoDB;

-- Unidade nula: ouvidoria geral, gerenciada somente por Administrador Geral.
-- Chamados e respostas não são apagados fisicamente. Somente gestores
-- autorizados respondem, e o associado acompanha apenas os próprios chamados.
