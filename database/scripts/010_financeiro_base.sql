-- CLICKCLUB - Componente 4 / Etapa 1
-- Estrutura para mensalidades e financeiro EXCLUSIVAMENTE SIMULADO.
-- Executar UMA VEZ, depois de 001 a 009, com conta MySQL administrativa.
-- Fazer backup antes. A conta clickclub_api não deve receber CREATE/ALTER.
-- DDL em MySQL provoca commits implícitos: ROLLBACK não desfaz a migração.
-- Nenhuma tarifa fictícia é inserida automaticamente.

USE clickclub_db;

-- A política vigente é escolhida por clube de origem, categoria e competência.
-- O dia 10, os 10 dias de tolerância e a multa única de 2% são regras fixas
-- aprovadas; manter os valores nesta estrutura registra a política histórica.
CREATE TABLE IF NOT EXISTS politica_financeira (
    id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    unidade_id BIGINT UNSIGNED NOT NULL,
    categoria_id BIGINT UNSIGNED NOT NULL,
    valor_mensal DECIMAL(12,2) NOT NULL,
    dia_vencimento TINYINT UNSIGNED NOT NULL DEFAULT 10,
    dias_tolerancia TINYINT UNSIGNED NOT NULL DEFAULT 10,
    percentual_multa DECIMAL(5,2) NOT NULL DEFAULT 2.00,
    vigencia_inicio DATE NOT NULL,
    vigencia_fim DATE NULL,
    criada_em_utc DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
    CONSTRAINT fk_politica_financeira_unidade FOREIGN KEY (unidade_id)
        REFERENCES unidade(id),
    CONSTRAINT fk_politica_financeira_categoria FOREIGN KEY (categoria_id)
        REFERENCES categoria_associativa(id),
    CONSTRAINT uq_politica_financeira_inicio UNIQUE
        (unidade_id, categoria_id, vigencia_inicio),
    CONSTRAINT ck_politica_financeira_valor CHECK (valor_mensal >= 0),
    CONSTRAINT ck_politica_financeira_regras CHECK (
        dia_vencimento = 10 AND dias_tolerancia = 10
        AND percentual_multa = 2.00),
    CONSTRAINT ck_politica_financeira_vigencia CHECK (
        vigencia_fim IS NULL OR vigencia_fim >= vigencia_inicio),
    INDEX ix_politica_financeira_consulta
        (unidade_id, categoria_id, vigencia_inicio, vigencia_fim)
) ENGINE=InnoDB;

-- Uma fatura mensal por associacao e competencia, independente do numero
-- de dependentes e da quantidade de clubes visitados.
-- EmPreparacao permite reservar um destino futuro antes da emissao formal.
CREATE TABLE IF NOT EXISTS fatura (
    id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    associacao_id BIGINT UNSIGNED NOT NULL,
    unidade_origem_id BIGINT UNSIGNED NOT NULL,
    politica_financeira_id BIGINT UNSIGNED NULL,
    tipo ENUM('Mensalidade') NOT NULL DEFAULT 'Mensalidade',
    competencia DATE NOT NULL,
    data_vencimento DATE NOT NULL,
    valor_total DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    status ENUM('EmPreparacao','EmAberto','QuitadaSimulada','Cancelada')
        NOT NULL DEFAULT 'EmPreparacao',
    criada_em_utc DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
    emitida_em_utc DATETIME(6) NULL,
    quitada_em_utc DATETIME(6) NULL,
    CONSTRAINT fk_fatura_associacao FOREIGN KEY (associacao_id)
        REFERENCES associacao(id),
    CONSTRAINT fk_fatura_unidade_origem FOREIGN KEY (unidade_origem_id)
        REFERENCES unidade(id),
    CONSTRAINT fk_fatura_politica FOREIGN KEY (politica_financeira_id)
        REFERENCES politica_financeira(id),
    CONSTRAINT uq_fatura_associacao_competencia UNIQUE
        (associacao_id, tipo, competencia),
    CONSTRAINT ck_fatura_total CHECK (valor_total >= 0),
    CONSTRAINT ck_fatura_competencia CHECK (DAYOFMONTH(competencia) = 1),
    CONSTRAINT ck_fatura_vencimento CHECK (
        DAYOFMONTH(data_vencimento) = 10
        AND YEAR(data_vencimento) = YEAR(competencia)
        AND MONTH(data_vencimento) = MONTH(competencia)),
    INDEX ix_fatura_origem_status_vencimento
        (unidade_origem_id, status, data_vencimento),
    INDEX ix_fatura_associacao_status (associacao_id, status)
) ENGINE=InnoDB;

