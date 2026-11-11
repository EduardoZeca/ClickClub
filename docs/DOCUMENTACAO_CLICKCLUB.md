# ClickClub — Documento de Requisitos e Planejamento

**Versão:** 1.8 — Escopos de Tesoureiro no financeiro simulado
**Data:** 11/11/2026

**Estado (11/11/2026):** Componentes 1, 2 e 3 concluídos conforme testes relatados pelo usuário. **Componente 4 — Financeiro Simulado concluído no desenvolvimento e nos testes iniciais relatados, incluindo a Etapa 6.** Etapa 2 teve verificações favoráveis do Codex; as evidências detalhadas de outras etapas e os testes profundos ainda estão pendentes. **Ordenação do histórico corrigida e testada; restrição de dependentes da categoria Individual aprovada e preparada para teste.** Os cenários de concorrência, segurança e integração permanecem em `TESTES_PENDENTES_CLICKCLUB.md`. A partir do Componente 5, a verificação imediata aprovada será apenas compilação; o Componente 5 requer autorização específica. A Continuidade separa implementação de evidências reais.

## 1. Visão geral

O ClickClub é um sistema para gestão de uma organização de clube social com várias unidades, cada uma apresentada e administrada como um clube (por exemplo, Foz 2 e Guaíra 2), em diferentes municípios e estados. O cadastro associativo continua centralizado e um associado pode utilizar serviços de outros clubes elegíveis. O sistema atenderá associados, dependentes, funcionários, porteiros, presidentes de clube, tesoureiros e administradores, contemplando cadastro, adesões sujeitas à aprovação, carteirinhas, reservas de espaços, atividades esportivas, eventos sociais, cobranças simuladas, comunicação e controle de acessos.

O objetivo inicial é um **projeto acadêmico funcional e demonstrável**, com arquitetura preparada para múltiplas unidades e sem obrigatoriedade de contratar serviços pagos. A primeira versão não equivale a um sistema homologado para controle físico de acesso ou operações financeiras reais.

**Diretriz confirmada:** todas as operações financeiras (mensalidades, taxas, multas, pagamentos e reembolsos) serão simuladas. Não haverá movimentação real de dinheiro. O controle de portaria será uma simulação por interface e QR Code, sem conexão ou testes de catraca física.

### 1.1 Arquitetura e premissas técnicas aprovadas

- Banco de dados oficial: **MySQL centralizado**.
- Backend: **API ASP.NET Core** com regras de negócio, autenticação, autorização e transações.
- Administração e portaria: **Web ASP.NET Core MVC**, consumindo exclusivamente a API.
- Associados: **Mobile .NET MAUI para Android**, consumindo a API. A portaria pode ser operada pela Web na primeira entrega.
- Persistência mobile local: **SQLite** para cache limitado e funcionamento offline especificado.
- Autenticação: senhas com hash e sal; tokens de sessão com armazenamento seguro no dispositivo; perfis e escopo de unidade verificados na API.
- Não há acesso direto Web–MySQL ou Mobile–MySQL. Não são necessários microsserviços nem infraestrutura paga para desenvolver a primeira versão.
- Instalação Android para testes via Visual Studio/ADB ou APK assinado. O aplicativo instalado não depende do notebook para abrir, mas as funções online exigem que a API esteja acessível pela rede.

## 2. Limites do escopo

### 2.1 Núcleo prioritário (primeira entrega)

1. Autenticação e autorização por perfil.
2. Cadastro de clubes/unidades, associados titulares, dependentes, presidentes, tesoureiros e funcionários; tipos de vínculo e solicitações de adesão aprovadas pela administração.
3. Cadastro de espaços e controle de reservas sem conflitos.
4. Emissão e quitação **simuladas** de faturas; regra de inadimplência.
5. Carteirinha digital com QR Code exibível offline no aparelho do sócio e validação online simulada pela portaria.
6. Inscrição em turmas com limite de vagas e lista de espera; cadastro de eventos sociais por público-alvo (somente associados ou público geral).
7. Avisos por unidade e geral, ouvidoria com solicitações e reclamações e relatórios administrativos essenciais.

### 2.2 Funcionalidades futuras ou fora do escopo inicial

- Notificações push remotas (integração com serviço de entrega de mensagens).
- Validação offline pela portaria, com sincronização posterior e controles de validade: **adiada, não incluída na primeira entrega**.
- Integração física ou testes com catracas, leitores dedicados, sistemas bancários ou emissão fiscal: **fora do escopo**.
- Torneios com chaves, resultados, classificação e arbitragem: **fora do escopo inicial**; inicialmente podem ser eventos com inscrições e vagas.
- Aplicativo iOS, armazenamento em nuvem obrigatório, gateway de pagamento e múltiplas organizações/franquias juridicamente independentes: **fora do escopo inicial**.
- **Contrato associativo formal anual**, gestão de vigência/renovação contratual, assinatura digital de contrato e módulo de eleições ou mandatos presidenciais de dois anos: **não serão implementados na primeira versão**. A ficha associativa simplificada é uma consulta do vínculo, não um contrato jurídico.
- Divulgação automática de eventos via WhatsApp, e-mail ou publicação em redes sociais e seleção obrigatória de clube na tela de login: **não serão implementadas**. Os avisos internos continuam previstos.

## 3. Conceitos e decisões estruturais

### 3.1 Clube/unidade

Cada unidade **representa um clube** para fins de cadastro, telas e administração local. Possui identificador, nome, sigla, endereço, bairro, município, UF, telefone, e-mail, fuso horário, situação (ativa/inativa), espaços, turmas, eventos, funcionários e avisos. O sistema deve suportar vários municípios e estados sem pressupor que todos os clubes tenham os mesmos horários, taxas, instalações ou atividades.

**Decisão aprovada (D01; complementada em 09/11/2026):** todos os clubes/unidades pertencem à mesma organização; o cadastro e a situação associativa são centrais. Cada associado possui um clube de origem, mas pode consultar serviços e solicitar reservas em qualquer clube/unidade habilitado, respeitando suas regras. Não se trata de múltiplas organizações juridicamente independentes.

### 3.2 Associado, dependente, categoria, tipo de vínculo e perfil

- **Titular:** pessoa responsável pela associação e pelas faturas do grupo familiar, quando aplicável.
- **Dependente:** pessoa vinculada a um titular, com direitos de uso definidos pela categoria e idade.
- **Categoria associativa:** familiar, individual, atleta ou outras configuráveis. Determina benefícios, tarifas e elegibilidade; não é uma permissão administrativa.
- **Tipo de vínculo associativo:** Associado/Fundador (com direito a voto), Convidado ou Simpatizante (ambos sem direito a voto). Representa condição associativa, não a categoria de benefícios nem um perfil de acesso.
- **Perfil de acesso:** titular, dependente, funcionário da unidade, presidente do clube, tesoureiro, administrador geral ou porteiro. Determina o que a conta pode fazer no sistema.

Categoria associativa, tipo de vínculo e perfil de acesso **são classificações distintas**. O administrador geral pode gerenciar categorias, mas a categoria “atleta” ou o tipo “fundador” não concede privilégios administrativos. Direito a voto será registrado como característica do tipo de vínculo; um módulo de eleições não integra o escopo.

**Categorias iniciais aprovadas:** Familiar, Individual e Atleta. Valores, benefícios, limites de dependentes e eventuais variações por unidade permanecem configuráveis, sem números presumidos nesta documentação.

**Restrição aprovada em 11/11/2026:** a categoria **Individual não admite novos dependentes nem reativação de vínculos inativos**. O administrador não pode trocar uma associação com dependentes ativos para Individual. Dependentes ativos vinculados antes da correção não serão excluídos, migrados nem terão cobranças modificadas automaticamente; o administrador deve regularizar o cadastro manualmente. A regra não altera, por inferência, os benefícios das categorias Familiar e Atleta.

**Tipos de vínculo iniciais aprovados em 09/11/2026:** Associado/Fundador, Convidado e Simpatizante. A inclusão desses tipos não gera descontos, isenções ou benefícios financeiros automáticos. Elegibilidade e valores continuam sujeitos às políticas associativas configuráveis. Dados cadastrais de pessoas devem contemplar CPF, endereço, bairro, cidade, UF, sexo, estado civil e interesses (múltiplos), além dos campos existentes. O acesso a dados pessoais permanece restrito por perfil e necessidade.

**Decisão aprovada (D04; complementada por D14):** dependentes podem possuir login próprio. Para **novas reservas de espaços pagos**, o dependente menor de 18 anos precisa de uma **permissão geral, individual e revogável**, concedida pelo titular da associação; não é exigida aprovação para cada reserva. Sem permissão ativa, a API bloqueia essa operação. Dependentes maiores de idade e reservas gratuitas não dependem dessa autorização. A revogação impede novas reservas pagas pelo menor, mas não cancela as já confirmadas. A cobrança simulada pertence à associação e a escolha de quitação ou transferência para mensalidade permanece exclusiva do titular; o dependente não obtém acesso ao financeiro do titular.

### 3.3 Cobrança e bloqueio

**Decisão aprovada (D02):** será emitida uma mensalidade por associação/titular, inclusive quando o plano familiar incluir dependentes, sem cobrar uma mensalidade adicional por cada unidade visitada. Mensalidade em aberto antes ou no vencimento **não** é inadimplência passível de bloqueio. O bloqueio atingirá titular e dependentes vinculados somente depois de ultrapassado o prazo de tolerância aprovado de **10 dias**. Pagamentos serão registrados como simulação, sem transações bancárias reais.

**Atualização aprovada:** `D = 10 dias` de tolerância após o vencimento, contados pela data local. Apenas mensalidades vencidas além desse prazo geram bloqueio associativo na primeira versão. A mensalidade terá vencimento no **dia 10**, com primeira competência no **mês seguinte à adesão**. Haverá **multa única de 2%, sem juros, a partir do 11º dia de atraso**, calculada sobre o **valor total vencido da fatura, incluindo locações transferidas**. A taxa de locação deve estar quitada de forma simulada **ou** transferida formalmente para a mensalidade futura elegível, com acréscimo de 10% uma única vez antes da utilização do espaço. A transferência, depois de incorporada à mensalidade, compõe o valor dessa cobrança e acompanha seu vencimento.

### 3.4 Disponibilidade e integridade

Conflitos de reserva e lotação devem ser verificados novamente pela API **dentro de uma transação no MySQL** no instante da confirmação. Mostrar um horário disponível na interface não garante a reserva até que a API confirme a operação.

### 3.5 Carteirinha exibível offline; validação de portaria online

**Não usar o JWT de login como QR Code de acesso.** A carteirinha deve possuir credencial de acesso distinta, com identificador, prazo de validade e assinatura digital verificável. O app do sócio pode exibir a credencial previamente obtida quando estiver sem rede, enquanto ela não estiver expirada.

