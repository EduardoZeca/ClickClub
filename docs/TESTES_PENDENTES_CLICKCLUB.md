# ClickClub — Testes profundos pendentes

**Atualizado em:** 11/11/2026 (Etapa 5 aceita nos testes iniciais; Etapa 6 preparada)  
**Objetivo:** acompanhar os testes aprofundados delegados ao Codex sem confundir implementação, compilação, aprovação do componente e verificação de integração.  
**Fontes:** `DOCUMENTACAO_CLICKCLUB.md` v1.8, `CONTINUIDADE_CLICKCLUB.md` e relatório do Codex `RELATORIO_C4_ETAPA2_2026-11-10.md` (fornecido pelo usuário).

## Método aprovado

1. **ChatGPT + usuário:** implementar incrementalmente componentes/etapas por ZIP, registrar decisões e entregar instruções de execução.
2. **Usuário:** no restante do Componente 4, aplicar arquivos/migrações, compilar e testar o fluxo principal. **Do Componente 5 em diante: apenas compilar como checagem imediata, por escolha do usuário.**
3. **Codex:** realizar os testes mais profundos registrados aqui, com ambiente local configurado e relatório de evidências; só popular dados fictícios quando a operação for autorizada.
4. **ChatGPT + usuário:** revisar resultados, corrigir problemas aprovados, atualizar este arquivo e a Continuidade. Testes futuros não são automaticamente considerados concluídos.

Não é obrigatório criar um commit a cada etapa para executar testes. O Codex não realizará `git commit`, `git push`, alterações de código, migrações, exclusões ou limpeza de dados por conta própria durante uma tarefa de **somente testes**.

### Estados

- `Pendente`: ainda não executado ou não há evidência suficiente.
- `Aprovado`: passou, com relatório e data registrados.
- `Falhou`: comportamento esperado não confirmado.
- `Bloqueado`: depende de componente ou ambiente ainda indisponível.
- `Não aplicável`: deixou de ser relevante mediante decisão documentada.

## Pendências atuais

