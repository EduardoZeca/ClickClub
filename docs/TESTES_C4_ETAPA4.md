# ClickClub — Componente 4, Etapa 4: testes iniciais

**Situação:** arquivos preparados em 11/11/2026; execução, compilação e testes no PC do usuário pendentes.

## Pré-requisitos

1. A Etapa 3 está instalada e as tabelas de `010_financeiro_base.sql` existem. **Não executar novamente o 010.**
2. Fazer backup lógico antes dos testes que gravem cobranças e faturas.
3. Compilar a solução: `dotnet build ClickClub.slnx`; iniciar API e Web com os perfis HTTPS habituais.
4. Usar somente **reservas pagas fictícias e futuras**, ainda com cobrança `EmAberto`. Reservas gratuitas não têm cobrança. Caso seja necessário criar reservas de teste, respeitar cota de três, horários e antecedência mínima do salão.
5. A transferência usa a mensalidade do **clube de origem da associação**, mesmo quando o espaço pertence a outra unidade.

## Testes mínimos do usuário (Web)

| ID | Ação | Resultado esperado |
| --- | --- | --- |
| 1 | Entrar como Titular, abrir Reservas → Cobrança de uma reserva paga confirmada e futura. | Prévia mostra taxa original, acréscimo único de 10%, total, competência de destino e vencimento dia 10. |
| 2 | Escolher **Quitar avulso (simulado)** na reserva do teste 1. | Cobrança `QuitadaSimulada`, modalidade `Avulsa`, lançamento negativo único; sem acréscimo nem inclusão em fatura mensal. |
| 3 | Em outra reserva paga futura, escolher **Transferir para mensalidade**. | Cobrança `Transferida`, modalidade `Mensalidade`, acréscimo único de 10%, principal e acréscimo em dois itens na mesma fatura futura. |
| 4 | No Financeiro, consultar a fatura de destino (pode estar `EmPreparacao`). Emitir sua mensalidade-base pelo Administrador Geral, se necessário. | A fatura reúne os itens de locação e mensalidade-base, soma correta, vencimento dia 10 e sem duplicidades. |
| 5 | Entrar como Dependente e tentar abrir diretamente a decisão financeira. | API nega acesso (HTTP 403); ausência de botão de decisão financeira para dependente. |
| 6 | Consultar reserva gratuita e uma reserva anterior após as alterações. | Fluxos existentes preservados; gratuidade sem cobrança e sem decisão financeira. |
| 7 | Executar `database/tests/013_financeiro_locacoes_validacao.sql`. | Consultas 2–7 retornam zero linhas; 1 e 8 mostram as operações efetuadas. |

**Atenção:** a decisão é permanente nesta etapa. Use duas reservas fictícias diferentes (uma para quitar e outra para transferir). O cancelamento financeiro de cobrança `Transferida` permanece bloqueado até a Etapa 5.

## Testes profundos para Codex

- Repetir a mesma quitação ou transferência pela API e conferir idempotência: nenhum novo item, lançamento ou acréscimo.
- Tentar escolher uma modalidade diferente após a primeira decisão: HTTP 409 e dados intactos.
- Testar reserva criada por dependente: apenas o **titular** pode decidir o pagamento.
- Tentar agir sobre reserva cancelada, gratuita ou após o instante exato do início; operação rejeitada e sem gravação.
- Confirmar que o sistema usa o fuso do clube prestador para exibir a utilização e o fuso do **clube de origem** para calcular a competência da mensalidade.
- Testar transferência para outra unidade e preservar a referência ao clube prestador nos itens/lançamentos sem expor faturas ao Tesoureiro externo (permissões da Etapa 6).
- Quitar simuladamente a mensalidade futura antes de uma nova transferência e confirmar que a API escolhe a próxima fatura elegível; a competência prevista deve ser mostrada e confirmada novamente.
- Simular alteração da competência elegível entre GET da prévia e POST: rejeitar a confirmação desatualizada, sem mover a cobrança silenciosamente.
- Duas decisões simultâneas da mesma cobrança não podem duplicar lançamento ou transferir após quitação.
- Conferir conservação do valor da taxa original em cobranças históricas, sem aplicar 10% mais de uma vez.
- Testar emissão de mensalidade após transferência para fatura `EmPreparacao`, incluindo pré-existência de fatura `EmAberto`.
- Conferir que quitação ou transferência não altera reservas existentes nem a cota de três reservas por associação.
- Testes de concorrência e injeção de falhas precisam ser feitos em **banco de teste isolado**, sem manipular registros pessoais ou faturas de demonstração.

Registrar resultados por ID em `docs/TESTES_PENDENTES_CLICKCLUB.md`. A execução profunda fica delegada ao Codex no fluxo aprovado, sem autorização para alterar código ou dados fora do escopo dos testes.

## Fora da Etapa 4

- Cancelamento com estorno e créditos após transferência: **Etapa 5**.
- Permissões de Tesoureiro e consulta interclubes: **Etapa 6**.
- Validação de uso do espaço pela portaria e carteirinha QR: **Componente 5**.
