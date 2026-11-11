# ClickClub — Guia prático de preparação e demonstração (Componentes 1 a 4, Etapa 4)

**Revisão:** 11/11/2026  
**Objetivo:** preparar dados fictícios, navegar pelas telas já existentes e demonstrar o fluxo completo, sem precisar adivinhar a ordem dos cadastros.  
**Estado:** roteiro preparado com base no código da API/Web. A execução no computador do desenvolvedor ainda precisa ser validada.  
**Escopo:** somente funcionalidades entregues até o **Componente 4 — Etapa 4**. Não inclui MAUI/Android, QR Code, portaria, atividades, créditos automáticos nem as Etapas 5–6 do financeiro.

## 0. Antes de cadastrar qualquer coisa

1. Faça um backup do banco de demonstração. **Não apague o banco nem os dados existentes.** Se já houver um cadastro adequado, reutilize-o; não crie duplicatas apenas para seguir este roteiro.
2. Confirme que os scripts `database/scripts/001` a `010` foram aplicados na ordem durante o desenvolvimento. **Não execute novamente scripts `ALTER TABLE` já aplicados.** A Etapa 4 não tem migração SQL nova.
3. Abra um terminal na raiz do repositório (pasta que contém `ClickClub.slnx`), execute:

   ```powershell
   dotnet build ClickClub.slnx
   ```

4. Inicie a API no primeiro terminal:

   ```powershell
   dotnet run --project src/ClickClub.Api --launch-profile https
   ```

5. Inicie a Web em um **segundo** terminal:

   ```powershell
   dotnet run --project src/ClickClub.Web --launch-profile https
   ```

6. Acesse `https://localhost:7202` no navegador. O endereço da API é `https://localhost:7201`. O diagnóstico básico está em `https://localhost:7201/api/diagnostico`.
7. Entre com a conta de **Administrador Geral já criada no Componente 2**. O comando `--bootstrap-admin` é exclusivo da configuração inicial e **não precisa ser repetido**. As senhas e a chave JWT devem permanecer fora do Git.

Se a compilação falhar ou a API não alcançar o MySQL, **pare aqui**: os passos seguintes dependem dessa conexão. Não tente compensar o erro criando dados diretamente no banco.

### Como se localizar

| Quem está conectado | Menus úteis na Web | Função neste roteiro |
| --- | --- | --- |
| **Administrador Geral** | Clubes, Categorias, Titulares, Equipe, Espaços, Solicitações, Financeiro | Preparar todos os cadastros, horários, políticas e faturas. |
| **Presidente do clube** | Meus clubes, Solicitações, Espaços | Consultar seu clube, analisar pedidos de adesão e administrar espaços autorizados. |
| **Titular** | Minha associação, Reservas, Financeiro | Criar reservas, quitar avulso ou transferir locações, consultar/quitar mensalidades. |
| **Dependente** | Minha associação, Reservas | Reservar conforme permissões; **não** decidir pagamentos. |
| **Funcionário da unidade** | Meus clubes, Espaços | Administrar espaços dentro do clube autorizado. |
| **Tesoureiro/Porteiro** | Meus clubes | Existem como perfis, mas as telas financeiras do Tesoureiro e a validação da portaria ainda não foram implementadas. |

**Distinção importante:** `PresidenteClube` é uma **conta funcional** cadastrada em **Equipe**. O **titular familiar** é um associado cadastrado em **Titulares**. São cadastros diferentes, mesmo que uma pessoa real pudesse ocupar ambos os papéis.

## 1. Criar ou localizar um clube

**Perfil:** Administrador Geral.  
**Caminho:** menu **Clubes** → **Cadastrar clube** (`/Unidades`).

Exemplo de dados fictícios:

| Campo | Exemplo |
| --- | --- |
| Nome do clube | Clube Central de Testes |
| Sigla | CCT |
| Endereço | Rua de Exemplo, 100 |
| Município / UF | Curitiba / PR |
| Fuso horário | `America/Sao_Paulo` |

Clique em **Salvar**. O clube deve aparecer na listagem com situação **Ativa**.

Se já existe um clube ativo, anote seu nome e **use-o**. O clube será a origem da associação, a referência da mensalidade e possivelmente o local das reservas.

## 2. Criar ou localizar a categoria Familiar

**Perfil:** Administrador Geral.  
**Caminho:** **Categorias** → **Cadastrar categoria** (`/Categorias`).