| ID | Prioridade | Componente | Teste aprofundado / critério de aceite | Estado | Observação |
| --- | --- | --- | --- | --- | --- |
| TP-001 | Média | C4 / UI | Valores monetários em tabelas/detalhes devem mostrar `R$ 150,00` e cultura pt-BR; datas e mensagens devem ser compreensíveis em português. | Pendente | A Etapa 3 ajustou a formatação monetária das telas financeiras, ainda sem verificação; campos de data/mensagens nativas do navegador exigem avaliação separada. |
| TP-002 | Alta | C4 / Etapa 2 | Duas emissões simultâneas da mesma associação/competência devem resultar em apenas uma fatura-base e um lançamento de emissão. | Pendente | Codex não executou teste de concorrência. |
| TP-003 | Alta | C4 / Etapa 2 | Verificar associação suspensa e cancelada, inclusive emissão direta pela API e impedimento de nova cobrança para cancelada. | Pendente | Cobertura ausente no relatório; executar em registros fictícios isolados. |
| TP-004 | Alta | C1 / C4 | Restaurar em banco de teste separado um backup recente, conferir tabelas e registros e preservar o banco ativo. | Pendente | Backup de 10/11/2026 foi criado, mas não restaurado. |
| TP-005 | Alta | C3 / C4 | Criar e consultar reservas fictícias após instalação do financeiro, confirmando capacidade, cotas, cobrança EmAberto e ausência de regressão. | Pendente | Codex consultou reservas, mas não criou novas. |
| TP-006 | Baixa | C4 / UI | Confirmar tratamento visual adequado para a tentativa de titular B abrir a fatura de titular A; o servidor deve manter HTTP 404 sem revelar dados. | Pendente | Restrição na API passou; navegador integrado apresentou erro de navegação ao exibir o 404. |
| TP-007 | Alta | C4 / Etapa 3 | Multa única de 2% somente no 11º dia de atraso sobre a fatura total vencida, sem juros, sem repetição; bloqueio e reabilitação após quitação. | Pendente | Etapa 3 implementada em arquivos em 11/11/2026; nenhuma compilação/teste confirmados. Roteiro em `docs/TESTES_C4_ETAPA3.md`. |
| TP-008 | Alta | C4 / Etapa 4 | Quitação avulsa simulada e transferência única de locação com acréscimo de 10%, incluindo escolha do titular, prazo até o uso, fatura futura e idempotência. | Pendente | O usuário relatou "Testes Etapa 4 OK" em 11/11/2026. Teste profundo de idempotência, prazo e integridade ainda requer execução/evidência Codex. Ver `docs/TESTES_C4_ETAPA4.md`. |
| TP-009 | Alta | C4 / Etapa 5 | Cancelamentos, estorno de principal e 10% quando elegível, multa do salão e créditos automáticos sem duplicação nem fatura negativa. | Pendente | Usuário relatou "Testes OK" após a correção da migração 011, liberando a Etapa 6. Testes profundos de concorrência, saldos e estorno em fatura quitada ainda precisam de evidência Codex. |
| TP-010 | Alta | C4 / Etapa 6 | Escopo financeiro de Tesoureiro por clube: fatura no clube de origem e locações visíveis ao clube prestador, sem acesso indevido. | Pendente | Etapa 6 preparada em arquivos. Aplicação, build e validações de acesso ainda pendentes. Roteiro: `docs/TESTES_C4_ETAPA6.md`. |
| TP-011 | Alta | C5/C8 | Carteirinha QR com renovação/expiração, portaria online, bloqueios e funcionamento Android físico; sem liberação manual indevida. | Bloqueado | Componentes posteriores. |
| TP-012 | Alta | Integração final | Fluxo ponta a ponta dos perfis, unidades, adesões, reservas, financeiro, atividades, comunicação e Mobile; preparar cenário de demonstração reproduzível. | Bloqueado | Executar após implementação das partes relevantes. |
| TP-013 | Alta | C4 / Etapa 3 | Duas quitações simultâneas e repetidas devem produzir somente um lançamento de quitação e no máximo uma multa; transação deve ser integral. | Pendente | Verificar concorrência e rollback no banco de teste. |
| TP-014 | Alta | C4 / Etapa 3 | Associado com duas mensalidades inadimplentes permanece bloqueado após quitar somente uma; taxa de locação avulsa não bloqueia associação. | Pendente | Cenário profundo; requer faturas de teste vencidas. |
| TP-015 | Alta | C4 / Etapa 3–4 | Multa de 2% sobre fatura vencida com locações transferidas, sem capitalizar a multa ou aplicar juros. | Pendente | Etapa 4 preparada; criar cenário completo no banco de teste, incluindo mensalidade-base e transferência. |
| TP-016 | Alta | C4 / Etapa 4 | Transferência interclubes deve registrar locação e 10% na fatura da associação no clube de origem, identificando o clube prestador. | Pendente | Testar com dois clubes distintos e mensalidade de origem correta. |
| TP-017 | Alta | C4 / Etapa 4 | Escolha do titular após início da reserva, com reserva cancelada ou por dependente deve ser rejeitada, sem lançamentos. | Pendente | Testar com relógio controlado ou dados isolados no banco de teste. |
| TP-018 | Alta | C4 / Etapa 4 | Fatura futura quitada deve ser ignorada; se o destino mudar entre prévia e confirmação, exigir nova confirmação. | Pendente | Validar HTTP 409 para competência desatualizada e histórico da fatura quitada intacto. |
| TP-019 | Alta | C4 / Etapa 4 | Concorrência de duas decisões para uma mesma cobrança não pode duplicar principal/acréscimo, lançamento ou transferência. | Pendente | Testar quitação simultânea e quitação versus transferência. |
| TP-020 | Baixa | C3 / Histórico | Com várias reservas de datas de utilização fora da ordem de criação, `Minhas reservas` deve mostrar as **mais recentemente criadas primeiro**, com ID como desempate, para titular e dependente; não alterar agenda ou histórico financeiro. | Pendente | **Teste visual do usuário aprovado** em 11/11/2026. Profundidade adicional (ex.: dependente, timestamps iguais e outros fluxos) ainda não executada pelo Codex. |
| TP-021 | Média | C2 / Categoria | Categoria Individual: recusar cadastro e reativação de dependentes, troca de categoria para Individual com dependentes ativos e renomeação que produza essa condição. Preservar cadastros antigos sem alterar finanças. | Pendente | **Opção B aprovada expressamente** em 11/11/2026; correção preparada. Ver `docs/TESTES_CORRECAO_INDIVIDUAL.md`; build, testes do usuário e profundidade Codex ainda pendentes. |

