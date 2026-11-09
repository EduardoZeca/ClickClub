-- =============================================================
-- CLICKCLUB - Componente 2 / Etapa 1
-- Script 002 - Complementos de identidade e cadastros
-- Alvo: MySQL 8.4
-- =============================================================
-- Executar UMA VEZ, após o 001_identidade_associativa.sql,
-- usando uma conta administrativa do MySQL.
-- Fazer backup do clickclub_db antes de executar.
-- NÃO executar como clickclub_api: essa conta não tem DDL.
-- DDL (ALTER/CREATE) realiza commits implícitos no MySQL;
-- um ROLLBACK não desfaz toda esta migração.
-- =============================================================

USE clickclub_db;

-- Dados específicos do clube/unidade.
-- Colunas inicialmente nulas para preservar registros existentes.
ALTER TABLE unidade
    ADD COLUMN sigla VARCHAR(20) NULL AFTER nome,
    ADD COLUMN bairro VARCHAR(120) NULL AFTER endereco,
    ADD COLUMN telefone VARCHAR(30) NULL AFTER uf,
    ADD COLUMN email VARCHAR(254) NULL AFTER telefone,
    ADD UNIQUE KEY uq_unidade_sigla (sigla);

-- Dados complementares da pessoa.
-- CPF: onze dígitos sem pontuação; o formato e os dígitos
-- verificadores serão validados também pela API.
-- O campo documento_identificacao anterior é preservado.
ALTER TABLE pessoa
    ADD COLUMN cpf CHAR(11) NULL AFTER documento_identificacao,
    ADD COLUMN endereco VARCHAR(255) NULL AFTER cpf,
    ADD COLUMN bairro VARCHAR(120) NULL AFTER endereco,
    ADD COLUMN cidade VARCHAR(120) NULL AFTER bairro,
    ADD COLUMN uf CHAR(2) NULL AFTER cidade,
    ADD COLUMN sexo VARCHAR(40) NULL AFTER uf,
    ADD COLUMN estado_civil VARCHAR(40) NULL AFTER sexo,
    ADD UNIQUE KEY uq_pessoa_cpf (cpf);

-- Novos perfis locais sem remover ou renumerar os atuais.
ALTER TABLE papel_usuario
    MODIFY COLUMN papel ENUM(
        'Titular',
        'Dependente',
        'FuncionarioUnidade',
        'AdministradorGeral',
        'Porteiro',
        'PresidenteClube',
        'Tesoureiro'
    ) NOT NULL;

-- Tipo de vínculo NÃO é categoria associativa nem perfil.
CREATE TABLE IF NOT EXISTS tipo_vinculo_associativo (
    id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    nome VARCHAR(100) NOT NULL,
    direito_voto BOOLEAN NOT NULL DEFAULT FALSE,
    ativo BOOLEAN NOT NULL DEFAULT TRUE,
    UNIQUE KEY uq_tipo_vinculo_nome (nome)
) ENGINE=InnoDB;

INSERT INTO tipo_vinculo_associativo (nome, direito_voto, ativo)
SELECT 'Associado/Fundador', TRUE, TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM tipo_vinculo_associativo
    WHERE nome = 'Associado/Fundador'
);

INSERT INTO tipo_vinculo_associativo (nome, direito_voto, ativo)
SELECT 'Convidado', FALSE, TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM tipo_vinculo_associativo
    WHERE nome = 'Convidado'
);

INSERT INTO tipo_vinculo_associativo (nome, direito_voto, ativo)
SELECT 'Simpatizante', FALSE, TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM tipo_vinculo_associativo
    WHERE nome = 'Simpatizante'
);

-- A referência é opcional para registros anteriores à migração.
-- Novas associações exigirão tipo de vínculo por validação da API.
ALTER TABLE associacao
    ADD COLUMN tipo_vinculo_id BIGINT UNSIGNED NULL AFTER categoria_id,
    ADD CONSTRAINT fk_associacao_tipo_vinculo
        FOREIGN KEY (tipo_vinculo_id)
        REFERENCES tipo_vinculo_associativo(id);

-- Catálogo aberto e relacionamento de múltiplos interesses.
CREATE TABLE IF NOT EXISTS interesse (
    id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    nome VARCHAR(100) NOT NULL,
    ativo BOOLEAN NOT NULL DEFAULT TRUE,
    UNIQUE KEY uq_interesse_nome (nome)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS pessoa_interesse (
    pessoa_id BIGINT UNSIGNED NOT NULL,
    interesse_id BIGINT UNSIGNED NOT NULL,
    PRIMARY KEY (pessoa_id, interesse_id),
    CONSTRAINT fk_pessoa_interesse_pessoa
        FOREIGN KEY (pessoa_id) REFERENCES pessoa(id),
    CONSTRAINT fk_pessoa_interesse_interesse
        FOREIGN KEY (interesse_id) REFERENCES interesse(id)
) ENGINE=InnoDB;

-- A adesão pendente não cria uma associação ativa.
-- Somente PresidenteClube da unidade pretendida ou
-- AdministradorGeral poderá decidir (regra aplicada na API).
CREATE TABLE IF NOT EXISTS solicitacao_adesao (
    id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    pessoa_id BIGINT UNSIGNED NOT NULL,
    unidade_pretendida_id BIGINT UNSIGNED NOT NULL,
    tipo_vinculo_id BIGINT UNSIGNED NOT NULL,
    status ENUM('Pendente', 'Aprovada', 'Rejeitada')
        NOT NULL DEFAULT 'Pendente',
    solicitada_em DATETIME(6) NOT NULL,
    analisada_em DATETIME(6) NULL,
    analisada_por_usuario_id BIGINT UNSIGNED NULL,
    motivo VARCHAR(500) NULL,
    INDEX ix_solicitacao_unidade_status
        (unidade_pretendida_id, status),
    INDEX ix_solicitacao_pessoa (pessoa_id),
    CONSTRAINT fk_solicitacao_pessoa
        FOREIGN KEY (pessoa_id) REFERENCES pessoa(id),
    CONSTRAINT fk_solicitacao_unidade
        FOREIGN KEY (unidade_pretendida_id) REFERENCES unidade(id),
    CONSTRAINT fk_solicitacao_tipo
        FOREIGN KEY (tipo_vinculo_id) REFERENCES tipo_vinculo_associativo(id),
    CONSTRAINT fk_solicitacao_analisada_por
        FOREIGN KEY (analisada_por_usuario_id) REFERENCES usuario(id)
) ENGINE=InnoDB;

-- Registro de mudanças críticas iniciado no Componente 2.
-- Não armazenar senha, token, segredo nem dados pessoais completos.
CREATE TABLE IF NOT EXISTS log_auditoria (
    id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    usuario_id BIGINT UNSIGNED NULL,
    unidade_id BIGINT UNSIGNED NULL,
    acao VARCHAR(100) NOT NULL,
    entidade VARCHAR(100) NOT NULL,
    identificador_entidade VARCHAR(100) NOT NULL,
    data_hora DATETIME(6) NOT NULL,
    resumo_alteracao VARCHAR(1000) NULL,
    INDEX ix_auditoria_data (data_hora),
    INDEX ix_auditoria_entidade (entidade, identificador_entidade),
    CONSTRAINT fk_auditoria_usuario
        FOREIGN KEY (usuario_id) REFERENCES usuario(id),
    CONSTRAINT fk_auditoria_unidade
        FOREIGN KEY (unidade_id) REFERENCES unidade(id)
) ENGINE=InnoDB;

-- Fim do Script 002.