- Nome: `Familiar`.
- Confirme que a categoria aparece como **Ativa**.
- Se `Familiar` já estiver cadastrada, **não duplique**. `Individual` e `Atleta` podem ser criadas para ampliar a demonstração, mas não são necessárias ao fluxo financeiro mínimo.

**Atenção:** `Familiar` é a categoria da associação, não o tipo de vínculo (`Associado/Fundador`, `Convidado`, `Simpatizante`) e não é um perfil de acesso.

## 3. Criar o Presidente do clube (conta funcional)

**Perfil:** Administrador Geral.  
**Caminho:** **Equipe** → **Cadastrar funcionário** (`/Equipe`).

Preencha:

- Nome: `Marina Presidente Teste`.
- Login: um identificador fictício ainda não utilizado, por exemplo `presidente.cct.teste`.
- Papel inicial: **Presidente do clube**.
- Clube de atuação: **Clube Central de Testes** (ou o clube escolhido).
- Senha e confirmação: senha nova de pelo menos **12 caracteres**, não registrada neste guia.

Clique em **Cadastrar**. Depois, em **Equipe**, abra **Gerenciar permissões** para conferir a atribuição `PresidenteClube` no clube correto.

**Teste opcional:** saia da conta do Administrador, entre com o login do Presidente e confira **Meus clubes**, **Solicitações** e **Espaços**. Depois volte ao Administrador para continuar os cadastros.

## 4. Criar um titular da categoria Familiar

**Perfil:** Administrador Geral.  
**Caminho:** **Titulares** → **Cadastrar titular** (`/Titulares`).

Exemplo:

| Campo | Exemplo / orientação |
| --- | --- |
| Nome | Lucas Titular Teste |
| CPF | Onze dígitos numéricos **fictícios e únicos** (não use CPF real). |
| Matrícula única | `CCT-TESTE-001` (ou outro código ainda não utilizado). |
| Clube de origem | Clube Central de Testes. |
| Categoria | **Familiar**. |
| Tipo de vínculo | **Associado/Fundador** (é criado pelo script `002`). |
| Data de início | Data atual ou anterior, conforme a demonstração. |
| Outros dados pessoais | Preencha os obrigatórios; dados opcionais podem ficar vazios. |

Salve. O titular deve aparecer em **Titulares e associações**, com situação **Ativa** e matrícula identificável.

**Primeira mensalidade:** se a associação começou em **11/11/2026**, a primeira competência permitida é **01/12/2026**, com vencimento em **10/12/2026**. Para outras datas, use sempre o primeiro dia do mês seguinte à adesão.

### Criar o login do titular (passo separado)

Ainda na lista de **Titulares**, clique em **Conta** na linha do Lucas. Se ele ainda não tiver usuário, o sistema abre **Criar conta de Titular**.

- Login de exemplo: `lucas.cct.teste`, se estiver disponível.
- Defina e confirme uma senha individual com no mínimo 12 caracteres.
- Salve e confira que a conta está ativa.

O cadastro da associação **não cria automaticamente as credenciais de login**. O titular só conseguirá entrar após essa etapa.

## 5. Criar um dependente e seu login

**Perfil:** Administrador Geral.  
**Caminho:** **Titulares** → linha do Lucas → **Dependentes** → **Cadastrar dependente**.

**Regra atualizada:** cadastre o titular na categoria **Familiar** para este teste. A categoria **Individual** não admite novos dependentes ou reativação de vínculos inativos; vínculos antigos só serão regularizados manualmente pela administração.

- Nome fictício: `Lia Dependente Teste`.
- Data de nascimento: uma data fictícia coerente com a idade do cenário. Para testar autorização de menores, escolha menos de 18 anos.
- CPF do dependente: opcional na tela; se informar, use um número fictício **diferente** do titular.
- Salve e confira que Lia aparece vinculada à associação do Lucas.

Na lista de **Dependentes**, clique em **Conta** na linha de Lia, defina login (`lia.cct.teste`, se livre) e senha própria. O dependente pode entrar sem usar o login do titular.

**Teste rápido:** saia, entre como Lia, abra **Minha associação** e **Reservas**; ela não deve ter menu **Financeiro**.

## 6. Criar um espaço pago

**Perfil:** Administrador Geral (o Presidente e o Funcionário da unidade também têm gestão de espaços dentro de seus escopos).  
**Caminho:** **Espaços** → **Cadastrar espaço** (`/Espacos`).

