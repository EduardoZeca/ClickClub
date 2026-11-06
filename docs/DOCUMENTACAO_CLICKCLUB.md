# ClickClub — Documento de Requisitos e Planejamento

**Versão:** 1.0 — Documentação funcional e modelagem inicial aprovadas  
**Data:** 05/11/2026  
**Estado:** Requisitos, regras de negócio, parâmetros e modelagem inicial aprovados em 05/11/2026. Documento-base para o ChatGPT Project ClickClub. Nenhum código implementado ou componente iniciado.

## 1. Visão geral

O ClickClub é um sistema para gestão de um clube social com possibilidade de operar várias unidades em diferentes municípios e estados. O sistema atenderá associados, dependentes, funcionários, porteiros e administradores, contemplando cadastro, carteirinhas, reservas de espaços, atividades esportivas, cobranças simuladas, comunicação e controle de acessos.

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
2. Cadastro de unidades, associados titulares, dependentes e funcionários.
3. Cadastro de espaços e controle de reservas sem conflitos.
4. Emissão e quitação **simuladas** de faturas; regra de inadimplência.
5. Carteirinha digital com QR Code exibível offline no aparelho do sócio e validação online simulada pela portaria.
6. Inscrição em turmas com limite de vagas e lista de espera.
7. Avisos por unidade e geral, ouvidoria básica e relatórios administrativos essenciais.

### 2.2 Funcionalidades futuras ou fora do escopo inicial

- Notificações push remotas (integração com serviço de entrega de mensagens).
- Validação offline pela portaria, com sincronização posterior e controles de validade: **adiada, não incluída na primeira entrega**.
- Integração física ou testes com catracas, leitores dedicados, sistemas bancários ou emissão fiscal: **fora do escopo**.
- Torneios com chaves, resultados, classificação e arbitragem: **fora do escopo inicial**; inicialmente podem ser eventos com inscrições e vagas.
- Aplicativo iOS, armazenamento em nuvem obrigatório, gateway de pagamento e múltiplas organizações/franquias juridicamente independentes: **fora do escopo inicial**.

## 3. Conceitos e decisões estruturais

### 3.1 Unidade

Uma unidade possui identificador próprio, endereço, município, UF, fuso horário, situação (ativa/inativa), espaços, turmas, funcionários e avisos. O sistema deve suportar vários municípios e estados sem pressupor que todas as unidades tenham os mesmos horários, taxas, instalações ou atividades.

**Decisão aprovada (D01):** todas as unidades pertencem à mesma organização de clube; o cadastro e a situação associativa são centrais. Cada associado possui uma unidade de origem, mas pode consultar serviços e solicitar reservas em qualquer unidade habilitada, respeitando suas regras.

### 3.2 Associado, dependente, categoria e perfil

- **Titular:** pessoa responsável pela associação e pelas faturas do grupo familiar, quando aplicável.
- **Dependente:** pessoa vinculada a um titular, com direitos de uso definidos pela categoria e idade.
- **Categoria associativa:** familiar, individual, atleta ou outras configuráveis. Determina benefícios, tarifas e elegibilidade; não é uma permissão administrativa.
- **Perfil de acesso:** titular, dependente, funcionário da unidade, administrador geral ou porteiro. Determina o que a conta pode fazer no sistema.

Categoria associativa e perfil de acesso **não são a mesma coisa**. O administrador geral pode gerenciar categorias, mas a categoria “atleta” não concede privilégios de administrador.

**Categorias iniciais aprovadas:** Familiar, Individual e Atleta. Valores, benefícios, limites de dependentes e eventuais variações por unidade permanecem configuráveis, sem números presumidos nesta documentação.

**Decisão aprovada (D04):** dependentes podem possuir login próprio, com limitações conforme idade e direitos. Um dependente não recebe acesso ao financeiro do titular nem autorização automática para assumir despesas.

### 3.3 Cobrança e bloqueio

**Decisão aprovada (D02):** será emitida uma mensalidade por associação/titular, inclusive quando o plano familiar incluir dependentes, sem cobrar uma mensalidade adicional por cada unidade visitada. Mensalidade em aberto antes ou no vencimento **não** é inadimplência passível de bloqueio. O bloqueio atingirá titular e dependentes vinculados somente depois de ultrapassado o prazo de tolerância aprovado de **10 dias**. Pagamentos serão registrados como simulação, sem transações bancárias reais.

