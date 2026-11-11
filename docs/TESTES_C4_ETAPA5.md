# ClickClub — Componente 4, Etapa 5: cancelamentos e créditos

**Data:** 11/11/2026  
**Situação:** implementação preparada; instalação, compilação e testes do usuário pendentes.

## Instalação

1. Faça backup do banco `clickclub_db`, preservando o arquivo fora do Git.
2. Copie o conteúdo do ZIP incremental para a raiz do projeto, mantendo a estrutura de pastas. Não exclua arquivos anteriores.
3. Execute **uma única vez**, no MySQL Workbench, `database/scripts/011_cancelamentos_creditos.sql`, com usuário administrativo. A migração 010 precisa estar aplicada; não execute 010 novamente.
4. No terminal da raiz do projeto, execute:

   ```powershell
   dotnet build ClickClub.slnx
   ```

5. Inicie API e Web conforme o guia `docs/GUIA_DEMONSTRACAO_CLICKCLUB_ATE_C4_E4.md` e faça login com titular associado a um clube de origem com política financeira vigente.

## Verificação rápida de fluxo nesta etapa

- [ ] **Transferência e cancelamento em fatura aberta:** crie uma reserva paga futura, escolha `Cobrança` → `Transferir para mensalidade`, anote principal + 10% e a fatura de destino. Cancele em `Minhas reservas`. A API deve aceitar, a fatura deve conter crédito negativo discriminado e o total deve diminuir por principal + acréscimo, descontada eventual multa.
- [ ] **Estorno de fatura quitada:** crie outra reserva paga futura e transfira para mensalidade; em `Financeiro`, emita e quite a fatura correspondente (fatura de teste). Cancele a reserva antes do uso. A fatura quitada deve manter status, valor e itens intactos; deve aparecer um crédito futuro correspondente ao estorno líquido.
- [ ] **Compensação automática:** após obter saldo de crédito, emita uma mensalidade futura elegível ou transfira nova locação para ela. O crédito deve aparecer como item negativo e reduzir o total até, no mínimo, R$ 0,00. Saldo restante pode permanecer para competências seguintes.
- [ ] **Quitação avulsa:** crie reserva paga futura, quite a taxa avulsa pela tela de cobrança e cancele antes do uso. Deve gerar crédito de valor original menos eventual multa, sem mexer em fatura já quitada.
- [ ] **Regressão rápida:** uma reserva gratuita pode ser cancelada; um segundo cancelamento da mesma reserva não duplica crédito; tentar cancelar após o início do uso permanece vedado.

### Conferência SQL

Execute `database/tests/015_cancelamentos_creditos_validacao.sql` no Workbench. As três verificações da seção D devem retornar zero linhas. As seções A, B, C e E ajudam a localizar e conferir as reservas criadas, o livro de créditos e os itens de fatura. **Nenhuma consulta desse script altera dados.**

### Atenção aos prazos

O Salão de Festas exige antecedência mínima de 24 horas para criar uma reserva. A multa de cancelamento de salão só é aplicada quando a operação ocorre com **menos de 24 horas antes do início** e antes do uso; é limitada ao menor valor entre R$ 50 e a taxa original. Não altere o relógio do computador ou o banco ativo para fabricar um cenário com multa: deixe esse caso para testes isolados do Codex.

### Testes aprofundados reservados ao Codex

Ver `docs/TESTES_PENDENTES_CLICKCLUB.md`, TP-009:

1. Concorrência de cancelamentos, quitação de fatura versus cancelamento e compensações repetidas.
2. Estorno de R$ 110 ou R$ 60 para locação de R$ 100 transferida, com multa de R$ 50 quando cabível.
3. Créditos superiores à próxima fatura, transporte do saldo, compensação repetida na mesma fatura e ausência de faturas negativas.
4. Crédito originado em cancelamento antigo do Componente 3, recuperado pela migração 011 sem duplicidade.
5. Rejeição de acesso fora da associação e preservação de histórico/auditoria.

**Não considerar esta etapa testada só porque o ZIP foi gerado.** O resultado real de compilação será informado pelo usuário.