-- Vinculo real da cobranca da reserva com sua eventual fatura futura.
-- Os registros anteriores permanecem inalterados (coluna ja existia e e NULL).
ALTER TABLE cobranca_reserva
    ADD INDEX ix_cobranca_reserva_fatura_destino (fatura_destino_id),
    ADD CONSTRAINT fk_cobranca_reserva_fatura_destino
        FOREIGN KEY (fatura_destino_id) REFERENCES fatura(id);

-- Saldo de creditos de cancelamentos: a compensacao sera realizada nas
-- etapas posteriores com bloqueio de linha e transacao, sem fatura negativa.
CREATE TABLE IF NOT EXISTS credito_financeiro (
    id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    associacao_id BIGINT UNSIGNED NOT NULL,
    cancelamento_reserva_id BIGINT UNSIGNED NULL,
    valor_original DECIMAL(12,2) NOT NULL,
    saldo_disponivel DECIMAL(12,2) NOT NULL,
    criado_em_utc DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
    criado_por_usuario_id BIGINT UNSIGNED NULL,
    CONSTRAINT fk_credito_financeiro_associacao FOREIGN KEY (associacao_id)
        REFERENCES associacao(id),
    CONSTRAINT fk_credito_financeiro_cancelamento
        FOREIGN KEY (cancelamento_reserva_id)
        REFERENCES cancelamento_reserva(id),
    CONSTRAINT fk_credito_financeiro_usuario
        FOREIGN KEY (criado_por_usuario_id) REFERENCES usuario(id),
    CONSTRAINT uq_credito_financeiro_cancelamento UNIQUE
        (cancelamento_reserva_id),
    CONSTRAINT ck_credito_financeiro_valores CHECK (
        valor_original > 0 AND saldo_disponivel >= 0
        AND saldo_disponivel <= valor_original),
    INDEX ix_credito_financeiro_associacao (associacao_id, saldo_disponivel)
) ENGINE=InnoDB;