**Validade aprovada (P12):** cada credencial QR tem validade de **24 horas** a partir da emissão; o aplicativo solicita renovação automaticamente quando houver conexão. Se expirar durante a falta de rede, não deve ser apresentada como uma credencial de entrada válida. A portaria permanece obrigatoriamente online para validação.

**Decisão aprovada (D11):** a primeira entrega exibirá o QR Code offline no Android do associado, mas a **portaria precisará de conexão para validá-lo na API**. A API verificará credencial, validade, vínculo, situação associativa, inadimplência e unidade, registrando permitido/negado. O controle será simulado por interface, sem catraca.

Uma futura validação offline na portaria exigirá dispositivo de porteiro com chave pública, cache de bloqueios, janela limitada de sincronização e posterior envio dos registros. **Isso não integra o escopo aprovado da primeira entrega.** Uma carteirinha exibida offline pode estar revogada no servidor; a decisão de acesso continua pertencendo à API.

### 3.6 Solicitação de adesão e aprovação (complementação de 09/11/2026)

- Uma pessoa poderá solicitar pelo aplicativo adesão ao clube de origem como **Convidado** ou **Simpatizante**; a interface Mobile será desenvolvida no Componente 8, com API e persistência preparadas no Componente 2.
- Associados/Fundadores serão cadastrados pela administração, não por autoinscrição no aplicativo na primeira versão.
- A solicitação terá pelo menos os estados `Pendente`, `Aprovada` e `Rejeitada`. A solicitação pendente não cria vínculo associativo ativo nem autoriza uso de serviços exclusivos de associados.
- Apenas o **Presidente do clube de origem** ou o **Administrador Geral** poderá aprovar ou rejeitar. Registrar responsável, instante e decisão para auditoria; o presidente não poderá aprovar solicitações de outro clube sem atribuição.
- O acompanhamento de solicitações no aplicativo será detalhado no componente correspondente, sem concessão antecipada de privilégios.

### 3.7 Eventos sociais (complementação de 09/11/2026)

Além de atividades esportivas, cada clube poderá cadastrar eventos sociais, como almoços, jantares e cafés, com descrição, início, término, situação e público-alvo `SomenteAssociados` ou `PublicoGeral`. Eventos abertos ao público não dispensam controle de acesso: a identificação de visitantes será detalhada no componente específico, preservando a validação online pela API e a proibição de liberação manual quando a API estiver indisponível. Não haverá bilheteria, venda de ingressos ou transações financeiras reais por iniciativa própria.

### 3.8 Ficha associativa, login e limites adicionais (decisões de 10/11/2026)

- **Ficha associativa simplificada, aprovada:** aproveitar `Pessoa`, `Associacao`, `DependenteAssociacao`, `CategoriaAssociativa` e demais dados existentes para exibir matrícula, titular, clube de origem, categoria, tipo de vínculo, data de início, status e dependentes. Quando o Componente 4 estiver implementado, a ficha poderá apresentar um resumo das mensalidades e seus vencimentos. Não criar uma entidade de contrato anual nem automatizar vencimento ou renovação contratual. A ficha não representa documento contratual formal.
- **Login híbrido, aprovado como ampliação futura:** a mesma conta poderá ser autenticada pelo login já cadastrado **ou** por seu e-mail, sempre com a senha existente. Preservar os logins atuais e não tornar e-mail obrigatório para contas existentes. A API só poderá usar o e-mail como identificador se ele determinar uma única conta elegível; colisões entre e-mails e nomes de usuário devem ser tratadas antes da ativação do recurso, sem autenticar arbitrariamente uma conta. A autorização por papel e clube permanece na API. Não exigir escolha prévia de clube no login.
- **Administração e divulgação:** preservar o perfil `PresidenteClube` e sua vinculação às unidades, sem controle de mandato bienal; não acrescentar integrações automáticas com WhatsApp, e-mail e mídias sociais. Utilizar o mural interno de avisos planejado.
- **Pontos ainda propostos, sem autorização de implementação:** profissão/qualificação, grau de parentesco, data/hora de cadastro e consulta da última alteração e responsável; filtro por sexo/gênero; possibilidade de novos tipos de espaços, incluindo quiosque; associação de eventos sociais à disponibilidade/ocupação de espaços no Componente 6; eventual cadastro de diretores. Detalhar escopo, permissões e impacto antes de convertê-los em requisitos aprovados.
- **Finanças (decisão concluída em 10/11/2026):** multa única de **2%** sobre o total vencido da fatura, incluindo locações transferidas, a partir do **11º dia de atraso**, **sem juros**. O bloqueio após o 10º dia e o acréscimo único de 10% por transferência de locação são regras distintas. Consulte RN33 e F06.

## 4. Perfis e permissões

| Perfil | Permissões previstas |
|---|---|
| Administrador geral | Gerenciar unidades, categorias, pessoal, associados, regras, financeiro geral, atividades, relatórios e auditoria. |
| Presidente do clube | Administrar cadastros e operações permitidas do(s) clube(s) atribuído(s); aprovar/rejeitar solicitações de adesão do respectivo clube. Não modifica políticas globais. |
| Tesoureiro | Consultar e administrar operações financeiras simuladas autorizadas dentro de seu escopo de clube, sem acesso global automático. |
| Funcionário da unidade | Gerenciar instalações, turmas, reservas e atendimento **somente das unidades atribuídas**. Não altera políticas globais. |
| Porteiro | Validar carteirinhas e convidados, consultar autorizações relevantes e registrar entradas/saídas da unidade atribuída. Não altera faturas. |
| Sócio titular | Consultar seu cadastro e dependentes, carteirinha, cobranças, reservas, atividades, avisos e chamados. |
| Dependente | Consultar a própria carteirinha, avisos, atividades e reservas permitidas, respeitando idade, categoria e autorização do titular. |

Toda autorização é conferida pela API; ocultar botões na interface não substitui segurança no servidor. A possibilidade de uma conta possuir mais de um papel ou funcionário atender duas unidades deve ser prevista pela modelagem de vínculos, mesmo que o primeiro protótipo use apenas um papel principal. **Administrador Geral** equivale ao superadministrador do enunciado; **Presidente** e **Tesoureiro** possuem papéis locais, sem acesso indistinto aos demais clubes. Titular, dependente, categoria e tipo de vínculo permanecem conceitos separados.

## 5. Requisitos funcionais

| ID | Descrição | Prioridade |
|---|---|---|
| **RF01** | Autenticar e autorizar os sete perfis de acesso previstos, com permissões por perfil e clube/unidade; permitir múltiplos papéis/atribuições conforme modelagem. | Essencial |
| **RF02** | Permitir ao associado visualizar e solicitar serviços e reservas de unidades diferentes da sua unidade de origem, conforme elegibilidade. | Essencial |
| **RF03** | Consultar agenda e reservar espaços por data e horário; apresentar taxa aplicável e impedir sobreposição de reservas confirmadas ou bloqueios de manutenção. | Essencial |
| **RF04** | Exibir carteirinha digital e QR Code previamente armazenado no mobile do associado, mesmo sem conexão no aparelho; credencial válida por 24 horas e renovada quando houver rede; a leitura/decisão na portaria será online pela API. | Essencial |
| **RF05** | Impedir novas reservas e novas autorizações de entrada de titular e dependentes quando a associação tiver mensalidade vencida há **mais de 10 dias**. A portaria não libera entradas bloqueadas ou sem validação online. | Essencial |
| **RF06** | Cadastrar, consultar, alterar, ativar e desativar clubes/unidades com identificação (incluindo sigla), endereço, bairro, município, UF, telefone, e-mail, fuso e horários próprios. | Essencial |
| **RF07** | Cadastrar, consultar, alterar e desativar titulares; registrar dados pessoais complementares e interesses; gerenciar categoria, tipo de vínculo, clube de origem e estado associativo. | Essencial |
| **RF08** | Vincular/desvincular dependentes ao titular e registrar data de nascimento e condição de autorização. | Essencial |
| **RF09** | Administrar funcionários, porteiros, presidentes e tesoureiros, com papéis e atribuições por clube/unidade. | Essencial |
| **RF10** | Cadastrar tipos de espaço, capacidades, horários, preços e períodos indisponíveis por unidade. Tipos iniciais: salão de festas, churrasqueira e quadra, com possibilidade de expansão; registrar finalidade da locação quando aplicável. | Essencial |
| **RF11** | Consultar reservas, cancelar conforme política e registrar o cálculo de taxa/multa/reembolso **simulado**. | Essencial |
| **RF12** | Cadastrar e editar a lista de convidados das reservas elegíveis até o limite estabelecido e permitir conferência na portaria. | Essencial |
| **RF13** | Gerar faturas de mensalidade por competência, com itens discriminados de mensalidade, taxas e locações transferidas, evitando duplicação da mesma cobrança. | Essencial |
| **RF14** | Registrar quitação simulada, consultar débitos, discriminar principal e acréscimos e apresentar histórico financeiro ao titular e ao administrador autorizado. | Essencial |
| **RF15** | Cadastrar atividades, turmas, instrutores (se utilizados), capacidade, datas, horários e unidade. | Essencial |
| **RF16** | Inscrever associados elegíveis em turmas/eventos, impedir excesso de vagas e disponibilizar lista de espera. | Essencial |
| **RF17** | Publicar avisos gerais e por unidade; permitir leitura no Web/Mobile. | Essencial |
| **RF18** | Criar e acompanhar chamados de ouvidoria diferenciados como `Solicitação` ou `Reclamação`, com categoria, status e histórico de respostas. | Essencial |
| **RF19** | Enviar notificações push remotas para dispositivos cadastrados, por público/unidade, utilizando serviço de entrega externo configurado. | Posterior |
| **RF20** | Validar online, em interface de portaria simulada, QR Code de carteirinha ou autorização de convidado; registrar decisão, motivo, data, hora, unidade e operador. Sem hardware de catraca. | Essencial |
| **RF21** | Disponibilizar painel com ocupação de espaços, faturas pagas/em aberto/vencidas, inscrições e totais por unidade/período. | Essencial (básico) |
| **RF22** | Registrar auditoria de eventos críticos: alterações de associados, reservas, faturas, permissões e validações de acesso. | Essencial |
| **RF23** | Permitir consulta de histórico de acessos e reservas, filtrado pelos direitos do usuário. | Essencial |
| **RF24** | Permitir que o titular escolha, até imediatamente antes do início do uso do espaço, quitar a taxa de locação de forma simulada **ou** transferi-la para a próxima mensalidade elegível, com acréscimo simples e único de **10%** sobre o valor original. Registrar a escolha e a competência de destino. Disponível para todos os espaços pagos. | Essencial; aprovado |
| **RF25** | Administrar separadamente os tipos de vínculo Associado/Fundador (com direito a voto), Convidado e Simpatizante (sem direito a voto), sem confundi-los com categorias associativas ou perfis de acesso. | Essencial; aprovado em 09/11/2026 |
| **RF26** | Receber pedidos de adesão de Convidados e Simpatizantes pelo Mobile, analisar na Web por Presidente do clube de origem ou Administrador Geral e registrar status e decisão. | Essencial; aprovado em 09/11/2026 |
| **RF27** | Cadastrar e consultar eventos sociais por clube, com início/fim, descrição, situação e público-alvo de associados ou público geral. | Essencial; aprovado em 09/11/2026 |
| **RF28** | Exibir na Web calendário/agenda de locações, incluindo reservas de períodos que atravessam datas e a finalidade da utilização; preservar privacidade dos responsáveis. | Essencial; aprovado em 09/11/2026 |
| **RF29** | Registrar e consultar interesses múltiplos dos associados, além dos dados pessoais complementares exigidos no cadastro. | Essencial; aprovado em 09/11/2026 |
| **RF30** | Disponibilizar ficha associativa simplificada com matrícula, situação, titular, categoria, clube de origem, início do vínculo e dependentes; acrescentar consulta resumida do financeiro quando esse componente existir. Não emitir ou renovar contrato anual. | Aprovado em 10/11/2026; implementação pendente |
| **RF31** | Aceitar autenticação pelo login existente ou pelo e-mail da mesma conta, preservando senha e perfis; rejeitar identificadores de e-mail ambíguos e não exigir seleção de clube no login. | Aprovado em 10/11/2026; implementação pendente |