**Atualização aprovada:** `D = 10 dias` de tolerância após o vencimento, contados pela data local. Apenas mensalidades vencidas além desse prazo geram bloqueio associativo na primeira versão. A taxa de locação deve estar quitada de forma simulada **ou** transferida formalmente para a mensalidade futura elegível, com acréscimo de 10% uma única vez antes da utilização do espaço. A transferência, depois de incorporada à mensalidade, compõe o valor dessa cobrança e acompanha seu vencimento.

### 3.4 Disponibilidade e integridade

Conflitos de reserva e lotação devem ser verificados novamente pela API **dentro de uma transação no MySQL** no instante da confirmação. Mostrar um horário disponível na interface não garante a reserva até que a API confirme a operação.

### 3.5 Carteirinha exibível offline; validação de portaria online

**Não usar o JWT de login como QR Code de acesso.** A carteirinha deve possuir credencial de acesso distinta, com identificador, prazo de validade e assinatura digital verificável. O app do sócio pode exibir a credencial previamente obtida quando estiver sem rede, enquanto ela não estiver expirada.

**Validade aprovada (P12):** cada credencial QR tem validade de **24 horas** a partir da emissão; o aplicativo solicita renovação automaticamente quando houver conexão. Se expirar durante a falta de rede, não deve ser apresentada como uma credencial de entrada válida. A portaria permanece obrigatoriamente online para validação.

**Decisão aprovada (D11):** a primeira entrega exibirá o QR Code offline no Android do associado, mas a **portaria precisará de conexão para validá-lo na API**. A API verificará credencial, validade, vínculo, situação associativa, inadimplência e unidade, registrando permitido/negado. O controle será simulado por interface, sem catraca.

Uma futura validação offline na portaria exigirá dispositivo de porteiro com chave pública, cache de bloqueios, janela limitada de sincronização e posterior envio dos registros. **Isso não integra o escopo aprovado da primeira entrega.** Uma carteirinha exibida offline pode estar revogada no servidor; a decisão de acesso continua pertencendo à API.

## 4. Perfis e permissões

| Perfil | Permissões previstas |
|---|---|
| Administrador geral | Gerenciar unidades, categorias, pessoal, associados, regras, financeiro geral, atividades, relatórios e auditoria. |
| Funcionário da unidade | Gerenciar instalações, turmas, reservas e atendimento **somente das unidades atribuídas**. Não altera políticas globais. |
| Porteiro | Validar carteirinhas e convidados, consultar autorizações relevantes e registrar entradas/saídas da unidade atribuída. Não altera faturas. |
| Sócio titular | Consultar seu cadastro e dependentes, carteirinha, cobranças, reservas, atividades, avisos e chamados. |
| Dependente | Consultar a própria carteirinha, avisos, atividades e reservas permitidas, respeitando idade, categoria e autorização do titular. |

Toda autorização é conferida pela API; ocultar botões na interface não substitui segurança no servidor. A possibilidade de uma conta possuir mais de um papel ou funcionário atender duas unidades deve ser prevista pela modelagem de vínculos, mesmo que o primeiro protótipo use apenas um papel principal.

## 5. Requisitos funcionais