| Campo | Exemplo |
| --- | --- |
| Clube | Clube Central de Testes |
| Nome | Churrasqueira Piloto |
| Tipo | **Churrasqueira** |
| Capacidade | 20 pessoas |
| Taxa base | **R$ 100,00** |

Salve e confirme que o espaço aparece na lista. `TaxaBase = 0` significa espaço gratuito; mantenha **R$ 100,00** neste primeiro espaço para testar o financeiro.

### Etapa indispensável: cadastrar horário de funcionamento

Na linha do espaço, clique em **Agenda**. A página apresenta **Horários de funcionamento semanais**.

1. Escolha o dia da semana em que pretende fazer a primeira reserva (por exemplo, sábado).
2. Informe **Abertura = 09:00** e **Fechamento = 21:00**.
3. Deixe **Termina no dia seguinte** desmarcado.
4. Clique em **Adicionar horário**.
5. Para reservar em outros dias da semana, cadastre os horários desses dias também.

**Sem horário cadastrado, uma reserva será rejeitada como fora do funcionamento**, mesmo que o espaço esteja ativo e desocupado. Evite criar manutenção sobre o intervalo de demonstração.

## 7. Configurar o valor da mensalidade

**Perfil:** Administrador Geral.  
**Caminho:** **Financeiro** → **Valores mensais** → **Cadastrar valor** (`/Financeiro/Politicas`).

| Campo | Exemplo |
| --- | --- |
| Clube | Clube Central de Testes (**clube de origem do Lucas**) |
| Categoria | Familiar |
| Mensalidade | **R$ 150,00** |
| Início de vigência | Primeiro dia do mês da competência que será emitida; no exemplo, `01/12/2026`. |

Clique em **Salvar política**. A política deve aparecer na listagem. **Não cadastre novamente a mesma combinação de clube, categoria e início de vigência.**

A mensalidade é por **associação**, não por dependente e não pelo clube prestador de uma eventual locação externa.

## 8. Fazer a primeira reserva paga

**Perfil:** Titular (Lucas). Faça **Sair** e entre com `lucas.cct.teste`.  
**Caminho:** **Reservas** → **Nova reserva** (`/Reservas`).

Preencha:

1. **Clube e espaço:** `Clube Central de Testes — Churrasqueira Piloto`.
2. **Início e fim:** escolha uma data **futura** no dia de funcionamento cadastrado, por exemplo **14:00 às 16:00**. Para simplificar, escolha pelo menos 48 horas à frente.
3. **Quantidade de participantes:** `2` (respeitando a capacidade).
4. **Finalidade:** `Teste de reserva avulsa`.
5. **Convidados:** deixe em branco neste primeiro teste.
6. Clique em **Confirmar reserva**.

**Resultado esperado:** a reserva aparece em **Minhas reservas** com situação `Confirmada` e taxa simulada de R$ 100,00 **EmAberto**. **Anote o ID da reserva.**

Se não funcionar, consulte o quadro de erros ao final; a causa mais comum é horário de funcionamento ausente ou divergente.

## 9. Fazer a segunda reserva paga

Continue como **Lucas** em **Reservas** → **Nova reserva**.

Repita a operação na mesma Churrasqueira, mas use **outra data ou horário futuro, sem sobreposição** com a primeira. Finalidade: `Teste de transferência para mensalidade`.

**Resultado esperado:** outra reserva `Confirmada`, também com taxa `EmAberto` de R$ 100,00. Anote o segundo ID.

**Importante:** são duas reservas diferentes. A decisão financeira de cada cobrança fica registrada e não pode ser trocada depois. A associação só pode ter **3 reservas confirmadas por mês de utilização**, somando titular, dependentes e clubes. Se o limite já foi atingido por testes anteriores, reutilize reservas futuras elegíveis ou libere cota cancelando uma reserva **que ainda não tenha cobrança transferida**.

## 10. Testar quitação avulsa da primeira reserva

**Perfil:** Titular.  
**Caminho:** **Reservas** → primeira reserva paga → **Cobrança**.

Confira a tela e clique em **Registrar quitação avulsa**; confirme a ação.

**Resultado esperado:**

- Cobrança passa de `EmAberto` para `QuitadaSimulada`.
- Valor simulado quitado: **R$ 100,00**, sem acréscimo.
- A cobrança **não** é adicionada à mensalidade.
- Uma tentativa posterior de mudar a decisão para transferência não é permitida.