## 6. Regras de negócio

| ID | Regra | Estado |
|---|---|---|
| **RN01** | Cada titular/associação possui limite de **3 reservas confirmadas para datas de utilização no mesmo mês-calendário**, somando titular, dependentes e todas as unidades. Reservas canceladas liberam a cota. | Aprovado |
| **RN02** | Reserva de salão exige pelo menos **24 horas exatas** de antecedência (X=1 dia). Cancelamento com pelo menos **24 horas exatas** antes do início (Y=1 dia) resulta em reembolso integral simulado; após esse marco, a multa simulada é o **menor valor entre R$ 50 e a taxa original da locação**, sem saldo negativo. | Aprovado |
| **RN03** | Convidados de salões/churrasqueiras devem estar cadastrados até 24 horas antes do evento. Se a reserva for feita exatamente no limite de antecedência, a lista completa deverá ser incluída na confirmação. **Não são permitidas inclusões/alterações após o prazo**, sem exceções na primeira versão. | Aprovado |
| **RN04** | Dependente menor de 18 anos só pode confirmar **nova reserva paga** se houver **permissão geral ativa do titular para aquele vínculo associativo**, registrada na API; a autorização é revogável e **não exige aprovação por reserva**. A ausência de data de nascimento exige autorização por precaução. Reservas gratuitas e reservas de dependentes maiores de idade não exigem essa permissão. Revogação não cancela reservas já confirmadas. A cobrança pertence à associação, e a gestão financeira continua exclusiva do titular. | Aprovado em 09/11/2026 — Opção B |
| **RN05** | Associado ativo pode solicitar serviços de outra unidade, respeitando bloqueios, categoria, idade, capacidade e regras locais. | Aprovado |
| **RN06** | A mensalidade é única por associação/titular e este responde por cobranças próprias e dos dependentes; evitar mensalidades duplicadas de um mesmo grupo familiar. | Aprovado |
| **RN07** | Mensalidade com **mais de 10 dias de atraso** bloqueia o titular e os dependentes vinculados; registro de pagamento simulado da mensalidade regulariza o bloqueio. Taxas avulsas ainda não incorporadas à mensalidade não causam esse bloqueio associativo. | Aprovado |
| **RN08** | Associação suspensa/cancelada ou credencial revogada não deve receber autorização de acesso. A portaria sempre consulta a API; o QR exibido offline não concede autorização sem conferência online. **Porteiro não possui liberação manual quando a API está indisponível ou quando há bloqueio.** | Aprovado para primeira entrega |
| **RN09** | Reservas devem respeitar funcionamento, manutenção, capacidade, disponibilidade, elegibilidade e fuso horário da unidade. | Aprovado |
| **RN10** | Duas reservas confirmadas para o mesmo espaço não podem possuir períodos sobrepostos. A API deve impedir concorrência na confirmação. | Aprovado |
| **RN11** | Vagas de turmas são exclusivas por associado; inscrição duplicada é rejeitada. A lista de espera segue ordem cronológica; **funcionário promove manualmente o primeiro elegível** quando surgir vaga. | Aprovado |
| **RN12** | A API deve **impedir inscrições do mesmo associado em turmas com horários sobrepostos**, considerando o período de realização das aulas, e informar o conflito. | Aprovado (M03) |
| **RN13** | Toda alteração crítica deve registrar usuário, operação, entidade, data/hora e unidade relacionada, quando houver. | Aprovado |
| **RN14** | Apenas funcionários designados podem modificar dados da respectiva unidade; administrador geral pode atuar em todas. | Aprovado |
| **RN15** | Taxas e políticas podem variar entre unidades. Uma reserva conserva o preço e condições aplicados no instante da confirmação. | Aprovado |
| **RN16** | Mensalidades, taxas, pagamentos, restituições e multas são **exclusivamente registros simulados**, sem débito em cartão, PIX, boleto ou transferência real. | Aprovado |
| **RN17** | Convidado é autorizado exclusivamente no contexto da reserva/evento e horário, após conferência de identidade pela portaria. | Aprovado |
| **RN18** | O titular pode optar pela quitação simulada ou transferência da locação **até imediatamente antes do início do uso**. A transferência está disponível para qualquer espaço pago e aplica **10% uma única vez** sobre o valor original, sem capitalização ou novo acréscimo por mês. | Aprovado |
| **RN19** | Uma locação transferida deve gerar cobrança discriminada na fatura de destino: principal + 10% de acréscimo. A cobrança original deixa de ser exigível separadamente; quitação e transferência devem ser idempotentes, sem duplicação. | Aprovado |
| **RN20** | Mensalidade já quitada não pode ser alterada retroativamente para incluir uma locação. Se a mensalidade da competência subsequente já estiver quitada, a API escolhe a **primeira mensalidade futura elegível** e informa competência e vencimento ao titular antes da confirmação. | Aprovado |
| **RN21** | Se uma reserva com locação transferida for cancelada antes do uso, ajustar os itens de uma mensalidade ainda aberta, zerando a cobrança no prazo de reembolso integral ou mantendo apenas a multa cabível. Se a fatura já estiver quitada, registrar crédito simulado para compensação futura sem alterar o histórico quitado. | Aprovado |
| **RN22** | O QR Code da carteirinha expira **24 horas** após a emissão. Quando online, o Mobile renova a credencial; quando offline, pode exibir a última credencial e seu estado, mas não indicará uma credencial vencida como válida. | Aprovado |
| **RN23** | Torneios esportivos na primeira versão funcionam como eventos com inscrição e limite de vagas. Chaves, placares, classificação e arbitragem ficam para evolução futura. | Aprovado |
| **RN24** | Para a administração do sistema, cada unidade representa um clube da mesma organização. O cadastro associativo é centralizado; o Presidente e o Tesoureiro atuam somente no(s) clube(s) atribuído(s), e o Administrador Geral tem escopo global. | Aprovado em 09/11/2026 |
| **RN25** | Tipo de vínculo, categoria de benefícios e perfil de acesso são independentes. Somente Associado/Fundador possui direito a voto. O tipo de vínculo não concede, por si, desconto, isenção ou acesso administrativo. Não há módulo de votação na primeira versão. | Aprovado em 09/11/2026 |
| **RN26** | Autoinscrição pelo aplicativo é permitida inicialmente apenas para Convidado e Simpatizante; Associado/Fundador é cadastrado pela administração. Pedido pendente não ativa associação. Somente Presidente do clube solicitado ou Administrador Geral decide, com registro de auditoria. | Aprovado em 09/11/2026 |
| **RN27** | Reserva que atravesse dois meses-calendário conta apenas para a cota de **3 reservas** do mês da **data local de início**, no fuso horário do clube onde ocorrerá a utilização; cancelar libera a cota desse mês. | Aprovado em 09/11/2026; complemento de RN01 |
| **RN28** | Eventos sociais podem ser destinados somente a associados ou ao público geral. Evento aberto não equivale a liberação automática de entrada: eventual conferência de visitante continua sujeita à API online e não há liberação manual sem validação. | Aprovado em 09/11/2026 |
| **RN29** | A duração de uma reserva pode atravessar a meia-noite e datas diferentes, desde que `Fim > Início` e sejam respeitadas disponibilidade, funcionamento, política e fuso da unidade. A agenda Web apresenta ocupação sem divulgar dados pessoais indevidos. | Aprovado em 09/11/2026 |
| **RN30** | A ficha associativa é uma apresentação dos dados do vínculo existente, sem criar contrato formal, número contratual autônomo, vigência anual ou obrigação de renovação. A matrícula continua sendo o identificador associativo. | Aprovado em 10/11/2026 |
| **RN31** | No login híbrido, um e-mail informado precisa identificar inequivocamente uma conta; nomes de usuário existentes continuam válidos. A API não pode resolver colisão entre identificadores por escolha arbitrária. O escopo de clube é determinado por papéis e atribuições após autenticação. | Aprovado em 10/11/2026 |
| **RN32** | Mensalidade por associação tem vencimento no **dia 10**; a primeira competência é o **mês seguinte ao início da adesão**. Valores são configuráveis por categoria/clube de origem e não são presumidos por este documento. | Aprovado em 10/11/2026 |
| **RN33** | Sobre a **fatura vencida**, aplicar **multa única de 2%** a partir do **11º dia de atraso**, calculada sobre o **valor total vencido**, incluindo os itens de locações transferidas, **sem qualquer juros**. Não reaplicar a multa em dias ou meses posteriores e não capitalizar valores. O acréscimo único de 10% pela transferência da locação é distinto dessa multa. | Aprovado integralmente em 10/11/2026 |
| **RN34** | Ao cancelar locação com direito a restituição, reverter também o acréscimo único de **10%** da transferência, deduzindo somente a multa de cancelamento cabível. Se a fatura estiver quitada, preservar seus itens e gerar crédito simulado futuro; se aberta, ajustar a cobrança. Créditos serão compensados automaticamente na próxima fatura elegível, com saldo remanescente transportado para futuras competências. | Aprovado em 10/11/2026 |
| **RN35** | Tesoureiro do clube de origem administra as faturas de suas associações. Tesoureiro do clube prestador consulta somente o lançamento de locação originado em seu clube, sem acesso irrestrito à fatura familiar de outra origem. Administrador Geral mantém acesso global. | Aprovado em 10/11/2026 |

