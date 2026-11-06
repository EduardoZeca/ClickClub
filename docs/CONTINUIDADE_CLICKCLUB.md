# Continuidade — Projeto ClickClub

**Data de criação:** 05/11/2026  
**Fase atual:** Planejamento concluído; projeto ainda não implementado.  
**Fonte oficial:** `DOCUMENTACAO_CLICKCLUB.md` v1.0.  
**Instruções do Project:** `INSTRUCTIONS_PROJECT_CLICKCLUB.md`.

## 1. Objetivo

Sistema acadêmico ClickClub para clube social com possibilidade de múltiplas unidades. Arquitetura: **MySQL central ↔ API ASP.NET Core ↔ Web ASP.NET Core MVC e Mobile .NET MAUI Android**, com SQLite local somente para cache/offline limitado. A API concentra regras e segurança. Sem nuvem paga, microsserviços, transações financeiras reais ou catraca física.

## 2. Estado comprovado

- [x] Escopo e arquitetura propostos e validados em conversa.
- [x] Requisitos funcionais, não funcionais e regras de negócio definidos.
- [x] Decisões P01–P14, F01–F05 e M01–M04 aprovadas.
- [x] Modelagem lógica inicial e relações consolidadas no documento-base.
- [x] Instruções personalizadas preparadas para o novo Project.
- [ ] Solução e repositório ClickClub criados.
- [ ] Banco MySQL criado.
- [ ] API/Web/Mobile implementados.
- [ ] Build ou testes de execução realizados.
- [ ] Instalação e testes Android físico realizados.

**Atenção:** Nenhum componente foi codificado ou testado. Não confundir requisitos aprovados com funcionalidades implementadas.

## 3. Decisões centrais

- Cadastro associativo central; unidades em municípios/estados diferentes; reservas entre unidades.
- Categorias iniciais Familiar, Individual e Atleta; espaços iniciais Salão, Churrasqueira e Quadra, com preços fictícios configuráveis.
- Perfis de titular, dependente, funcionário da unidade, administrador geral e porteiro.
- Até **3 reservas confirmadas por associação/mês de utilização**; prazo de salão e cancelamento de **24 horas exatas**; multa simulada limitada a **R$ 50 ou ao valor da locação, o que for menor**.
- Convidados informados até 24 horas antes, sem inclusão tardia.
- Uma mensalidade por titular/associação; bloqueio de titular/dependentes após **mais de 10 dias de atraso** na mensalidade.
- Todas as operações financeiras são simuladas. Locação pode ser quitada de forma simulada ou transferida para próxima mensalidade elegível com **10% uma única vez**; registrar itens separados e impedir duplicidade.
- QR Code no Android com validade de 24 horas, exibível offline, mas validação da portaria somente online pela API. Sem catracas ou liberação manual.
- Lista de espera de atividades com promoção manual por funcionário; impedir sobreposição de horários de inscrição. Torneios inicialmente apenas por inscrições.
- Mural e ouvidoria na primeira versão; push remoto para evolução posterior.

## 4. Ordem proposta dos componentes

1. Fundação: solução/projetos, esquema MySQL, scripts versionados, configuração segura, README e build inicial.
2. Autenticação e cadastros: perfis, unidades, titulares, dependentes, categorias e funcionários.
3. Espaços e reservas: horários, bloqueios, concorrência, convidados e cancelamentos.
4. Financeiro simulado: mensalidades, locações, transferências com 10%, cobranças, inadimplência e ajustes.
5. Carteirinha QR e portaria online, bloqueios e auditoria.
6. Atividades/turmas, vagas e lista de espera.
7. Avisos, ouvidoria e relatórios administrativos.
8. Mobile Android, cache/SQLite, integração ponta a ponta e testes em aparelho físico.
9. Complementos futuros somente mediante novo pedido: push remoto, validação offline pela portaria, integrações físicas.

## 5. Próxima ação autorizada

**Nenhuma implementação autorizada até o momento.** O usuário deverá criar/configurar o Project ClickClub com os documentos e solicitar expressamente o início do Componente 1.

No início do Componente 1, verificar versões disponíveis de .NET, Visual Studio/VS Code, MySQL e Android; confirmar ambiente de execução, portas e organização da solução antes de escrever código.

## 6. Como atualizar este arquivo ao longo do desenvolvimento

Para cada componente, acrescentar: data; arquivos criados/alterados; comandos executados; resultados de build/testes verificados; problemas encontrados; correções; pendências; próximo passo autorizado. Não inventar testes aprovados nem alterar os requisitos oficiais sem decisão do usuário.
