# ClickClub — Componente 4, Etapa 2: testes manuais

**Situação:** código preparado; execução, build e testes no ambiente do usuário **pendentes**.

## Pré-requisitos

1. Etapa 1 concluída e script `010_financeiro_base.sql` já aplicado. **Não executá-lo novamente.**
2. Fazer backup de `clickclub_db` antes dos testes com dados reais.
3. Executar `dotnet build ClickClub.slnx` na raiz da solução; iniciar API e Web pelos comandos habituais.
4. Ter login de Administrador Geral, Titular e Dependente, uma associação com data de adesão conhecida e categoria/clube definidos.
5. Não cadastrar CPF, nomes reais ou credenciais em capturas encaminhadas para avaliação.

## Testes mínimos — realizar pela Web

1. **Política mensal:** Administrador Geral → **Financeiro → Valores mensais → Cadastrar valor**. Selecionar clube e categoria da associação, valor fictício `R$ 150,00`, vigência no primeiro dia do mês seguinte ao início da associação. Confirmar na listagem. Tentar repetir o mesmo clube/categoria/vigência: deve ser rejeitado, sem duplicar política.
2. **Competência inicial e vencimento:** Administrador Geral → **Financeiro → Emitir mensalidade**. Selecionar associação e informar o dia 1º do **mesmo mês da adesão**: a API deve recusar. Informar o dia 1º do **mês seguinte**, coberto pela política: deve criar **uma fatura EmAberto** com item `MensalidadeBase` de `R$ 150,00`, competência escolhida e vencimento no **dia 10** desse mês.
3. **Idempotência:** Repetir exatamente a emissão do teste 2. Deve mostrar que a fatura já existia e manter **o mesmo ID, valor e um único item MensalidadeBase**. Verificar também que não há lançamento duplicado.
4. **Histórico de preços:** Cadastrar nova política para mesmo clube/categoria, vigência no dia 1º do **mês posterior à primeira fatura**, valor fictício `R$ 175,00`. Emitir esse mês: nova fatura de `R$ 175,00`. A fatura anterior continua em `R$ 150,00`.
5. **Autorização:** Entrar como Titular da associação: **Financeiro** mostra somente suas faturas e detalhes. Não permite emitir nem cadastrar políticas. Entrar como Dependente: acesso financeiro negado. Se houver segundo Titular, tentar abrir URL de detalhe da fatura do primeiro: não deve apresentar dados (HTTP 404 na API).
6. **Regressão:** Fazer login, consultar associação e consultar/criar uma reserva com o fluxo já aprovado no Componente 3. Nenhuma operação financeira nova deve quitar ou transferir uma locação. Os estados de reservas existentes não devem mudar ao consultar faturas.
7. **Integridade SQL:** Executar `database/tests/011_financeiro_emissao_validacao.sql`. Consultas 2–6 devem retornar **zero linhas**; consulta 1 mostra as faturas; consulta 7 mostra **uma emissão por fatura nova**.

## Falhas esperadas (sem alterar código)

- Sem política vigente para o clube e categoria: a emissão apresenta mensagem de falta de política, sem criar fatura.
- Data de competência diferente do dia 1º: formulário/API rejeitam.
- Associação cancelada: não recebe nova emissão; associação suspensa continua cobravel, de acordo com o cadastro existente.
- Valor mensal negativo: formulário/API rejeitam.

## Fora desta etapa

- Multa única de 2% após 10 dias completos de atraso, bloqueio por inadimplência e reabilitação (Etapa 3).
- Quitação simulada e transferência de locação com acréscimo de 10% (Etapa 4).
- Créditos e estornos de cancelamento (Etapa 5).
- Permissões operacionais do Tesoureiro por clube e visão da locação prestada (Etapa 6).

**Critério de avanço:** o usuário confirma resultado da compilação, dos sete testes e autoriza expressamente a Etapa 3.