### 6.1 Cálculo de atraso aprovado

Calcular `diasEmAtraso` a partir da **data local do vencimento**, sem arredondar horas de forma inconsistente. Bloquear titular e dependentes somente se `diasEmAtraso > 10`, isto é, a partir do **11º dia de atraso**. A API recalcula a elegibilidade na tentativa de reserva/entrada, sem depender exclusivamente de tarefa automática diária. Apenas mensalidades vencidas acima desse limite bloqueiam a associação na primeira versão; cobranças de locação transferidas passam a integrar a mensalidade de destino.

**Encargo aprovado integralmente em 10/11/2026:** multa única de **2%** sobre o **total vencido da fatura**, incluindo principal de mensalidade e locações transferidas, **a partir do 11º dia** de atraso, **sem juros**. No 10º dia não há multa nem bloqueio por atraso; no 11º dia ambos se aplicam. A multa não se repete por dia, mês ou tentativa de quitação. Exemplo ilustrativo: uma fatura de R$ 260,00 vencida há 11 dias recebe R$ 5,20 de multa única, totalizando R$ 265,20, sem juros adicionais.

### 6.2 Fluxo proposto das reservas

1. Titular ou dependente autenticado escolhe unidade, espaço, dia e horário; interface consulta disponibilidade.
2. API valida perfil, associação, autorização geral para menor em espaço pago (RN04), inadimplência quando o módulo financeiro estiver integrado, horário, limites e conflitos dentro de transação.
3. **Decisão aprovada (D09):** reserva é confirmada imediatamente, com preço e política registrados, e é criada taxa simulada em aberto quando cabível. A confirmação não aguarda quitação simulada.
4. Até imediatamente antes do início da utilização, o titular registra quitação simulada ou solicita transferência da locação para a mensalidade elegível com acréscimo único de 10%.
5. Titular consulta e pode cancelar reservas da própria associação; cada dependente consulta e pode cancelar apenas as reservas feitas por si. A API calcula eventual multa/reembolso simulado e libera o horário.
6. Em salões/churrasqueiras, o titular ou o dependente responsável informa convidados até 24 horas antes; no limite da reserva, a lista completa é exigida na confirmação. O titular também pode administrar os convidados das reservas da associação.

**Regra de uso aprovada:** taxa de locação em aberto não impede a confirmação inicial. Até antes do início do evento, o acesso **ao espaço reservado** exige quitação simulada ou transferência registrada; isso é diferente do acesso geral ao clube, que depende da situação associativa e da mensalidade. Se houver mensalidade vencida por mais de 10 dias, aplica-se o bloqueio geral.

### 6.3 Transferência de locação para a mensalidade

Exemplo: Lucas reservou salão por **R$ 100,00** e, antes da utilização, escolheu incluir a locação na próxima mensalidade.

- Valor original da locação: **R$ 100,00**.
- Acréscimo único de 10%: `100,00 × 0,10 = R$ 10,00`.
- Total transferido: **R$ 110,00**.
- Se a mensalidade normal for **R$ 150,00**, o valor simulado da próxima fatura será **R$ 260,00** (`150,00 + 100,00 + 10,00`).

O valor deve aparecer com **itens separados** (`Aluguel do salão` e `Acréscimo por transferência — 10%`), vinculados à reserva. Os 10% serão cobrados **uma única vez, sem capitalização**, e não representarão juros mensais recorrentes. A opção financeira pertence ao **titular**, mesmo que o espaço tenha sido reservado por dependente autorizado; o porteiro apenas verifica a situação na API.

Transferências só serão concluídas pela API após verificar que o débito não foi quitado ou transferido antes e que a fatura de destino não está fechada/quitada. Se a próxima mensalidade já estiver quitada, a API deve localizar a primeira mensalidade futura elegível, sem reabrir ou alterar a anterior. O aplicativo deve mostrar antecipadamente o principal, acréscimo, total, competência de destino e vencimento para confirmação do titular. A escolha pode ocorrer no Mobile ou na Web com autenticação do titular, até imediatamente antes do início do uso do espaço.

Se a reserva for cancelada depois de transferida, mas antes do uso, uma fatura ainda aberta poderá ter os respectivos itens ajustados conforme a multa aplicável; uma fatura quitada permanece intacta e o estorno será lançado como crédito simulado em cobrança futura. O registro de histórico e auditoria deve permitir rastrear a reserva e evitar dupla restituição.

**Complementação aprovada em 10/11/2026:** a reversão da locação cancela também os 10% do acréscimo, mantendo apenas eventual multa de cancelamento. Exemplo: locação de R$ 100 transferida por R$ 110; cancelamento com restituição integral gera reversão/crédito de R$ 110; cancelamento tardio de salão sujeito à multa de R$ 50 gera reversão/crédito líquido de R$ 60. Créditos são abatidos automaticamente da próxima fatura elegível, mantendo saldo remanescente para outras competências e sem reabrir fatura quitada. Os valores são exemplos de cálculo, não tarifas obrigatórias.

### 6.4 Fluxo proposto da portaria

1. Porteiro autentica-se na unidade e abre a tela de validação.
2. Lê o QR Code ou pesquisa manualmente a associação/convidado.
3. A API, obrigatoriamente online, confere assinatura, identidade, validade, unidade, associação, inadimplência e demais regras. Para **uso de espaço alugado**, também confere se a taxa foi quitada de forma simulada ou transferida; o débito avulso ainda aberto não bloqueia, por si só, a entrada geral no clube.
4. O sistema retorna **Permitido** ou **Negado** com motivo apropriado e registra o evento.
5. Se a portaria estiver sem conexão, a validação não é concluída como autorizada; o sistema deve informar indisponibilidade da conferência. **Não há liberação manual de exceção na primeira versão.**

## 7. Requisitos não funcionais

| ID | Requisito | Critério de verificação proposto |
|---|---|---|
| **RNF01 — Arquitetura** | MySQL só é acessado pela API; Web e Mobile consomem endpoints autenticados. | Inspeção das dependências e execução. |
| **RNF02 — Segurança** | Hash seguro para senhas, autorização no servidor, expiração de sessão, validação de entrada e consultas SQL parametrizadas. | Tentativas de login/acesso não autorizado rejeitadas. |
| **RNF03 — Segredos** | Senhas do banco, chaves de assinatura e credenciais externas fora do Git e do código-fonte. | Verificação de configuração e repositório. |
| **RNF04 — Integridade** | Operações financeiras (inclusive transferência de locação), confirmação de reserva e ocupação de vaga devem ser transacionais e resistentes a concorrência. | Testes de duas solicitações simultâneas e de transferência repetida. |
| **RNF05 — Offline e armazenamento** | O Mobile guarda apenas dados indispensáveis em SQLite e protege segredos em armazenamento seguro do sistema operacional. | Teste sem rede e inspeção de cache. |
| **RNF06 — Consistência** | O Android pode exibir QR armazenado offline, mas a portaria deve validar estado atualizado na API. Credencial expirada ou sem acesso ao servidor não equivale a autorização de entrada. | Testes de QR válido/expirado e portaria sem conexão. |
| **RNF07 — Usabilidade** | Interfaces em português do Brasil, mensagens compreensíveis, campos obrigatórios claros e telas adaptadas a celulares. | Checklist de navegação Web/Android. |
| **RNF08 — Desempenho** | Consultas comuns e confirmações devem responder em tempo adequado para demonstração; definir meta quantitativa após determinar o ambiente de teste. | Medição com conjunto de dados e carga definidos. |
| **RNF09 — Auditoria** | Registrar operações críticas sem registrar senhas, tokens completos ou dados pessoais desnecessários. | Conferir trilha de eventos. |
| **RNF10 — Recuperação** | Procedimento documentado de backup e restauração do MySQL e configuração reprodutível do projeto. | Restaurar cópia em ambiente de teste. |
| **RNF11 — Manutenibilidade** | Organização por camadas/projetos, migrações SQL versionadas, documentação e Git. | Nova instalação a partir do README. |
| **RNF12 — Portabilidade** | Mobile Android físico; versão mínima do Android e versão final do .NET definidas conforme ambiente de desenvolvimento disponível. | Compilação e instalação no aparelho escolhido. |
| **RNF13 — Privacidade** | Coletar somente dados pessoais necessários, inclusive CPF, endereço, sexo, estado civil e interesses previstos no cadastro; limitar leitura por perfil e definir retenção de documentos, solicitações de adesão, convidados e logs. | Revisão das telas, endpoints e tabelas. |
| **RNF14 — Datas e horários** | Guardar instantes com referência de fuso e interpretar agendas na hora local de cada unidade. | Testes com unidades em fusos distintos. |

## 8. Modelagem lógica inicial validada

**Identidade e organização**

- `Unidade` (clube): Id, Nome, Sigla, Endereco, Bairro, Municipio, UF, Telefone, Email, FusoHorario, Status.
- `Pessoa`: Id, Nome, DataNascimento, DocumentoIdentificacao/CPF, Endereco, Bairro, Cidade, UF, Sexo, EstadoCivil, Telefone, Email, Status.
- `Usuario`: Id, PessoaId, Login, SenhaHash, Ativo.
- `PapelUsuario`: UsuarioId, Papel, UnidadeIdOpcional (escopo de funcionário, porteiro, presidente e tesoureiro).
- `CategoriaAssociativa`: Id, Nome, RegrasBeneficios, Ativa.
- `TipoVinculoAssociativo`: Id, Nome, DireitoVoto, Ativo (iniciais: Associado/Fundador, Convidado e Simpatizante).
- `Associacao`: Id, TitularPessoaId, CategoriaId, TipoVinculoId, UnidadeOrigemId, Matricula, Status, DataInicio.
- `DependenteAssociacao`: Id, AssociacaoId, DependentePessoaId, Ativo.
- `FichaAssociativa` **não é tabela**: visualização composta a partir da associação e de seus relacionamentos. O login híbrido reaproveita `Usuario.Login` e `Pessoa.Email`; a estratégia de unicidade/colisões será implementada e testada em ajuste autorizado, sem modificar automaticamente os cadastros existentes.
- `Interesse`: Id, Nome, Ativo; `PessoaInteresse`: PessoaId, InteresseId (múltiplos interesses por pessoa).
- `SolicitacaoAdesao`: Id, PessoaId, UnidadePretendidaId, TipoVinculoId, Status, SolicitadaEm, AnalisadaEmOpcional, AnalisadaPorUsuarioIdOpcional, MotivoOpcional. A pessoa pode existir com situação cadastral pendente, sem associação ativa.

**Instalações e reservas**