-- Valores positivos sao debitos; credito aplicado tem valor negativo.
-- Chaves geradas impedem duas mensalidades-base ou duas multas de atraso
-- na mesma fatura. Itens de locacao sao unicos por reserva e tipo.
CREATE TABLE IF NOT EXISTS item_fatura (
    id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    fatura_id BIGINT UNSIGNED NOT NULL,
    tipo ENUM('MensalidadeBase','LocacaoPrincipal','AcrescimoTransferencia',
              'MultaAtraso','MultaCancelamento','Credito') NOT NULL,
    descricao VARCHAR(250) NOT NULL,
    valor DECIMAL(12,2) NOT NULL,
    reserva_id BIGINT UNSIGNED NULL,
    origem_unidade_id BIGINT UNSIGNED NULL,
    credito_financeiro_id BIGINT UNSIGNED NULL,
    criado_em_utc DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
    fatura_base_unica BIGINT UNSIGNED GENERATED ALWAYS AS (
        IF(tipo = 'MensalidadeBase', fatura_id, NULL)) STORED,
    fatura_multa_unica BIGINT UNSIGNED GENERATED ALWAYS AS (
        IF(tipo = 'MultaAtraso', fatura_id, NULL)) STORED,
    CONSTRAINT fk_item_fatura FOREIGN KEY (fatura_id) REFERENCES fatura(id),
    CONSTRAINT fk_item_fatura_reserva FOREIGN KEY (reserva_id)
        REFERENCES reserva(id),
    CONSTRAINT fk_item_fatura_origem FOREIGN KEY (origem_unidade_id)
        REFERENCES unidade(id),
    CONSTRAINT fk_item_fatura_credito FOREIGN KEY (credito_financeiro_id)
        REFERENCES credito_financeiro(id),
    CONSTRAINT uq_item_fatura_base UNIQUE (fatura_base_unica),
    CONSTRAINT uq_item_fatura_multa_atraso UNIQUE (fatura_multa_unica),
    CONSTRAINT uq_item_fatura_reserva_tipo UNIQUE (reserva_id, tipo),
    CONSTRAINT uq_item_fatura_credito UNIQUE (fatura_id, credito_financeiro_id),
    CONSTRAINT ck_item_fatura_sinal CHECK (
        (tipo = 'Credito' AND valor <= 0)
        OR (tipo <> 'Credito' AND valor >= 0)),
    CONSTRAINT ck_item_fatura_origem CHECK (
        (tipo IN ('LocacaoPrincipal','AcrescimoTransferencia')
            AND reserva_id IS NOT NULL AND origem_unidade_id IS NOT NULL)
        OR (tipo NOT IN ('LocacaoPrincipal','AcrescimoTransferencia'))),
    CONSTRAINT ck_item_fatura_credito CHECK (
        (tipo = 'Credito' AND credito_financeiro_id IS NOT NULL)
        OR (tipo <> 'Credito' AND credito_financeiro_id IS NULL)),
    INDEX ix_item_fatura_fatura (fatura_id),
    INDEX ix_item_fatura_origem (origem_unidade_id, tipo)
) ENGINE=InnoDB;

-- Livro de eventos financeiros SIMULADOS: nenhum gateway, boleto ou PIX.
-- Valores e autor das decisoes ficam registrados sem dados bancarios.
CREATE TABLE IF NOT EXISTS lancamento_financeiro (
    id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    fatura_id BIGINT UNSIGNED NULL,
    item_fatura_id BIGINT UNSIGNED NULL,
    cobranca_reserva_id BIGINT UNSIGNED NULL,
    unidade_referencia_id BIGINT UNSIGNED NULL,
    tipo VARCHAR(50) NOT NULL,
    valor DECIMAL(12,2) NOT NULL,
    data_hora_utc DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
    usuario_responsavel_id BIGINT UNSIGNED NOT NULL,
    observacao VARCHAR(500) NULL,
    CONSTRAINT fk_lancamento_financeiro_fatura FOREIGN KEY (fatura_id)
        REFERENCES fatura(id),
    CONSTRAINT fk_lancamento_financeiro_item FOREIGN KEY (item_fatura_id)
        REFERENCES item_fatura(id),
    CONSTRAINT fk_lancamento_financeiro_cobranca FOREIGN KEY (cobranca_reserva_id)
        REFERENCES cobranca_reserva(id),
    CONSTRAINT fk_lancamento_financeiro_unidade FOREIGN KEY (unidade_referencia_id)
        REFERENCES unidade(id),
    CONSTRAINT fk_lancamento_financeiro_usuario FOREIGN KEY (usuario_responsavel_id)
        REFERENCES usuario(id),
    CONSTRAINT ck_lancamento_financeiro_tipo CHECK (CHAR_LENGTH(TRIM(tipo)) > 0),
    INDEX ix_lancamento_financeiro_fatura (fatura_id, data_hora_utc),
    INDEX ix_lancamento_financeiro_unidade
        (unidade_referencia_id, data_hora_utc)
) ENGINE=InnoDB;

-- Etapa 1: somente esquema. Nao emitir, quitar ou transferir automaticamente.
-- A API fara verificacoes de escopo, juros/multa e transacoes em etapas
-- posteriores; nao executar INSERT de tarifas ou faturas nesta migracao.
-- A estrutura de cancelamento sera ampliada em migracao separada na Etapa 5.