## Já observado no relatório da Etapa 2 (não repetir sem motivo)

- Reemissão idempotente, políticas duplicadas, primeiro mês elegível, vencimento dia 10, valores históricos e consultas SQL 2–6: **aprovados conforme relatório Codex de 10/11/2026**.
- Restrição de faturas próprias ao titular e ausência de acesso do dependente: **aprovadas conforme o mesmo relatório**.
- Foram criadas somente entidades financeiras e associações fictícias declaradas no relatório; o Codex informou preservação dos registros anteriores.
- Os testes não cobriram concorrência, novas reservas, associações canceladas/suspensas nem restauração do backup.

## Regras para executar os testes pendentes

- Respeitar `DOCUMENTACAO_CLICKCLUB.md` e as decisões mais recentes da Continuidade; testes nunca autorizam mudanças de requisitos.
- Usar preferencialmente um banco **de teste separado**. Antes de qualquer gravação no banco ativo, exigir autorização específica e backup validado; nunca limpar o banco ativo automaticamente.
- Testar com dados fictícios identificáveis, sem incluir senhas, chaves, tokens, CPFs reais ou informações privadas em relatórios versionados.
- Gravar evidências temporárias em `test-artifacts/` ou `codex-output/` (ignorados pelo Git); apenas os roteiros e relatórios aprovados destinados à equipe devem entrar em `docs/`.
- Não ignorar nem excluir `database/scripts/`, `database/tests/` ou `docs/TESTES_PENDENTES_CLICKCLUB.md`.
- Ao terminar, informar **ID**, resultado, data, ambiente, procedimento, evidência, dados criados/alterados e pendências. Atualizar o estado do teste somente depois da revisão humana do relatório.

## Tarefa de preparação da demonstração (depois de finalizar a Web)

Por solicitação de 11/11/2026, o Codex deverá executar, em tarefa **separadamente autorizada**, um processo controlado:

1. Identificar os dados de teste existentes e confirmar exatamente quais podem ser removidos. Fazer backup e testar que o arquivo de backup está legível antes de limpar.
2. Limpar somente os registros de teste autorizados; não recriar o esquema, apagar dados reais, usuários administrativos necessários ou scripts/migrações.
3. Inserir dados fictícios coerentes para uma demonstração Web, com pelo menos dois clubes, perfis de acesso, espaços, reservas pagas/gratuitas, transferências, faturas e créditos. Nunca registrar credenciais de demonstração em arquivos versionados.
4. Gerar roteiro passo a passo da apresentação e relatório com IDs e contagens esperadas, indicando funcionalidades comprovadas e limitações restantes.

**Ainda não autorizado a executar exclusão/carga:** esta seção documenta intenção, não permissão operacional para alterações destrutivas ou carga de demonstração.

## Para preparar a apresentação ao professor

Após o fechamento das funcionalidades, preparar um conjunto de dados fictícios reproduzível e um roteiro de demonstração, contemplando pelo menos: Administrador Geral, Presidente, Tesoureiro, Titular, Dependente e Funcionário, múltiplos clubes, reservas gratuitas/pagas, faturas, atraso, locação transferida e um cenário de acesso negado. Registrar o que foi **efetivamente implementado e testado**; não simular funcionalidades ausentes.