- `Espaco`: Id, UnidadeId, Nome, Tipo, Capacidade, TaxaBase, Ativo.
- `HorarioEspaco`: Id, EspacoId, DiaSemana, Inicio, Fim.
- `IndisponibilidadeEspaco`: Id, EspacoId, Inicio, Fim, Motivo.
- `Reserva`: Id, EspacoId, AssociacaoId, ResponsavelPessoaId, Inicio, Fim, FinalidadeOpcional, ValorContratado, Status, CriadaEm. O mês da cota de RN01 é o da data local de início.
- `ConvidadoReserva`: Id, ReservaId, Nome, IdentificacaoMinima, Situacao.
- `AutorizacaoReservaDependente`: Id, DependenteAssociacaoId (único), PermitirReservaPaga, TitularUsuarioId, AtualizadoEmUtc. Permissão geral do vínculo, não vinculada a uma reserva específica; histórico de concessão e revogação registrado em auditoria.

**Financeiro**

- `Fatura`: Id, AssociacaoId, UnidadeOrigemId, PoliticaFinanceiraIdOpcional, Tipo, Competencia, DataVencimento, ValorTotal, Status.
- `ItemFatura`: Id, FaturaId, Tipo, Descricao, Valor, ReservaIdOpcional, OrigemUnidadeIdOpcional (mensalidade base, principal de locação, acréscimo, outros).
- `CobrancaReserva`: Id, ReservaId, ValorOriginal, Status (EmAberto/QuitadaSimulada/Transferida), Modalidade, CompetenciaDestinoOpcional, FaturaDestinoIdOpcional, PercentualAcrescimo, ValorAcrescimo, DataDecisao.
- `LancamentoFinanceiro`: Id, FaturaIdOpcional, CobrancaReservaIdOpcional, Tipo, Valor, DataHora, UsuarioResponsavelId, Observacao.
- `PoliticaFinanceira`: Id, UnidadeId, CategoriaId, ValorMensal, DiaVencimento, DiasTolerancia, PercentualMulta, VigenciaInicio, VigenciaFimOpcional.
- `CreditoFinanceiro`: Id, AssociacaoId, CancelamentoReservaIdOpcional, ValorOriginal, SaldoDisponivel, CriadoEm. Controla compensações parciais sem duplicidade.

**Atividades e comunicação**

- `Atividade`: Id, Nome, Descricao, Tipo.
- `Turma`: Id, AtividadeId, UnidadeId, InstrutorPessoaIdOpcional, Inicio, Fim, Capacidade, Status.
- `InscricaoTurma`: Id, TurmaId, PessoaId, Status, InscritaEm, PosicaoEsperaOpcional.
- `EventoSocial`: Id, UnidadeId, Nome, Descricao, Inicio, Fim, PublicoAlvo (SomenteAssociados/PublicoGeral), Status.
- `Aviso`: Id, UnidadeIdOpcional (nulo = geral), Titulo, Corpo, Inicio, Fim, AutorUsuarioId.
- `ChamadoOuvidoria`: Id, PessoaId, UnidadeIdOpcional, Tipo (Solicitacao/Reclamacao), Assunto, Descricao, Status, CriadoEm.
- `RespostaChamado`: Id, ChamadoId, AutorUsuarioId, Conteudo, DataHora.

**Portaria, notificações e auditoria**

- `CredencialCarteirinha`: Id, PessoaId, IdentificadorPublico, EmitidaEm, ValidaAte (= EmitidaEm + 24 horas), RevogadaEmOpcional, Versao.
- `RegistroAcesso`: Id, UnidadeId, PessoaIdOpcional, ConvidadoIdOpcional, PorteiroUsuarioId, DataHora, Resultado, Motivo. A primeira versão registra somente validações online.
- `Dispositivo`: Id, UsuarioId, Identificador, TokenPushOpcional, UltimaSincronizacao.
- `LogAuditoria`: Id, UsuarioId, UnidadeIdOpcional, Acao, Entidade, IdentificadorEntidade, DataHora, ResumoAlteracao.

### 8.1 Regras de modelagem

- Usar chave primária estável e chaves estrangeiras para relacionamentos.
- `UnidadeId` em entidades vinculadas a instalações/serviços locais; não repetir `UnidadeId` em todas as tabelas sem necessidade.
- Em relatórios de reservas/financeiro, preservar valores e políticas históricos para que alterações futuras de preço não reescrevam o passado.
- Preferir desativação e estados a exclusões físicas de dados que possuem reservas, faturas ou auditoria.
- Garantir identificador único para matrícula da associação e evitar duplicação da fatura por associação/competência/tipo conforme política.
- Garantir que cada reserva tenha no máximo uma cobrança principal ativa e que cada transferência crie exatamente um item principal e um item de acréscimo na fatura de destino; não alterar faturas já quitadas.
- A transferência registra a mudança da obrigação do débito avulso para a mensalidade, preservando o histórico e evitando cobrar o principal duas vezes.
- Tratar simultaneidade de reservas e vagas dentro da API e do banco, não apenas por validação de tela.
- Não usar dados completos do titular ou informações financeiras dentro do QR Code; privilegiar identificadores opacos e credencial assinada.
- Dados iniciais configuráveis: categorias **Familiar, Individual e Atleta**; espaços **Salão de Festas, Churrasqueira e Quadra**. Não presumir preços ou benefícios reais.
- Inscrições em lista de espera devem conservar ordem de entrada e registrar o funcionário que promoveu o associado após liberação de vaga.
- Cada pessoa poderá figurar como titular de **no máximo uma associação ativa** e como dependente de **no máximo uma associação ativa**, com regras de exclusividade verificadas na API e no banco conforme possível.
- Cada reserva paga terá uma cobrança principal identificável e idempotente; se transferida, o principal e o acréscimo de 10% devem aparecer nos itens da fatura de destino sem débito paralelo.
- Preservar as sete tabelas já criadas no Componente 1 e evoluir o banco com novos scripts versionados; os nomes e restrições das novas estruturas serão confirmados após inspeção de `001_identidade_associativa.sql`, sem reescrever o script histórico.
- Garantir identificação consistente de CPF, sigla de clube e matrícula, com unicidade pertinente; os interesses serão vinculados por relacionamento múltiplo, evitando concatenar valores em texto.
- `TipoVinculoAssociativo` não substitui `CategoriaAssociativa` nem `PapelUsuario`. Somente Associado/Fundador terá o indicador `DireitoVoto`; esse indicador não autoriza funções administrativas.
- Solicitação de adesão pendente/rejeitada não pode gerar associação ativa, autorização de entrada ou acesso aos serviços exclusivos de associado. A decisão deve ser autorizada para o clube pretendido e auditada.
- Ao aplicar RN01, considerar a **data local de início** da reserva, inclusive em períodos que atravessem meses, preservando bloqueio de conflitos pelo intervalo completo.
- Diferenciar eventos sociais de turmas esportivas; visitantes de eventos abertos não são automaticamente associados, e a portaria continua online.

### 8.2 Relacionamentos consolidados

| Origem | Relação | Destino | Regra |
|---|---|---|---|
| `Unidade` | 1:N | `Espaco`, `Turma` | Espaços e turmas pertencem à unidade onde são oferecidos. |
| `Unidade` | 1:N | `Associacao` | Unidade de origem; não restringe reservas em outras unidades. |
| `Unidade` | 1:N | `EventoSocial`, `SolicitacaoAdesao` | Eventos e pedidos de adesão pertencem ao clube responsável/pretendido. |
| `Pessoa` | 1:0..1 | `Usuario` | Pessoas com acesso ao sistema recebem credenciais e papel(is) definidos. |
| `Pessoa` | N:N | `Interesse` (`PessoaInteresse`) | A pessoa pode registrar vários interesses e cada interesse pode servir a várias pessoas. |
| `Pessoa` | 1:N | `SolicitacaoAdesao` | Histórico de pedidos pendentes, aprovados e rejeitados, sem ativação automática. |
| `Associacao` | N:1 | `Pessoa` titular | Uma associação possui um titular e o titular tem, no máximo, uma associação ativa. |
| `Associacao` | 1:N | `DependenteAssociacao` | Cada vínculo referencia uma pessoa dependente e não pode coexistir em duas associações ativas. |
| `Associacao` | N:1 | `CategoriaAssociativa` | A categoria define regras e preço-base configuráveis. |
| `Associacao` | N:1 | `TipoVinculoAssociativo` | Tipo de vínculo define condição associativa e direito a voto, sem substituir categoria ou perfil. |
| `Associacao` | 1:N | `Reserva`, `Fatura` | Cota de reservas e cobranças são controladas pelo grupo associativo. |
| `Espaco` | 1:N | `Reserva` | Reservas confirmadas do mesmo espaço não podem se sobrepor no tempo. |
| `Reserva` | 0..1:1 | `CobrancaReserva` | Reservas gratuitas não exigem cobrança; locações pagas possuem uma. |
| `Reserva` | 1:N | `ConvidadoReserva` | Apenas espaços/eventos que aceitam convidados utilizam a lista. |
| `Fatura` | 1:N | `ItemFatura` | Mensalidade-base, locações e acréscimos são discriminados. |
| `CobrancaReserva` | 1:0..N | `ItemFatura` | Se transferida, a cobrança origina itens de principal e 10% vinculados à mesma reserva. |
| `Atividade` | 1:N | `Turma` | Uma atividade pode ser oferecida em diferentes turmas/unidades. |
| `Turma` | 1:N | `InscricaoTurma` | Inscrições respeitam lotação, lista de espera e conflito de horários. |
| `Pessoa` | 1:N | `CredencialCarteirinha` | Histórico de credenciais com validade de 24 horas; somente a válida pode ser usada. |
| `Unidade` | 1:N | `RegistroAcesso` | Cada acesso pertence à unidade e identifica associado ou convidado e o operador. |

As cardinalidades descrevem o **modelo lógico**. A implementação definirá nomes exatos de índices, estados, migrações e restrições, preservando os comportamentos aprovados.

## 9. Telas iniciais sugeridas

### 9.1 Mobile — Sócio titular/dependente

1. Solicitação de adesão (Convidado/Simpatizante) e acompanhamento de situação, sem acesso associativo antecipado; login para contas autorizadas.
2. Início: status associativo, unidade preferida, avisos e próximas reservas.
3. Minha carteirinha: QR Code com validade de 24 horas, renovação automática quando online e aviso de expiração quando sem rede.
4. Clubes/unidades e espaços: filtros, disponibilidade, detalhes e reserva, com finalidade e períodos de início/fim.
5. Minhas reservas: consultar/cancelar, convidados, autorizações pendentes e escolha do titular entre quitação simulada ou transferência da taxa à mensalidade com 10%.
6. Atividades e eventos: turmas, inscrições, lista de espera e divulgação de eventos sociais por público-alvo.
7. Financeiro: faturas discriminadas, vencimentos, locações transferidas e histórico de quitações simuladas (titular).
8. Ouvidoria: abrir/acompanhar solicitações e reclamações.
9. Dependentes: consulta e aprovações (titular).
10. Ficha associativa: matrícula, situação, titular, categoria, clube de origem, data de início e dependentes; resumo financeiro após o Componente 4.

