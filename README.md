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

Componente 1 — Fundação em desenvolvimento.

Os módulos de autenticação, reservas, financeiro,
portaria, atividades e Mobile serão implementados
gradualmente.