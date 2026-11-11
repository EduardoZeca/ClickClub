# ClickClub — Componente 4, Etapa 6: escopos financeiros

**Data:** 11/11/2026  
**Situação:** código e documentação preparados; build e testes do usuário pendentes.

## Instalação e compilação

1. Aplicar ZIP incremental da Etapa 6 **depois** de aplicar as Etapas 1–5, incluindo migração 011, sobre o projeto local.
2. Nesta etapa, **não existe nova migração SQL**. Não reaplicar 010 nem 011.
3. Na raiz do projeto, executar:

   ```powershell
   dotnet build ClickClub.slnx
   ```

4. Iniciar API e Web pelos comandos habituais.

## Cenário mínimo — dois clubes

Prepare ou reutilize dados fictícios, de preferência em banco isolado:

- Clube A com Tesoureiro A, espaço pago (ex.: Quadra) e horários válidos.
- Clube B com Tesoureiro B e uma associação titular B.
- Titular B faz uma reserva paga **no espaço do Clube A**, transfere a cobrança para a mensalidade da associação B e o Administrador Geral emite essa mensalidade.

### Verificações manuais prioritárias

1. **Tesoureiro A:** após login Web, abrir `Financeiro` → `Locações dos clubes prestadores`. A reserva do titular B deve aparecer como locação **prestada pelo Clube A**, com principal e 10%.
2. **Tesoureiro A:** em `Financeiro`, a fatura do titular B **não** aparece. Copiar o ID dessa fatura e tentar abrir a URL `/Financeiro/Detalhes/ID`: resposta HTTP 404 esperada. Tentar `POST /api/financeiro/faturas/ID/quitar` autenticado como Tesoureiro A deve retornar 404 e não alterar a fatura.
3. **Tesoureiro B:** em `Financeiro`, a mensalidade do titular B deve aparecer, inclusive o item da locação prestada pelo A. Pode registrar quitação simulada dessa mensalidade. Em `Locações dos clubes prestadores`, a locação do A **não** aparece.
4. **Administrador Geral:** deve visualizar as faturas de A e B e as locações dos dois clubes. Continua sendo o único perfil capaz de cadastrar políticas e emitir mensalidades.
5. **Titular B:** vê apenas suas mensalidades e seus detalhes, sem acesso ao relatório de locações ou a faturas de outras associações.

### Conferência no banco

`database/tests/016_escopos_financeiros_validacao.sql` é **somente leitura**. Use-o para localizar IDs das faturas e reservas, conferir escopos cadastrados e verificar qual clube recebeu os itens transferidos. O SQL **não simula autorização**: os testes de negação precisam ser feitos por API e Web com tokens de perfis diferentes.

## Testes profundos delegados ao Codex

TP-010 em `docs/TESTES_PENDENTES_CLICKCLUB.md`: usuário com múltiplos clubes, revogação de papel, múltiplos perfis, acesso direto a outros IDs, quitação simultânea, regressão de faturas quitadas e cancelamentos da Etapa 5.

**Atenção:** compilação bem-sucedida não demonstra que as restrições financeiras funcionam. O Componente 4 só deve ser declarado completamente testado após execução dos cenários e revisão dos resultados.
