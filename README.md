# ClickClub

Sistema acadêmico de gestão de clubes sociais com múltiplas unidades.

## Tecnologias

- .NET 10
- ASP.NET Core Web API
- ASP.NET Core MVC
- MySQL 8.4
- .NET MAUI Android (implementação futura)
- SQLite Mobile (cache/offline limitado)

## Arquitetura

O MySQL é o banco central do sistema.

A API é responsável pelo acesso ao banco, autenticação,
autorização e regras de negócio.

As aplicações Web e Mobile comunicam-se exclusivamente
com a API.

## Requisitos

- .NET SDK 10
- MySQL Server 8.4
- Visual Studio Code
- Visual Studio 2026 para desenvolvimento Android

## Banco de dados

Banco: clickclub_db

Os scripts SQL estão em:

database/scripts/

Execute-os em ordem numérica com um usuário MySQL
que possua permissões de criação de tabelas.

O esquema versionado atual possui os scripts `001` a `009`.
Em um banco novo, execute-os na ordem, verificando antes
os avisos de cada script. **Não execute novamente migrações
`ALTER TABLE` já aplicadas** no banco existente.

A API utiliza o usuário clickclub_api com permissões
restritas ao banco ClickClub.

## Configuração

A string de conexão da API utiliza a chave:

ConnectionStrings:ClickClub

Durante o desenvolvimento, configure essa chave
utilizando o .NET User Secrets.

Nunca armazene senhas no código-fonte ou no Git.

## Executar

Compilar:

dotnet build ClickClub.slnx

Iniciar API:

dotnet run --project src/ClickClub.Api --launch-profile https

Iniciar Web:

dotnet run --project src/ClickClub.Web --launch-profile https

## Endereços locais

API: https://localhost:7201

Web: https://localhost:7202

Diagnóstico: https://localhost:7201/api/diagnostico

Diagnóstico do banco (somente desenvolvimento):
https://localhost:7201/api/diagnostico/banco

## Documentação

Consulte a pasta docs para verificar os requisitos
aprovados e o histórico de desenvolvimento.

## Estado atual

**Componentes 1 a 3 concluídos segundo os testes realizados
e relatados pelo desenvolvedor:**

1. Fundação: solução, acesso à API/MySQL, configuração
   segura, scripts iniciais e backup/restauração.
2. Autenticação e Cadastros: login por nome de usuário,
   sete perfis, clubes/unidades, titulares, dependentes,
   categorias, equipe e análise de solicitações de adesão.
3. Espaços e Reservas: cadastros, agenda, manutenção,
   concorrência, convidados, cancelamentos, limite de
   três reservas mensais por associação e permissão geral
   para dependentes menores em reservas pagas.

**Próximo componente:** 4 — Financeiro Simulado.
Ainda não iniciado nem autorizado para implementação.
Abrange mensalidades, quitação simulada, inadimplência,
taxas de reserva e transferência para mensalidade futura
com acréscimo único de 10%.

**Ajustes de escopo aprovados e ainda não implementados:**
ficha associativa simplificada (sem contrato anual) e
autenticação por nome de usuário ou e-mail. Os parâmetros
de multas/juros de mensalidades atrasadas ainda precisam
ser definidos; não são equivalentes ao acréscimo de 10%
das locações transferidas.

**Etapas futuras:** carteirinha QR/portaria online,
atividades e eventos, avisos/ouvidoria/relatórios e
aplicativo Android com testes físicos de integração.

O histórico e as decisões estão em
`docs/DOCUMENTACAO_CLICKCLUB.md` (v1.3) e
`docs/CONTINUIDADE_CLICKCLUB.md`. Os testes citados
acima foram informados pelo desenvolvedor; esta revisão
documental não executou compilação, testes ou migrações.