### 9.2 Web — Administração/funcionário

1. Login e painel por escopo (Administrador Geral, Presidente de clube, Tesoureiro e demais perfis autorizados).
2. Clubes/unidades, funcionários, presidentes e tesoureiros com suas atribuições.
3. Associados, dependentes, categorias, tipos de vínculo, interesses e análise de pedidos de adesão por clube.
4. Espaços, calendário de locações, manutenções e reservas com início/fim mesmo em datas distintas.
5. Turmas, inscrições/lista de espera e gestão de eventos sociais por público-alvo.
6. Financeiro: emitir faturas itemizadas, simular quitação, conferir inadimplência, locações transferidas e acréscimos de 10%; Tesoureiro com escopo autorizado.
7. Avisos e ouvidoria com solicitação/reclamação.
8. Acessos, convidados, relatórios e auditoria.
9. Ficha associativa simplificada e autenticação por usuário ou e-mail, quando os ajustes correspondentes forem autorizados e implementados.

### 9.3 Portaria

1. Identificação do funcionário/unidade.
2. Leitor de QR Code ou entrada manual.
3. Resultado de acesso: permitido/negado, identificação mínima e motivo. Não há liberação manual por parte do porteiro.
4. Lista de convidados autorizados para eventos.
5. Histórico de verificações e indicador de disponibilidade da API; se ficar sem conexão, apresentar "Validação indisponível" em vez de "Entrada permitida".

## 10. Testes mínimos planejados

| ID | Cenário | Resultado esperado |
|---|---|---|
| T01 | Titular autenticado consulta sua área; dependente tenta acessar financeiro do titular sem permissão. | Autorização correta; acesso indevido rejeitado. |
| T02 | Funcionário da unidade A tenta alterar espaço exclusivo da unidade B. | Negado pela API. |
| T03 | Associado da unidade A reserva espaço disponível na unidade B. | Reserva criada se elegível. |
| T04 | Duas sessões tentam confirmar o mesmo espaço no mesmo horário. | No máximo uma confirmação. |
| T05 | Espaço bloqueado por manutenção recebe tentativa de reserva. | Reserva negada. |
| T06 | Gerar duas vezes a mensalidade da mesma competência. | Sem duplicação indevida. |
| T07 | Fatura aberta no prazo, fatura vencida dentro da tolerância e fatura acima do limite. | Bloqueio apenas no último caso. |
| T08 | Registrar quitação simulada após bloqueio. | Próxima validação online reconhece regularização. |
| T09 | Dependente menor solicita espaço pago sem permissão geral ativa do titular. | Não confirma; recebe orientação de solicitar habilitação ao titular. |
| T10 | Inscrições atingem lotação de turma; próxima inscrição entra em espera. | Sem exceder capacidade. |
| T11 | Lista de convidados editada depois das 24 horas anteriores ao evento. | Alteração rejeitada pela API, sem exceção na primeira versão. |
| T12 | Porteiro lê QR válido, expirado e revogado. | Decisões corretas online. |
| T13 | Carteirinha é exibida sem internet no Android. | Exibição de dados e indicador de validade. |
| T14 | Porteiro tenta ler um QR com a API inacessível. | Não libera automaticamente; informa indisponibilidade. |
| T15 | Alteração administrativa relevante. | Evento de auditoria registrado sem segredos. |
| T16 | Titular ou dependente tenta a quarta reserva confirmada para o mesmo mês, incluindo outras unidades. | Rejeita; cancelamento de reserva anterior libera a cota. |
| T17 | Salão reservado exatamente 24 horas antes exige convidados. | Lista completa deve acompanhar a confirmação; cadastro tardio é bloqueado. |
| T18 | Cancelamento antes/depois de 24 horas, com taxa de locação de R$ 30. | Reembolso integral dentro do prazo; fora, multa limitada a R$ 30, sem saldo negativo. |
| T19 | Titular com mensalidade vencida há 10 e depois 11 dias; dependente tenta acessar clube. | Nenhum bloqueio no 10º dia; ambos bloqueados a partir do 11º. |
| T20 | Reserva confirmada e taxa não quitada na data do evento. | Uso do espaço depende de quitação simulada ou transferência confirmada com acréscimo; a entrada geral no clube segue RN07. |
| T21 | Locação de R$ 100 transferida para mensalidade base de R$ 150. | Fatura discrimina R$ 150 + R$ 100 + R$ 10 = R$ 260; sem cobrança avulsa duplicada. |
| T22 | Usuário tenta transferir a mesma locação duas vezes, inclusive em sessões simultâneas. | Uma transferência, um principal e um acréscimo; segunda tentativa é idempotente ou rejeitada sem duplicar. |
| T23 | Próxima mensalidade de destino já está quitada. | Não altera fatura quitada; transfere para a primeira mensalidade futura elegível e exibe competência/vencimento. |
| T24 | Titular cancela locação cuja taxa já foi transferida para uma mensalidade futura. | Ajusta itens se a fatura estiver aberta; se quitada, registra crédito simulado futuro, aplicando uma única vez eventual multa. |
| T25 | App tenta apresentar como válida carteirinha com mais de 24 horas desde a emissão. | Apresenta expiração; renova ao reconectar, sem autorização offline pela portaria. |
| T26 | Porteiro tenta liberar entrada com a API indisponível ou com associação bloqueada. | Sistema não autoriza entrada; não há exceção operacional no fluxo. |
| T27 | Turma lotada ganha uma vaga e funcionário promove o primeiro da espera. | Promoção somente pelo funcionário, respeitando ordem e capacidade. |
| T28 | Associado tenta alterar convidados após o prazo de 24 horas. | Alteração impedida sem exceção. |
| T29 | Mesmo associado tenta matricular-se em duas turmas com horários sobrepostos. | Segunda inscrição é rejeitada com indicação do conflito. |
| T30 | Titular/dependente tenta manter vínculo ativo em duas associações. | Segundo vínculo ativo é rejeitado. |
| T31 | Administrador Geral cadastra clubes Foz 2 e Guaíra 2, cada um com sigla e contatos; Presidente do Foz 2 tenta administrar o Guaíra 2. | Ambos cadastrados sob uma organização; ação fora do escopo do presidente rejeitada na API. |
| T32 | Registrar pessoa com CPF, endereço, sexo, estado civil e vários interesses; consultar por outro associado sem permissão. | Dados persistidos e recuperados pelos perfis autorizados; acesso indevido negado. |
| T33 | Criar associações com tipos Fundador, Convidado e Simpatizante, além de categorias associativas distintas. | Apenas Fundador possui indicador de voto; categoria/tipo/perfil permanecem independentes e não geram desconto automático. |
| T34 | Enviar pedido Mobile de adesão como Convidado/Simpatizante e tentar usar serviço exclusivo enquanto pendente. | Solicitação registrada; associação não ativada e acesso exclusivo rejeitado antes da aprovação. |
| T35 | Presidente do clube de origem aprova solicitação; Presidente de outra unidade tenta aprovar; Administrador Geral avalia qualquer clube. | Apenas os aprovadores com escopo correto concluem a decisão; auditoria registra operação. |
| T36 | Tentar autoinscrição como Fundador pelo aplicativo e cadastrar Fundador pela administração. | Autoinscrição indisponível; cadastro administrativo permitido. |
| T37 | Tesoureiro de um clube consulta/atua nas operações financeiras de seu escopo e tenta acessar dados financeiros fora dele. | Permissões financeiras locais respeitadas pela API; Administrador Geral mantém escopo global. |
| T38 | Reservar espaço de 31/10 18h a 01/11 02h, no fuso da unidade, e consultar cota de outubro e novembro. | Reserva válida se elegível; consome uma cota de outubro apenas; conflito é verificado durante todo o período. |
| T39 | Consultar agenda Web com locações em dias distintos e finalidade de uso informada. | Calendário mostra ocupação correta sem expor dados pessoais indevidos. |
| T40 | Publicar eventos sociais para somente associados e público geral; tentar liberar visitante sem validação online. | Público-alvo respeitado; nenhum visitante é liberado automaticamente sem API. |
| T41 | Abrir e acompanhar chamados como Solicitação e Reclamação. | Tipo, status e histórico preservados com autorização por perfil. |
| T42 | Titular autoriza dependente menor para reservas pagas, revoga a permissão e o menor tenta novamente. | Permissão ativa libera novas reservas pagas; revogação bloqueia novas reservas sem cancelar as anteriores. |
| T43 | Dependente maior de idade solicita reserva paga sem permissão; menor solicita reserva gratuita. | Ambos podem reservar, se as demais regras forem atendidas. |
| T44 | Titular consulta reservas familiares; dependente tenta consultar, alterar convidados ou cancelar reserva de outro membro. | Titular vê e administra reservas da associação; dependente só acessa as próprias. |
| T45 | Autenticar a mesma conta pelo login cadastrado e por um e-mail único associado a ela. | Ambos identificam a mesma conta e aplicam as mesmas permissões e validação de senha. |
| T46 | Tentar login por e-mail ausente, compartilhado por contas diferentes ou coincidente com o login de outra conta. | Não autenticar por correspondência ambígua; retornar falha segura e preservar logins antigos não conflitantes. |
| T47 | Titular abre sua ficha associativa e tenta visualizar dados financeiros ainda não disponibilizados ou de outra associação. | Exibe somente dados autorizados e já implementados; não apresenta contrato anual fictício ou informações financeiras inexistentes. |
| T48 | Emitir mensalidade de associação com dependentes e repetir a mesma competência. | Apenas uma fatura da associação; dependentes não geram mensalidade adicional. |
| T49 | Associação ingressa em novembro e recebe a primeira mensalidade de dezembro. | Primeira competência dezembro, vencimento em 10/12; nenhuma cobrança de mensalidade em novembro. |
| T50 | Quitar fatura de R$ 260 vencida há 10 dias e outra há 11 dias; repetir quitação. | No 10º dia sem multa; no 11º, multa única de R$ 5,20 sobre o total vencido, incluindo locação transferida, sem juros; quitação idempotente e histórico preservado. |
| T51 | Transferir uma locação de R$ 100 para fatura de R$ 150. | Fatura discrimina R$ 150 + R$ 100 + R$ 10; segunda transferência não duplica itens. |
| T52 | Cancelar locação de R$ 100 já transferida por R$ 110, com ou sem multa de R$ 50. | Reversão integral de R$ 110 ou líquida de R$ 60, sem reabrir fatura quitada. |
| T53 | Aplicar crédito maior que o valor da próxima fatura. | Abate até zerar a fatura e preserva saldo para competência futura, sem débito negativo. |
| T54 | Tesoureiro de clube prestador consulta locação interclubes e tenta consultar fatura familiar de outra origem. | Visualiza somente lançamento de sua unidade; acesso à fatura alheia é negado pela API. |
| T55 | Tentar confirmar reserva com mensalidade vencida há 10 e 11 dias e quitar a mensalidade. | Reserva permitida no 10º dia, negada no 11º e liberada após regularização, sem bloquear a consulta financeira. |

