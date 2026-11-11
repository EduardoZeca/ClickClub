-- CLICKCLUB - Componente 4 / Etapa 5
-- Executar UMA VEZ APÓS 010 com usuário MySQL administrativo.
-- Antes: backup do banco. Não executar com o usuário clickclub_api.
-- DDL no MySQL efetua commits implícitos e não é desfeito por ROLLBACK.
USE clickclub_db;

-- Preserva o estado original da cobrança, inclusive locações transferidas,
-- e registra os 10% efetivamente revertidos no cancelamento.
ALTER TABLE cancelamento_reserva
    ADD COLUMN valor_acrescimo_transferido DECIMAL(12,2) NOT NULL DEFAULT 0
        AFTER valor_original,
    MODIFY COLUMN situacao_original_cobranca
        ENUM('Gratuita','EmAberto','QuitadaSimulada','Transferida') NOT NULL,
    MODIFY COLUMN situacao_financeira
        ENUM('SemAcerto','MultaPendente','CreditoPendente',
             'CreditoParcial','CreditoCompensado') NOT NULL DEFAULT 'SemAcerto',
    DROP CHECK ck_cancelamento_valores,
    ADD CONSTRAINT ck_cancelamento_valores CHECK (
        valor_original >= 0 AND valor_acrescimo_transferido >= 0
        AND multa_simulada >= 0 AND credito_simulado_pendente >= 0
        AND multa_simulada + credito_simulado_pendente
            <= valor_original + valor_acrescimo_transferido
    );

-- Impede usar crédito futuro para quitar competência anterior à restituição.
-- Créditos manuais preexistentes, caso existam, mantêm elegibilidade anterior.
ALTER TABLE credito_financeiro
    ADD COLUMN competencia_minima DATE NOT NULL DEFAULT '1000-01-01'
        AFTER saldo_disponivel;

-- Reaproveita os créditos que haviam sido registrados pelo Componente 3
-- antes da criação do livro de créditos. Não apaga nem duplica registros.
INSERT INTO credito_financeiro
    (associacao_id, cancelamento_reserva_id,
     valor_original, saldo_disponivel, competencia_minima, criado_em_utc,
     criado_por_usuario_id)
SELECT r.associacao_id, c.id,
       c.credito_simulado_pendente, c.credito_simulado_pendente,
       DATE_ADD(DATE_FORMAT(c.cancelada_em_utc, '%Y-%m-01'), INTERVAL 1 MONTH),
       c.cancelada_em_utc, c.usuario_responsavel_id
FROM cancelamento_reserva c
JOIN reserva r ON r.id=c.reserva_id
LEFT JOIN credito_financeiro existente
    ON existente.cancelamento_reserva_id=c.id
WHERE c.situacao_original_cobranca='QuitadaSimulada'
  AND c.credito_simulado_pendente>0
  AND existente.id IS NULL;