| ID | Descrição | Prioridade |
|---|---|---|
| **RF01** | Autenticar e autorizar os cinco perfis de acesso, com permissões por perfil e unidade. | Essencial |
| **RF02** | Permitir ao associado visualizar e solicitar serviços e reservas de unidades diferentes da sua unidade de origem, conforme elegibilidade. | Essencial |
| **RF03** | Consultar agenda e reservar espaços por data e horário; apresentar taxa aplicável e impedir sobreposição de reservas confirmadas ou bloqueios de manutenção. | Essencial |
| **RF04** | Exibir carteirinha digital e QR Code previamente armazenado no mobile do associado, mesmo sem conexão no aparelho; credencial válida por 24 horas e renovada quando houver rede; a leitura/decisão na portaria será online pela API. | Essencial |
| **RF05** | Impedir novas reservas e novas autorizações de entrada de titular e dependentes quando a associação tiver mensalidade vencida há **mais de 10 dias**. A portaria não libera entradas bloqueadas ou sem validação online. | Essencial |
| **RF06** | Cadastrar, consultar, alterar, ativar e desativar unidades com localização e horários próprios. | Essencial |
| **RF07** | Cadastrar, consultar, alterar e desativar titulares; gerenciar categoria, unidade de origem e estado associativo. | Essencial |
| **RF08** | Vincular/desvincular dependentes ao titular e registrar data de nascimento e condição de autorização. | Essencial |
| **RF09** | Administrar funcionários, porteiros e suas atribuições de unidade. | Essencial |
| **RF10** | Cadastrar tipos de espaço, capacidades, horários, preços e períodos indisponíveis por unidade. Tipos iniciais: salão de festas, churrasqueira e quadra, com possibilidade de expansão. | Essencial |
| **RF11** | Consultar reservas, cancelar conforme política e registrar o cálculo de taxa/multa/reembolso **simulado**. | Essencial |
| **RF12** | Cadastrar e editar a lista de convidados das reservas elegíveis até o limite estabelecido e permitir conferência na portaria. | Essencial |
| **RF13** | Gerar faturas de mensalidade por competência, com itens discriminados de mensalidade, taxas e locações transferidas, evitando duplicação da mesma cobrança. | Essencial |
| **RF14** | Registrar quitação simulada, consultar débitos, discriminar principal e acréscimos e apresentar histórico financeiro ao titular e ao administrador autorizado. | Essencial |
| **RF15** | Cadastrar atividades, turmas, instrutores (se utilizados), capacidade, datas, horários e unidade. | Essencial |
| **RF16** | Inscrever associados elegíveis em turmas/eventos, impedir excesso de vagas e disponibilizar lista de espera. | Essencial |
| **RF17** | Publicar avisos gerais e por unidade; permitir leitura no Web/Mobile. | Essencial |
| **RF18** | Criar e acompanhar chamados de ouvidoria com categoria, status e histórico de respostas. | Essencial |
| **RF19** | Enviar notificações push remotas para dispositivos cadastrados, por público/unidade, utilizando serviço de entrega externo configurado. | Posterior |
| **RF20** | Validar online, em interface de portaria simulada, QR Code de carteirinha ou autorização de convidado; registrar decisão, motivo, data, hora, unidade e operador. Sem hardware de catraca. | Essencial |
| **RF21** | Disponibilizar painel com ocupação de espaços, faturas pagas/em aberto/vencidas, inscrições e totais por unidade/período. | Essencial (básico) |
| **RF22** | Registrar auditoria de eventos críticos: alterações de associados, reservas, faturas, permissões e validações de acesso. | Essencial |
| **RF23** | Permitir consulta de histórico de acessos e reservas, filtrado pelos direitos do usuário. | Essencial |
| **RF24** | Permitir que o titular escolha, até imediatamente antes do início do uso do espaço, quitar a taxa de locação de forma simulada **ou** transferi-la para a próxima mensalidade elegível, com acréscimo simples e único de **10%** sobre o valor original. Registrar a escolha e a competência de destino. Disponível para todos os espaços pagos. | Essencial; aprovado |

## 6. Regras de negócio

| ID | Regra | Estado |
|---|---|---|
| **RN01** | Cada titular/associação possui limite de **3 reservas confirmadas para datas de utilização no mesmo mês-calendário**, somando titular, dependentes e todas as unidades. Reservas canceladas liberam a cota. | Aprovado |
| **RN02** | Reserva de salão exige pelo menos **24 horas exatas** de antecedência (X=1 dia). Cancelamento com pelo menos **24 horas exatas** antes do início (Y=1 dia) resulta em reembolso integral simulado; após esse marco, a multa simulada é o **menor valor entre R$ 50 e a taxa original da locação**, sem saldo negativo. | Aprovado |
| **RN03** | Convidados de salões/churrasqueiras devem estar cadastrados até 24 horas antes do evento. Se a reserva for feita exatamente no limite de antecedência, a lista completa deverá ser incluída na confirmação. **Não são permitidas inclusões/alterações após o prazo**, sem exceções na primeira versão. | Aprovado |
| **RN04** | Dependente menor não pode assumir locação paga ou responsabilidade financeira sem autorização prévia registrada pelo titular. | Aprovado; fluxo de autorização no componente de reservas |
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

### 6.1 Cálculo de atraso aprovado

Calcular `diasEmAtraso` a partir da **data local do vencimento**, sem arredondar horas de forma inconsistente. Bloquear titular e dependentes somente se `diasEmAtraso > 10`, isto é, a partir do **11º dia de atraso**. A API recalcula a elegibilidade na tentativa de reserva/entrada, sem depender exclusivamente de tarefa automática diária. Apenas mensalidades vencidas acima desse limite bloqueiam a associação na primeira versão; cobranças de locação transferidas passam a integrar a mensalidade de destino.

### 6.2 Fluxo proposto das reservas