## 11. Proposta de componentes de desenvolvimento

**Estado em 09/11/2026:** Componentes 1 (Fundação), 2 (Autenticação e cadastros) e 3 (Espaços e reservas) concluídos por relato de compilação e testes do usuário. O Componente 4 permanece não iniciado e requer autorização explícita. Resultados e limites de verificação constam da Continuidade; o escopo restante continua planejado.

1. **Fundação (concluído por relato):** solução, projetos, schema MySQL, configuração segura, README e build inicial.
2. **Autenticação e cadastros (concluído por relato):** sete perfis, clubes/unidades, titulares, dependentes, categorias, tipos de vínculo, interesses, solicitações de adesão e aprovação por Presidente/Administrador Geral; Web de administração e preparação dos endpoints para Mobile. **Ajustes posteriores ainda não implementados:** login híbrido por usuário/e-mail e ficha associativa simplificada.
3. **Espaços e reservas (concluído por relato):** horários, manutenção, finalidade de locação, agenda Web, intervalos entre datas, contagem mensal por data local de início, convidados, cancelamentos, autorização geral para dependentes menores, regras e concorrência. O acerto financeiro completo das taxas fica no Componente 4.
4. **Financeiro simulado:** geração de mensalidades itemizadas e taxas, quitação, transferência de locações com acréscimo de 10%, multa única de 2% sobre o total da fatura vencida a partir do 11º dia (sem juros), inadimplência e acesso do Tesoureiro limitado ao escopo autorizado.
5. **Carteirinha e portaria online:** emissão, leitura/verificação, bloqueios e auditoria; considerar visitantes de eventos públicos sem dispensar conferência online.
6. **Atividades, turmas e eventos sociais:** inscrições, vagas, lista de espera, eventos com público-alvo de associados ou público geral. Avaliar, antes da implementação, a proposta de vincular eventos à ocupação dos espaços e à agenda do Componente 3.
7. **Comunicação e relatórios:** avisos, ouvidoria separando solicitação/reclamação e indicadores principais.
8. **Mobile Android e integração:** solicitação de adesão de Convidados/Simpatizantes, acompanhamento, navegação, cache SQLite, teste em aparelho e fluxos ponta a ponta.
9. **Possíveis evoluções futuras (não incluídas na primeira entrega):** validação offline na portaria, notificações push remotas e integração física de acesso. A catraca real não é necessária ao projeto.

Após cada componente: informar arquivos alterados, instruções de execução, `dotnet build`, testes mínimos observados e pendências; atualizar documento de continuidade. Não declarar testes aprovados sem evidência. Preservar módulos que já funcionam e esperar aprovação antes de iniciar o próximo componente.

### 11.1 Plano incremental do Componente 4 — validado em 10/11/2026

**Autorização atualizada em 11/11/2026:** o usuário autorizou iniciar o Componente 4, avançou às Etapas 3 e 4 e, após confirmar os testes iniciais das Etapas 4 e 5, autorizou a **Etapa 6**. O Componente 5 requer autorização separada:

1. **Estrutura financeira:** novo script `010_financeiro_base.sql` para faturas, itens, lançamentos e políticas, com índices de unicidade e migrações posteriores preservando `001`–`009`.
2. **Mensalidades:** configuração de valores por categoria/clube de origem, emissão inicialmente manual por competência, vencimento no dia 10, primeira competência no mês seguinte à adesão, leitura e prevenção de duplicidade.
3. **Quitação e inadimplência:** pagamento integral simulado, multa única de 2% sobre o valor total da fatura vencida a partir do 11º dia, sem juros, cálculo dinâmico do atraso, bloqueio de novas reservas a partir do 11º dia e liberação após quitação. Portaria será integrada no Componente 5.
4. **Integração das locações:** quitação avulsa ou transferência, somente pelo titular e antes do uso, com prévia de competência/vencimento e dois itens (principal + 10%) em fatura futura elegível; controle transacional e idempotente.
5. **Cancelamentos/créditos:** permitir cancelar cobrança transferida, ajustar fatura aberta ou gerar crédito futuro quando quitada, reverter também os 10%, considerar a multa de salão e aproveitar créditos pendentes do Componente 3.
6. **Permissões, Web e regressão:** consultas financeiras por titular, Administrador Geral e Tesoureiro em escopos distintos. Tesoureiro pode consultar e registrar quitação simulada apenas de faturas do clube de origem a que esteja atribuído; para locações interclubes, enxerga somente as prestadas pelo seu clube, sem acesso às faturas do outro clube. Somente o Administrador Geral cadastra políticas e emite mensalidades. Auditoria e regressão dos principais fluxos continuam necessárias.

**Testes mínimos:** T06–T08, T19–T24, T37, T48–T55, além dos cenários de concorrência, crédito remanescente e regressão dos Componentes 2 e 3. As verificações de permissão e transação são obrigatórias durante todas as etapas, não apenas na etapa final.

### 11.2 Ajuste de fluxo aprovado em 11/11/2026

Até concluir o **Componente 4**, manter o ciclo incremental existente, incluindo os testes básicos propostos. **A partir do Componente 5**, o usuário fará apenas a compilação como verificação imediata por etapa/componente; os testes funcionais e de integração serão registrados em `TESTES_PENDENTES_CLICKCLUB.md` e executados posteriormente pelo Codex. Essa redução de validação não equivale à aprovação dos fluxos, e erros ainda poderão aparecer durante a demonstração.

**Tarefa futura separada, após concluir a Web:** o Codex deverá receber instruções para (1) limpar somente dados de teste identificados, com backup e autorização específica antes de exclusões, e (2) criar/popular dados inteiramente fictícios para a apresentação de 11/11/2026. Não executar limpeza ou carga durante as etapas do Componente 4. Não usar dados pessoais reais no dataset de demonstração.

## 12. Decisões registradas e pendências

### 12.1 Decisões aprovadas pelo solicitante em 05/11/2026

| Referência | Decisão aprovada |
|---|---|
| **D01** | Um único clube com várias unidades e cadastro associativo centralizado. |
| **D02** | Uma mensalidade por associação/titular/grupo, sem multiplicação automática por unidade visitada. |
| **D04** | Dependentes podem ter login próprio, sujeito às restrições de idade/perfil. |
| **D14 — Reservas pagas por menores** | **Opção B:** o titular concede uma permissão geral individual e revogável para o dependente menor confirmar futuras reservas pagas, sem aprovação por reserva. Revogar não cancela reservas anteriores; maiores e reservas gratuitas dispensam essa permissão. A obrigação financeira permanece na associação/titular. |
| **D06 — Limite de reservas** | **N = 3** reservas confirmadas para utilização no mesmo mês-calendário, somando toda a associação e unidades; canceladas liberam a cota. |
| **D07 — Cancelamento** | **X = Y = 24 horas exatas**; multa simulada é limitada ao menor entre **R$ 50** e o valor da locação. |
| **D08 — Inadimplência** | **D = 10 dias** após vencimento; bloquear titular e dependentes a partir do **11º dia**, somente por mensalidade vencida. |
| **D09** | Reservas pagas são confirmadas no ato, gerando cobrança simulada em aberto. |
| **D11** | Carteirinha pode ser **exibida offline pelo sócio**; primeira versão exige **portaria online para validar o QR**. |
| **D12** | Acesso/portaria **simulados** por Web/Mobile; nenhuma integração ou teste de catraca real. |
| **D13** | Avisos no mural primeiro; notificações push remotas ficam para etapa posterior. |
| **FIN01** | Todas as mensalidades, taxas, pagamentos, multas e reembolsos são **simulados**. |
| **FIN02 — Uso de espaço** | Taxa de locação quitada de forma simulada antes do uso; alternativamente, o titular pode optar por incluir a cobrança na mensalidade futura elegível com acréscimo de 10%. |
| **FIN03 — Acréscimo** | Aplicar os 10% uma única vez sobre o valor original, sem juros recorrentes e sem cobrar o principal em duplicidade. |
| **F01 — Momento da escolha** | O titular pode escolher pelo Mobile ou Web até imediatamente antes do início do uso. Porteiro não altera situação financeira. |
| **F02 — Competência de destino** | Usar a próxima mensalidade elegível; se a da competência seguinte já estiver quitada, usar a primeira mensalidade futura ainda elegível e exibir competência e vencimento antes da confirmação. |
| **F03 — Abrangência** | A opção de transferência está disponível para todos os espaços pagos, com possibilidade de configuração por unidade. |
| **F04 — Cálculo dos juros** | Acrescentar 10% uma única vez, sem aplicação repetida por mês de espera nem juros compostos. |
| **F05 — Cancelamento após transferência** | Ajustar itens se a fatura estiver aberta; se já estiver quitada, registrar crédito simulado em cobrança futura, considerando a política de multa e preservando o histórico. |
| **P08 — Convidados** | Cadastro/alteração não permitido após o limite de 24 horas antes do evento; sem exceções na primeira versão. |
| **P09 — Cadastros iniciais** | Categorias Familiar, Individual e Atleta; espaços Salão de Festas, Churrasqueira e Quadra. Valores e benefícios são configuráveis. |
| **P10 — Lista de espera** | Funcionário promove manualmente os candidatos respeitando a ordem cronológica e a lotação. |
| **P11 — Portaria** | Porteiro não libera manualmente pessoas bloqueadas ou quando a API estiver indisponível. |
| **P12 — QR Code** | Credencial válida por 24 horas, renovada automaticamente quando houver rede; credencial expirada não é apresentada como válida. |
| **P13 — Torneios** | Primeira versão contempla eventos com inscrições e controle de vagas, sem gerenciamento de chaves/resultados. |
| **P14 — Tecnologias** | Escolher versões compatíveis de .NET, MySQL e Android no início do Componente 1, após verificar o ambiente disponível. |

### 12.2 Validação da modelagem — aprovada

| ID | Decisão confirmada | Reflexo na modelagem |
|---|---|---|
| **M01 — Associação** | Um titular tem no máximo uma associação ativa; cada dependente pertence a uma associação ativa por vez. | Integridade dos vínculos `Pessoa`, `Associacao` e `DependenteAssociacao`. |
| **M02 — Reserva/financeiro** | Cada reserva paga gera uma cobrança; transferência para mensalidade não duplica valores. | Integridade transacional de `Reserva`, `CobrancaReserva`, `Fatura` e `ItemFatura`. |
| **M03 — Atividades simultâneas** | Bloquear inscrições sobrepostas do mesmo associado. | Validação de horários na API antes de confirmar `InscricaoTurma`. |
| **M04 — Preços e benefícios** | Valores iniciais fictícios e editáveis por administrador. | Configuração em categorias, políticas e espaços; sem preços fixados no código. |