Não existe PIX, boleto ou transação bancária real neste teste.

## 11. Testar transferência da segunda reserva

**Perfil:** Titular.  
**Caminho:** **Reservas** → segunda reserva paga → **Cobrança**.

Confira a prévia:

| Item | Valor esperado no exemplo |
| --- | ---: |
| Principal da locação | R$ 100,00 |
| Acréscimo único de 10% | R$ 10,00 |
| Total da transferência | **R$ 110,00** |
| Competência de destino | Primeira mensalidade futura elegível (ex.: **12/2026**) |
| Vencimento | Dia **10** da competência escolhida |

Clique em **Confirmar transferência**.

**Resultado esperado:** cobrança `Transferida`, com referência à fatura de destino. Se a fatura ainda não foi emitida pelo Administrador, ela pode aparecer com situação **`EmPreparacao`**: isso é esperado, não é erro.

**Não cancele essa segunda reserva nesta etapa.** O ajuste financeiro de cancelamentos de cobranças transferidas será implementado na **Etapa 5**. Uma fatura já quitada não pode ser alterada por novas transferências.

## 12. Emitir a mensalidade que recebeu a locação

**Perfil:** Administrador Geral. Faça **Sair** da conta do Lucas e entre como Administrador.  
**Caminho:** **Financeiro** → **Emitir mensalidade** (`/Financeiro/Emitir`).

1. Selecione a **associação/matrícula de Lucas**.
2. Selecione a **competência mostrada na transferência** (dia **1º** do mês; no exemplo, `01/12/2026`).
3. Clique em **Emitir fatura**.
4. Abra os **Detalhes** da fatura resultante.

**Resultado esperado no exemplo:**

| Item da fatura | Valor |
| --- | ---: |
| Mensalidade-base da associação | R$ 150,00 |
| Principal da segunda locação | R$ 100,00 |
| Acréscimo único da transferência | R$ 10,00 |
| **Total** | **R$ 260,00** |

A fatura deve passar para `EmAberto` e vencer no dia **10** da competência. Emitir novamente **a mesma competência** não deve criar cobrança duplicada.

Se você ainda pretende criar mais reservas com transferência para essa competência, **não quite a mensalidade ainda**. Isso preserva o exemplo para outros testes.

## 13. Quitar a mensalidade (quando terminar os demais testes)

**Perfil:** Titular Lucas ou Administrador Geral.  
**Caminho:** **Financeiro** → fatura `EmAberto` → **Detalhes** → **Registrar quitação integral simulada**.

**Resultado esperado:** situação `QuitadaSimulada`; os itens e o total histórico ficam preservados; a quitação não envolve movimentação real. Não é possível quitar fatura em `EmPreparacao` antes da emissão da mensalidade-base.

**Regra de atraso:** vencimento no dia 10; somente a partir do **11º dia de atraso** incide multa **única de 2% sobre o total vencido**, incluindo locações transferidas, sem juros. A partir desse atraso também são bloqueadas novas reservas do titular e dependentes, com liberação mediante quitação simulada. **Não altere o relógio do computador ou datas do banco de produção/demonstração para forçar esse cenário.** Deixe testes de datas extremas para o Codex, em base isolada.

## 14. Testes opcionais rápidos dos Componentes 2 e 3

Faça somente depois de completar o roteiro principal, com dados fictícios:

1. **Login do dependente:** entrar como Lia e verificar **Minha associação** e **Reservas**. Não deve existir decisão de cobrança ou menu Financeiro.
2. **Permissão de menor para espaço pago:** entrar como Lucas → **Reservas** → **Permissões de dependentes**; autorizar Lia a realizar reservas pagas. A permissão é geral e revogável. Sem ela, Lia menor de idade só pode reservar espaço gratuito.
3. **Reserva gratuita:** como Administrador, cadastrar um espaço com taxa **R$ 0,00**, configurar seu horário em **Agenda** e, como Lia, criar uma reserva futura. Não deve surgir cobrança financeira.
4. **Cancelamento simples:** cancelar uma reserva futura **gratuita** ou uma cobrança ainda `EmAberto` que não foi transferida; a cota mensal de reservas confirmadas deve ser liberada. Não use a reserva transferida do passo 11.
5. **Agenda e conflito:** tentar reservar o mesmo espaço em intervalo já ocupado; a API deve impedir sobreposição.
6. **Segundo clube (multiunidades):** como Administrador, criar outro clube e um espaço pago com horário. Como Lucas, criar reserva nele; a reserva pertence ao clube prestador, mas uma eventual transferência é cobrada na mensalidade do **clube de origem de Lucas**. Para não exceder a cota, faça isso com uma associação de teste separada ou após liberar uma vaga.
7. **Solicitação de adesão:** sair do sistema → **Solicitar adesão**; preencher dados fictícios e escolher vínculo `Convidado` ou `Simpatizante`. Depois, como Presidente do clube correto ou Administrador, entrar em **Solicitações** → **Analisar**. Para aprovar, informar categoria e matrícula únicas. Isso cria vínculo associativo, **não cria o login automaticamente**. Para o roteiro principal, o cadastro direto de titular no passo 4 é mais curto.

