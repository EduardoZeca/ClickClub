# ClickClub — Testes iniciais: categoria Individual sem dependentes

**Data de preparação:** 11/11/2026  
**Situação:** correção preparada; compilação e testes no Windows do desenvolvedor pendentes.  
**Regra aprovada:** a categoria Individual não admite novos dependentes ou reativações. Não realizar conversões nem exclusões automáticas de vínculos existentes.

## Preparar

1. Extrair o ZIP incremental na raiz do ClickClub, preservando a estrutura `src/`, `docs/` e `database/`.
2. Executar `dotnet build ClickClub.slnx`.
3. Reiniciar a API e a Web com os perfis HTTPS habituais.
4. Entrar como **Administrador Geral**.
5. Usar somente titulares e CPFs fictícios, em ambiente de testes ou com backup recente. Não reexecutar migrações SQL.

## Casos mínimos

| Teste | Operação na Web | Resultado esperado |
| --- | --- | --- |
| IND-01 | Em **Titulares**, localizar titular da categoria **Individual**, abrir **Dependentes → Cadastrar dependente** e tentar salvar um novo cadastro. | A API rejeita a inclusão e a Web mostra mensagem explicando que Individual não permite dependentes. Nenhuma pessoa/vínculo novo é criado. |
| IND-02 | Em uma associação **Familiar**, cadastrar um dependente fictício. | Cadastro continua funcionando, com vínculo ativo. |
| IND-03 | Em **Titulares → Editar**, tentar mudar uma associação Familiar **com dependente ativo** para **Individual**. | A API rejeita a mudança. A categoria anterior e o vínculo permanecem intactos. |
| IND-04 | Desativar o dependente de teste Familiar, depois alterar a associação para Individual e tentar **reativar** o mesmo dependente. | A mudança de categoria com todos os dependentes inativos é permitida; a reativação sob Individual é rejeitada. |
| IND-05 | Editar dados pessoais de um dependente **legado já ativo** em associação Individual, sem reativar/criar vínculo. | Edição continua permitida. Não há conversão nem exclusão automática. |

O teste IND-05 só se aplica se houver vínculo legado fictício desse tipo; **não force a criação de registros inválidos** no banco principal para reproduzi-lo. Depois de IND-04, é possível voltar a categoria para Familiar e reativar o dependente, caso seja necessário continuar usando o cenário para outros testes.

## Conferência do banco (somente leitura)

Execute `database/tests/014_categoria_individual_validacao.sql`. O resultado apresenta possíveis **vínculos ativos preexistentes** em associações Individuais. **Não é obrigatório retornar zero linhas**, pois a decisão aprovada preserva registros anteriores até regularização manual. Nenhum comando do script altera dados.

## Profundidade reservada ao Codex

- Repetir tentativas via API autenticada, contornando a Web; confirmar rejeição e rollback do cadastro e da reativação.
- Confirmar que desativação e edição de dependentes legados permanecem possíveis.
- Testar renomear uma categoria que possui dependentes ativos para `Individual`, em banco isolado e sem violar a unicidade dos nomes.
- Testar concorrência de cadastro/reativação com troca de categoria, inclusive falhas transacionais.
- Confirmar que mensalidades emitidas, locações e reservas históricas não são alteradas automaticamente.
- Confirmar que categorias `Familiar` e `Atleta` mantêm o comportamento anterior.

Atualizar o estado em `docs/TESTES_PENDENTES_CLICKCLUB.md` somente após execução e verificação da evidência.