1. Usuário escolhe unidade, espaço, dia e horário; interface consulta disponibilidade.
2. API valida perfil, associação, inadimplência, horário, limites e conflitos dentro de transação.
3. **Decisão aprovada (D09):** reserva é confirmada imediatamente, com preço e política registrados, e é criada taxa simulada em aberto quando cabível. A confirmação não aguarda quitação simulada.
4. Até imediatamente antes do início da utilização, o titular registra quitação simulada ou solicita transferência da locação para a mensalidade elegível com acréscimo único de 10%.
5. Titular consulta e pode cancelar; a API calcula eventual multa/reembolso simulado e libera o horário.
6. Em salões/churrasqueiras, titular informa convidados até 24 horas antes; no limite da reserva, a lista completa é exigida na confirmação.

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
| **RNF13 — Privacidade** | Coletar somente dados pessoais necessários; limitar leitura por perfil e definir retenção de documentos, convidados e logs. | Revisão das telas, endpoints e tabelas. |
| **RNF14 — Datas e horários** | Guardar instantes com referência de fuso e interpretar agendas na hora local de cada unidade. | Testes com unidades em fusos distintos. |

## 8. Modelagem lógica inicial validada

**Identidade e organização**

- `Unidade`: Id, Nome, Endereco, Municipio, UF, FusoHorario, Status.
- `Pessoa`: Id, Nome, DataNascimento, DocumentoIdentificacao, Telefone, Email, Status.
- `Usuario`: Id, PessoaId, Login, SenhaHash, Ativo.
- `PapelUsuario`: UsuarioId, Papel, UnidadeIdOpcional (escopo do funcionário/porteiro).
- `CategoriaAssociativa`: Id, Nome, RegrasBeneficios, Ativa.
- `Associacao`: Id, TitularPessoaId, CategoriaId, UnidadeOrigemId, Matricula, Status, DataInicio.
- `DependenteAssociacao`: Id, AssociacaoId, DependentePessoaId, Ativo, Autorizacoes.

**Instalações e reservas**

- `Espaco`: Id, UnidadeId, Nome, Tipo, Capacidade, TaxaBase, Ativo.
- `HorarioEspaco`: Id, EspacoId, DiaSemana, Inicio, Fim.
- `IndisponibilidadeEspaco`: Id, EspacoId, Inicio, Fim, Motivo.
- `Reserva`: Id, EspacoId, AssociacaoId, ResponsavelPessoaId, Inicio, Fim, ValorContratado, Status, CriadaEm.
- `ConvidadoReserva`: Id, ReservaId, Nome, IdentificacaoMinima, Situacao.
- `AutorizacaoResponsavel`: Id, ReservaId, TitularPessoaId, DataAutorizacao, Status (se necessário para dependentes).

**Financeiro**

- `Fatura`: Id, AssociacaoId, UnidadeIdOpcional, Tipo, Competencia, DataVencimento, ValorTotal, Status.
- `ItemFatura`: Id, FaturaId, Tipo, Descricao, Valor, ReservaIdOpcional, OrigemUnidadeIdOpcional (mensalidade base, principal de locação, acréscimo, outros).
- `CobrancaReserva`: Id, ReservaId, ValorOriginal, Status (EmAberto/QuitadaSimulada/Transferida), Modalidade, CompetenciaDestinoOpcional, FaturaDestinoIdOpcional, PercentualAcrescimo, ValorAcrescimo, DataDecisao.
- `LancamentoFinanceiro`: Id, FaturaIdOpcional, CobrancaReservaIdOpcional, Tipo, Valor, DataHora, UsuarioResponsavelId, Observacao.
- `PoliticaFinanceira`: Id, UnidadeIdOpcional, CategoriaIdOpcional, ValorMensal, DiasTolerancia, Vigencia.

**Atividades e comunicação**

- `Atividade`: Id, Nome, Descricao, Tipo.
- `Turma`: Id, AtividadeId, UnidadeId, InstrutorPessoaIdOpcional, Inicio, Fim, Capacidade, Status.
- `InscricaoTurma`: Id, TurmaId, PessoaId, Status, InscritaEm, PosicaoEsperaOpcional.
- `Aviso`: Id, UnidadeIdOpcional (nulo = geral), Titulo, Corpo, Inicio, Fim, AutorUsuarioId.
- `ChamadoOuvidoria`: Id, PessoaId, UnidadeIdOpcional, Assunto, Descricao, Status, CriadoEm.
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

### 8.2 Relacionamentos consolidados

