-- =====================================================
-- CLICKCLUB
-- Script 001 - Identidade e estrutura associativa
-- MySQL 8.4
-- =====================================================

CREATE DATABASE IF NOT EXISTS clickclub_db
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_0900_ai_ci;

USE clickclub_db;

-- 1. UNIDADES

CREATE TABLE IF NOT EXISTS unidade (
    id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    nome VARCHAR(150) NOT NULL,
    endereco VARCHAR(255) NOT NULL,
    municipio VARCHAR(120) NOT NULL,
    uf CHAR(2) NOT NULL,
    fuso_horario VARCHAR(64) NOT NULL,
    status ENUM('Ativa', 'Inativa')
        NOT NULL DEFAULT 'Ativa'
) ENGINE=InnoDB;

-- 2. PESSOAS

CREATE TABLE IF NOT EXISTS pessoa (
    id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    nome VARCHAR(150) NOT NULL,
    data_nascimento DATE NULL,
    documento_identificacao VARCHAR(30) NULL,
    telefone VARCHAR(30) NULL,
    email VARCHAR(254) NULL,
    status ENUM('Ativa', 'Inativa')
        NOT NULL DEFAULT 'Ativa',

    UNIQUE KEY uq_pessoa_documento (documento_identificacao)
) ENGINE=InnoDB;

-- 3. USUARIOS

CREATE TABLE IF NOT EXISTS usuario (
    id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    pessoa_id BIGINT UNSIGNED NOT NULL,
    login VARCHAR(100) NOT NULL,
    senha_hash VARCHAR(255) NOT NULL,
    ativo BOOLEAN NOT NULL DEFAULT TRUE,

    UNIQUE KEY uq_usuario_pessoa (pessoa_id),
    UNIQUE KEY uq_usuario_login (login),

    CONSTRAINT fk_usuario_pessoa
        FOREIGN KEY (pessoa_id)
        REFERENCES pessoa(id)
) ENGINE=InnoDB;

-- 4. PAPEIS DOS USUARIOS

CREATE TABLE IF NOT EXISTS papel_usuario (
    id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    usuario_id BIGINT UNSIGNED NOT NULL,

    papel ENUM(
        'Titular',
        'Dependente',
        'FuncionarioUnidade',
        'AdministradorGeral',
        'Porteiro'
    ) NOT NULL,

    unidade_id BIGINT UNSIGNED NULL,

    unidade_escopo_id BIGINT UNSIGNED
        GENERATED ALWAYS AS (
            COALESCE(unidade_id, 0)
        ) STORED,

    UNIQUE KEY uq_usuario_papel_escopo (
        usuario_id,
        papel,
        unidade_escopo_id
    ),

    CONSTRAINT fk_papel_usuario
        FOREIGN KEY (usuario_id)
        REFERENCES usuario(id),

    CONSTRAINT fk_papel_unidade
        FOREIGN KEY (unidade_id)
        REFERENCES unidade(id)
) ENGINE=InnoDB;

-- 5. CATEGORIAS ASSOCIATIVAS

CREATE TABLE IF NOT EXISTS categoria_associativa (
    id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    nome VARCHAR(100) NOT NULL,
    regras_beneficios JSON NULL,
    ativa BOOLEAN NOT NULL DEFAULT TRUE,

    UNIQUE KEY uq_categoria_nome (nome)
) ENGINE=InnoDB;

-- 6. ASSOCIACOES

CREATE TABLE IF NOT EXISTS associacao (
    id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    titular_pessoa_id BIGINT UNSIGNED NOT NULL,
    categoria_id BIGINT UNSIGNED NOT NULL,
    unidade_origem_id BIGINT UNSIGNED NOT NULL,
    matricula VARCHAR(30) NOT NULL,

    status ENUM(
        'Ativa',
        'Suspensa',
        'Cancelada'
    ) NOT NULL DEFAULT 'Ativa',

    data_inicio DATE NOT NULL,

    titular_ativo_id BIGINT UNSIGNED
        GENERATED ALWAYS AS (
            IF(status = 'Ativa', titular_pessoa_id, NULL)
        ) STORED,

    UNIQUE KEY uq_associacao_matricula (matricula),
    UNIQUE KEY uq_titular_associacao_ativa (titular_ativo_id),

    CONSTRAINT fk_associacao_titular
        FOREIGN KEY (titular_pessoa_id)
        REFERENCES pessoa(id),

    CONSTRAINT fk_associacao_categoria
        FOREIGN KEY (categoria_id)
        REFERENCES categoria_associativa(id),

    CONSTRAINT fk_associacao_unidade
        FOREIGN KEY (unidade_origem_id)
        REFERENCES unidade(id)
) ENGINE=InnoDB;

-- 7. DEPENDENTES DAS ASSOCIACOES

CREATE TABLE IF NOT EXISTS dependente_associacao (
    id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    associacao_id BIGINT UNSIGNED NOT NULL,
    dependente_pessoa_id BIGINT UNSIGNED NOT NULL,
    ativo BOOLEAN NOT NULL DEFAULT TRUE,
    autorizacoes JSON NULL,

    dependente_ativo_id BIGINT UNSIGNED
        GENERATED ALWAYS AS (
            IF(ativo = TRUE, dependente_pessoa_id, NULL)
        ) STORED,

    UNIQUE KEY uq_dependente_associacao (
        associacao_id,
        dependente_pessoa_id
    ),

    UNIQUE KEY uq_dependente_ativo (
        dependente_ativo_id
    ),

    CONSTRAINT fk_dependente_associacao
        FOREIGN KEY (associacao_id)
        REFERENCES associacao(id),

    CONSTRAINT fk_dependente_pessoa
        FOREIGN KEY (dependente_pessoa_id)
        REFERENCES pessoa(id)
) ENGINE=InnoDB;

-- =====================================================
-- DADOS INICIAIS
-- =====================================================

INSERT INTO categoria_associativa (nome, ativa)
SELECT 'Familiar', TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM categoria_associativa
    WHERE nome = 'Familiar'
);

INSERT INTO categoria_associativa (nome, ativa)
SELECT 'Individual', TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM categoria_associativa
    WHERE nome = 'Individual'
);

INSERT INTO categoria_associativa (nome, ativa)
SELECT 'Atleta', TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM categoria_associativa
    WHERE nome = 'Atleta'
);