**Validação concluída:** decisões P01–P14, F01–F05 e M01–M04 aprovadas. Detalhes técnicos de implementação, valores fictícios iniciais e versões de ferramentas podem ser definidos nos respectivos componentes sem alterar as regras de negócio.

### 12.3 Complementações do enunciado do professor — aprovadas em 09/11/2026

O professor descreveu um conjunto de clubes (exemplos: Foz 2, Guaíra 2), associados com cadastro detalhado e interesses, tipos de vínculo e direito a voto, administração local e superadministrador, tesoureiro, eventos sociais, solicitações/reclamações, adesão via aplicativo com aprovação, agenda de locações e consulta financeira. O usuário aprovou as complementações abaixo, mantendo o escopo anterior quando não houver conflito.

| Referência | Decisão aprovada |
|---|---|
| **PC01 — Clubes** | **Opção A:** cada unidade é um clube da mesma organização; cadastro associativo central e possibilidade de serviços entre clubes preservados (D01). |
| **PC02 — Dados cadastrais** | Acrescentar sigla/bairro/telefone/e-mail ao clube e CPF, endereço, sexo, estado civil e interesses múltiplos ao associado; respeitar privacidade. |
| **PC03 — Classificações** | Criar `TipoVinculoAssociativo`: Associado/Fundador com voto, Convidado e Simpatizante sem voto. Não substituir Familiar/Individual/Atleta nem os perfis; sem benefícios financeiros presumidos. |
| **PC04 — Administração** | Administrador Geral equivale a superadministrador; Presidente administra seu clube e Tesoureiro atua nas finanças autorizadas por unidade. |
| **PC05 — Aprovação** | **Opção A:** Presidente do clube de origem e Administrador Geral podem aprovar/rejeitar adesões, respeitando escopo e auditoria. |
| **PC06 — Cadastro Mobile** | **Opção A:** Convidado e Simpatizante solicitam adesão pelo aplicativo; Associado/Fundador é cadastrado pela administração. Pendência não ativa associação. |
| **PC07 — Reservas entre meses** | **Opção A:** reserva que começa em outubro e termina em novembro conta só na cota de outubro, determinada pela data local de início da utilização na unidade. |
| **PC08 — Salão e agenda** | Reservas possuem data/hora de início e fim, inclusive atravessando datas, finalidade de uso e calendário de locações na Web. |
| **PC09 — Eventos** | Eventos sociais específicos por clube e público-alvo (somente associados ou público geral), separados de turmas esportivas. |
| **PC10 — Ouvidoria** | Distinguir solicitações e reclamações, preservando histórico e autorização. |

**Detalhamentos técnicos futuros, sem mudança de requisito:** obrigatoriedade de cada campo de formulário, benefícios específicos de categorias, forma de acompanhamento da adesão pelo Mobile, identificação de visitantes em eventos abertos e acesso financeiro do Tesoureiro em transações entre clubes. Esses pontos serão definidos antes dos respectivos componentes, sem presumir novas regras de negócio.

### 12.4 Decisões posteriores às observações do professor — 10/11/2026

As anotações de correção de **outro projeto** foram tratadas como referência, não como exigências automaticamente aplicáveis ao ClickClub. O usuário confirmou as decisões abaixo; elas não significam alterações já implementadas em API, Web, SQL ou Mobile.

| Referência | Decisão | Estado |
|---|---|---|
| **PC11 — Ficha associativa** | Criar uma consulta simplificada baseada em matrícula, situação e dados associativos existentes, com financeiro resumido quando disponível. Não construir módulo de contratos anuais, renovação, assinatura ou novos estados contratuais. | Aprovada; implementação futura |
| **PC12 — Login híbrido** | Preservar login e senha atuais e adicionar autenticação pelo e-mail quando houver identificação inequívoca da conta. Não exigir seleção de clube no login. | Aprovada; implementação futura |
| **PC13 — Divulgação** | Não integrar envio automático por WhatsApp, e-mail ou publicação em redes sociais; manter mural de avisos previsto. | Aprovada; exclusão de escopo |
| **PC14 — Presidência** | Manter Presidente com escopo local, sem mandato obrigatório de dois anos ou módulo de eleições. | Aprovada; exclusão de escopo |

**Melhorias não convertidas em requisitos aprovados:** profissão, parentesco, data de cadastro e histórico visual de alterações, filtro por gênero, novos tipos de espaços, vínculo de evento a espaço/agenda, diretores e eventual limite por tipo de ambiente. O usuário considerou as sugestões pertinentes, mas ainda não foram detalhadas nem autorizadas para implementação. A regra de três reservas mensais continua intacta. Os encargos de mensalidade são atualizados na seção 12.5.

### 12.5 Decisões financeiras para o Componente 4 — 10/11/2026

| Referência | Decisão do usuário | Estado |
|---|---|---|
| **F06 — Encargo por atraso** | Opção B: multa **única de 2%**, **sem juros**, aplicada **a partir do 11º dia de atraso** sobre o **valor total vencido da fatura, incluindo locações transferidas**. Diferente do acréscimo único de 10% pela transferência de locação. | Aprovada integralmente |
| **F07 — Início/vencimento** | Opção B: primeira mensalidade no **mês seguinte à adesão**; vencimento no **dia 10** de cada mês. Valores permanecem configuráveis. | Aprovada |
| **F08 — Cancelamento de locação transferida** | Opção A: reverter também o acréscimo de 10%, preservando somente multa de cancelamento aplicável. | Aprovada |
| **F09 — Créditos** | Opção A: compensar créditos automaticamente na próxima fatura elegível; transportar saldo residual sem criar fatura negativa. | Aprovada |
| **F10 — Tesouraria interclubes** | Opção A: tesoureiro do clube de origem administra faturas de suas associações; tesoureiro do prestador consulta lançamentos de locações do seu clube sem acesso à fatura familiar de outra origem; Administrador Geral tem escopo global. | Aprovada |

**Validação final da F06:** o usuário confirmou expressamente o **11º dia de atraso** como início da multa e o **total vencido da fatura, incluindo locações transferidas**, como base de cálculo. Não há juros nem aplicação repetida da multa. A emissão manual, a quitação integral sem pagamentos parciais e os valores fictícios de teste são propostas técnicas da seção 11.1, não novas decisões obrigatórias. O planejamento está validado e a **Etapa 1 foi autorizada**, com execução e testes ainda pendentes.

### 12.6 Observações após os testes da Etapa 4 — 11/11/2026

- **Categoria Individual e dependentes — opção B aprovada:** o usuário constatou que era possível vincular dependentes a titulares da categoria `Individual` e aprovou a recomendação de **impedir novos dependentes**, sem conversão automática para Familiar. A API deve bloquear cadastro e reativação de dependentes nessa categoria; alterações de associação ou renomeações de categorias que resultariam em Individual com dependentes ativos também devem ser rejeitadas. Vínculos antigos permanecem para regularização **manual pelo Administrador Geral**, sem exclusão ou migração automática. A implementação foi preparada, mas aguarda build e testes do usuário.
- **Histórico de reservas — correção validada pelo usuário:** a lista `Minhas reservas` ordenava pela data **de utilização** (`inicio_utc DESC`), colocando reservas criadas recentemente abaixo de reservas de uso futuro. O usuário espera as **últimas criadas primeiro**. A ordenação foi alterada para `criada_em_utc DESC, id DESC`, e o usuário confirmou em 11/11/2026 que a correção **funcionou**. Nenhuma regra de reserva ou dado armazenado foi alterado.

## 13. Instruções iniciais sugeridas para o ChatGPT Project

> Você atuará como analista de requisitos, arquiteto e assistente de desenvolvimento do projeto acadêmico ClickClub. A fonte de verdade é este documento **aprovado**, complementado pelo arquivo `INSTRUCTIONS_PROJECT_CLICKCLUB.md`. O sistema usa MySQL centralizado, API ASP.NET Core, Web ASP.NET Core MVC e Mobile .NET MAUI Android com SQLite local quando necessário. A Web e o Mobile só se comunicam com a API; a API concentra autenticação, autorização, regras, transações e auditoria.
>
> Priorize simplicidade, funcionamento demonstrável, segurança básica e preparação para multiunidades sem adicionar serviços pagos, integrações bancárias, catracas reais ou microsserviços por iniciativa própria. Todas as movimentações financeiras serão simuladas. No financeiro, o titular pode quitar a taxa da locação de forma simulada antes do uso ou transferi-la até imediatamente antes do uso para a próxima mensalidade elegível com acréscimo único de 10%, sendo obrigatório evitar cobrança duplicada e preservar faturas quitadas. A transferência se aplica a todos os espaços pagos; cancelamentos exigem ajuste em fatura aberta ou crédito simulado futuro se já quitada. O QR Code do associado poderá ser exibido offline, mas a portaria somente validará online na primeira entrega; não construir nem testar integração de catraca física. Respeite todas as decisões aprovadas da seção 12 e não altere o escopo sem autorização. Desenvolva por componentes funcionais, aguardando autorização para avançar, e forneça arquivos/caminhos completos, passos de execução e testes mínimos. Não afirme que uma build ou teste foi bem-sucedido sem evidência. Mantenha `DOCUMENTACAO_CLICKCLUB.md` como fonte de requisitos e `CONTINUIDADE_CLICKCLUB.md` com implementação, testes, erros, pendências e próximos passos. Proteja credenciais e preserve as partes já funcionais.

## 14. Referências técnicas de apoio

- Microsoft Learn — [Armazenamento SQLite no .NET MAUI](https://learn.microsoft.com/pt-br/dotnet/maui/data-cloud/database-sqlite).
- Microsoft Learn — [Armazenamento seguro no .NET MAUI](https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/storage/secure-storage).
- Microsoft Learn — [Notificações push no .NET MAUI](https://learn.microsoft.com/en-us/dotnet/maui/data-cloud/push-notifications).
- Firebase — [Configuração e funcionamento da entrega de mensagens FCM](https://firebase.google.com/docs/cloud-messaging/server-environment).

---

**Próximo passo (10/11/2026):** após backup, executar e validar `010_financeiro_base.sql` no MySQL 8.4, conferir o esquema e a preservação das reservas; então confirmar os resultados antes de avançar. **A Etapa 1 está preparada, sem execução nem testes de banco comprovados.** Login híbrido e ficha associativa são ajustes aprovados de escopo, mas ainda não implementados. Os testes Android e a integração ponta a ponta permanecem futuros.