## 15. Consulta SQL de conferência da Etapa 4

Depois dos testes 10–13, no MySQL Workbench ou cliente equivalente, abra:

`database/tests/013_financeiro_locacoes_validacao.sql`

Execute as consultas **somente leitura**. As consultas **2 a 7 devem retornar zero linhas**; as consultas **1 e 8** mostram as cobranças e os lançamentos efetuados.

**Não execute novamente** `database/scripts/010_financeiro_base.sql` para testar: esse é um script de estrutura e já deve ter sido aplicado.

## 16. Se algum passo falhar

| Sintoma | Verificação mais provável |
| --- | --- |
| Login de Lucas/Lia falha | O cadastro da pessoa foi feito, mas a **Conta** de acesso ainda não foi criada; ou o login/senha não corresponde. |
| Clube não aparece em um seletor | O clube está inativo ou o perfil logado não possui o escopo adequado. |
| Não consigo criar titular | Confira categoria ativa, vínculo associativo, clube ativo, CPF de 11 dígitos, matrícula única e campos obrigatórios. |
| Reserva diz “fora do horário” | Abra **Espaços → Agenda** e cadastre o **dia exato da semana** e um horário que englobe todo o período. |
| Reserva diz “espaço indisponível” | Confira espaço/clube ativo, manutenção, sobreposição e limite de reservas. |
| Reserva de salão é recusada | Salão exige **24 horas exatas** de antecedência. Para os primeiros testes, use churrasqueira com data futura. |
| Dependente menor não reserva espaço pago | O titular precisa habilitar a permissão individual em **Reservas → Permissões de dependentes**. |
| Botão **Cobrança** não aparece | Entre como **Titular** e consulte uma reserva **paga, confirmada e futura**; dependentes não fazem decisões financeiras. |
| Cobrança não pode mais ser decidida | Ela já foi quitada/transferida, a reserva foi cancelada, a associação não está ativa ou o uso já começou. |
| Transferência vai para outro mês | A competência seguinte pode estar quitada/cancelada; a API escolhe a primeira **futura elegível**. Confirme a competência mostrada antes de prosseguir. |
| Fatura está em `EmPreparacao` | Ela recebeu a locação antes da emissão. Entre como Administrador e **Emita a mensalidade-base** da competência. |
| Não consigo emitir mensalidade | Verifique política vigente para **clube de origem + categoria** e competência a partir do **mês seguinte à adesão**. |
| Não consigo cancelar cobrança `Transferida` | **Bloqueio esperado** até a Etapa 5, que tratará estornos e créditos. |

## 17. Registro do que foi realmente testado

Preencha depois da execução; **não marque itens como aprovados antecipadamente**.

| Passo | Resultado | Data/observação |
| --- | --- | --- |
| 0. API/Web iniciaram e build passou | Pendente | |
| 1–3. Clube, categoria e Presidente | Pendente | |
| 4–5. Titular, dependente e logins | Pendente | |
| 6–7. Espaço, agenda e política mensal | Pendente | |
| 8–9. Duas reservas pagas | Pendente | |
| 10. Quitação avulsa | Pendente | |
| 11. Transferência + 10% | Pendente | |
| 12. Emissão e conferência da fatura | Pendente | |
| 13. Quitação de mensalidade | Pendente | |
| 15. Consultas SQL | Pendente | |

Os cenários aprofundados de concorrência, autorização, competência alterada entre prévia/confirmação e regressão permanecem no arquivo `docs/TESTES_PENDENTES_CLICKCLUB.md` para execução posterior pelo Codex, conforme o fluxo aprovado.