| Origem | Relação | Destino | Regra |
|---|---|---|---|
| `Unidade` | 1:N | `Espaco`, `Turma` | Espaços e turmas pertencem à unidade onde são oferecidos. |
| `Unidade` | 1:N | `Associacao` | Unidade de origem; não restringe reservas em outras unidades. |
| `Pessoa` | 1:0..1 | `Usuario` | Pessoas com acesso ao sistema recebem credenciais e papel(is) definidos. |
| `Associacao` | N:1 | `Pessoa` titular | Uma associação possui um titular e o titular tem, no máximo, uma associação ativa. |
| `Associacao` | 1:N | `DependenteAssociacao` | Cada vínculo referencia uma pessoa dependente e não pode coexistir em duas associações ativas. |
| `Associacao` | N:1 | `CategoriaAssociativa` | A categoria define regras e preço-base configuráveis. |
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

1. Login.
2. Início: status associativo, unidade preferida, avisos e próximas reservas.
3. Minha carteirinha: QR Code com validade de 24 horas, renovação automática quando online e aviso de expiração quando sem rede.
4. Unidades e espaços: filtros, disponibilidade, detalhes e reserva.
5. Minhas reservas: consultar/cancelar, convidados, autorizações pendentes e escolha do titular entre quitação simulada ou transferência da taxa à mensalidade com 10%.
6. Atividades: turmas, inscrições e lista de espera.
7. Financeiro: faturas discriminadas, vencimentos, locações transferidas e histórico de quitações simuladas (titular).
8. Ouvidoria: abrir/acompanhar chamado.
9. Dependentes: consulta e aprovações (titular).

### 9.2 Web — Administração/funcionário

1. Login e painel por escopo.
2. Unidades e funcionários.
3. Associados, dependentes e categorias.
4. Espaços, agendas, manutenções e reservas.
5. Turmas e inscrições/lista de espera.
6. Financeiro: emitir faturas itemizadas, simular quitação, conferir inadimplência, locações transferidas e acréscimos de 10%.
7. Avisos e ouvidoria.
8. Acessos, convidados, relatórios e auditoria.

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
| T09 | Dependente menor solicita espaço pago sem aprovação. | Não confirma sem autorização. |
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

## 11. Proposta de componentes de desenvolvimento

**Nenhum componente está implementado nesta etapa.** Iniciar somente mediante autorização explícita do usuário para o respectivo componente.

1. **Fundação:** solução, projetos, schema MySQL, configuração segura, README e build inicial.
2. **Autenticação e cadastros:** perfis, unidades, titular, dependentes e staff; Web de administração.
3. **Espaços e reservas:** horários, manutenção, elegibilidade, regras e concorrência.
4. **Financeiro simulado:** geração de mensalidades itemizadas e taxas, quitação, transferência de locações com acréscimo de 10%, inadimplência e integração com reservas/acesso.
5. **Carteirinha e portaria online:** emissão, leitura/verificação, bloqueios e auditoria.
6. **Atividades e turmas:** inscrições, vagas e lista de espera.
7. **Comunicação e relatórios:** avisos, ouvidoria e indicadores principais.
8. **Mobile Android e integração:** navegação, cache SQLite, teste em aparelho e fluxos ponta a ponta.
9. **Possíveis evoluções futuras (não incluídas na primeira entrega):** validação offline na portaria, notificações push remotas e integração física de acesso. A catraca real não é necessária ao projeto.

Após cada componente: informar arquivos alterados, instruções de execução, `dotnet build`, testes mínimos observados e pendências; atualizar documento de continuidade. Não declarar testes aprovados sem evidência. Preservar módulos que já funcionam e esperar aprovação antes de iniciar o próximo componente.

## 12. Decisões registradas e pendências

### 12.1 Decisões aprovadas pelo solicitante em 05/11/2026

| Referência | Decisão aprovada |
|---|---|
| **D01** | Um único clube com várias unidades e cadastro associativo centralizado. |
| **D02** | Uma mensalidade por associação/titular/grupo, sem multiplicação automática por unidade visitada. |
| **D04** | Dependentes podem ter login próprio, sujeito às restrições de idade/perfil. |
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

**Próximo passo:** configurar o ChatGPT Project com `DOCUMENTACAO_CLICKCLUB.md`, `INSTRUCTIONS_PROJECT_CLICKCLUB.md` e `CONTINUIDADE_CLICKCLUB.md`. O Componente 1 permanece pendente até autorização explícita.
