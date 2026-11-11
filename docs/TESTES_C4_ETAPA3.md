# ClickClub — Componente 4, Etapa 3: testes iniciais

**Situação:** implementação preparada; build e testes no Windows ainda não executados neste ambiente.  
**Escopo:** quitação integral de mensalidades exclusivamente simulada, multa única por atraso e bloqueio de novas reservas para titular/dependentes.

## Preparação

1. Confirmar que `010_financeiro_base.sql` já foi aplicado. **Não executar novamente** e não criar migração nova.
2. Fazer backup lógico antes de operações de teste. Usar dados fictícios e, para cenários de atraso, preferir um **banco de testes separado**.
3. Executar `dotnet build ClickClub.slnx` na raiz. Iniciar API e Web nos terminais habituais.
4. Manter pelo menos uma mensalidade EmAberto de um titular de teste (os testes da Etapa 2 geraram faturas com vencimento em dezembro/2026, janeiro/2027 e fevereiro/2027).

## Testes mínimos do usuário

| ID | Ação | Resultado esperado |
| --- | --- | --- |
| 1 | Entrar como Titular, abrir Financeiro e consultar fatura ainda dentro do prazo. | Valor em **R$** e sem multa; permanece `EmAberto`. |
| 2 | Na fatura do teste 1, clicar em **Registrar quitação integral simulada**. | Situação `QuitadaSimulada`, data de quitação registrada, um lançamento negativo de quitação; sem multa antes do 11º dia. |
| 3 | Repetir requisição de quitação para a mesma fatura pela API, se possível, ou conferir pela Web que o botão sumiu. | API retorna `JaQuitada = true` e não cria outro lançamento nem altera total. |
| 4 | Entrar como Dependente, tentar abrir Financeiro ou endpoint de quitação. | Sem acesso financeiro (HTTP 403 pela API). |
| 5 | Consultar ou criar reservas normalmente com associação sem mensalidade inadimplente. | Fluxo de reservas previamente aprovado permanece funcional. |
| 6 | Executar `database/tests/012_financeiro_inadimplencia_validacao.sql`. | Consultas 2–6 retornam **zero linhas**; consultas 1 e 7 exibem somente dados históricos válidos. |

**Importante:** uma quitação simulada altera a fatura permanentemente. Utilize uma fatura **de teste**; não quite por engano as três faturas de demonstração criadas pelo Codex.

## Testes profundos para o Codex

- **Limite 10/11 dias:** uma mensalidade com vencimento no dia 10 deve permitir reserva no dia 20 (10 dias de atraso) e bloquear no dia 21 (11 dias). Calcular conforme o fuso do **clube de origem**, inclusive quando a reserva se destinar a outra unidade.
- **Base da multa:** fatura de R$ 260,00 (mensalidade R$ 150, locação transferida R$ 100 e acréscimo R$ 10) deve apresentar multa única de R$ 5,20 no 11º dia; quitação deve resultar em R$ 265,20, sem juros e sem repetir multa ou cobrança.
- **Titular e dependente:** ambos impedidos de criar novas reservas no 11º dia; ambos podem manter consultas e cancelar reservas existentes conforme regras atuais.
- **Liberação:** após quitação integral, titular e dependente voltam a poder fazer novas reservas, se não houver outras mensalidades inadimplentes.
- **Atrasos múltiplos:** quitar somente uma das duas faturas vencidas além da tolerância não libera a associação.
- **Concorrência:** duas quitações simultâneas da mesma fatura não podem criar pagamentos ou multas duplicadas.
- **Fatura quitada:** consultas posteriores preservam valor e itens históricos. A API não reabre nem altera faturas quitadas.
- **Credencial maliciosa:** titular de outra associação e dependente não podem quitar a fatura do primeiro titular.
- **Falha de transação:** nenhuma quitação parcial deve ser persistida se uma operação SQL falhar antes do commit.
- **Sem fatura elegível:** confirmar que uma taxa avulsa de reserva não bloqueia a associação por inadimplência de mensalidade.

Estes testes profundos foram registrados em `docs/TESTES_PENDENTES_CLICKCLUB.md` e não precisam ser concluídos antes de relatar o resultado da compilação inicial, mas **a autorização de avançar à Etapa 4 depende da avaliação do usuário**.

## Fora desta etapa

- Quitação avulsa e transferência de locação com acréscimo de 10%: **Etapa 4**.
- Estornos, créditos e cancelamentos financeiros integrados: **Etapa 5**.
- Permissões do Tesoureiro e relatórios entre clubes: **Etapa 6**.
- Validação de entrada na portaria: **Componente 5**.
