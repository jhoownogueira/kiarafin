# Regras financeiras

## Convenções

Este documento define as regras financeiras implementadas na primeira versão.
As limitações conhecidas aparecem explicitamente em cada seção.

- Dinheiro usa `decimal`; valores persistidos terão precisão explícita.
- Instantes de auditoria usam UTC.
- Competência, vencimento, pagamento e compra são datas locais sem conversão
  silenciosa de fuso.
- Operações críticas são atômicas e recebem proteção contra duplicação.
- Lançamentos históricos mantêm os dados necessários para não mudar de sentido
  quando uma categoria, conta ou membro é renomeado ou desativado.

## Saldo de conta

O saldo atual de uma conta é:

```text
saldo atual = saldo inicial
            + receitas recebidas
            - despesas pagas em dinheiro/conta
            + transferências recebidas
            - transferências enviadas
            - pagamentos de fatura efetuados pela conta
```

Não afetam o saldo atual:

- receitas e despesas pendentes;
- lançamentos cancelados;
- compra no cartão de crédito;
- despesa de consumo representada por uma compra no cartão;
- previsão mensal de salário, renda extra ou conta fixa.

Uma transferência é registrada como uma única operação com duas movimentações
vinculadas. Ela altera os saldos das contas, mas não é receita nem despesa nos
relatórios do núcleo familiar.

Editar, cancelar ou excluir uma operação deve recalcular seu efeito de forma
transacional. Operações já conciliadas ou ligadas a uma fatura paga devem exigir
um fluxo explícito de estorno, não uma alteração silenciosa.

## Competência e caixa

Relatórios de consumo usam a despesa na competência apropriada. Relatórios de
caixa usam a movimentação que realmente alterou uma conta. Essa separação evita
considerar a compra de cartão e o pagamento da mesma fatura como duas despesas.

Para cartão:

- a compra registra o consumo e a obrigação;
- o pagamento da fatura registra apenas saída de caixa e liquidação da
  obrigação;
- o pagamento não cria uma segunda despesa de consumo.

## Saldo projetado

Na primeira versão, para uma data de corte e um horizonte selecionado:

```text
saldo projetado = saldo atual disponível
                 + receitas pendentes previstas no horizonte
                 - despesas pendentes previstas no horizonte
                 - faturas de cartão previstas no horizonte
```

Além da projeção operacional, o dashboard possui uma análise de planejamento de
seis meses. Ela repete as premissas mensais cadastradas pelo usuário e cruza
salários, rendas extras e contas fixas com faturas oficiais e faturas de cartão
em formação. Essas premissas nunca criam lançamentos ou movimentações de conta.
Essa análise começa em zero e não soma o saldo das contas. A fatura vigente é
apresentada no mês atual, ainda que o vencimento bancário ocorra no mês seguinte;
as faturas posteriores são distribuídas nos meses seguintes da projeção.

Uma compra já incluída em uma fatura prevista não é somada novamente como
despesa pendente de caixa. Parcelas futuras entram pela fatura correspondente,
nunca em duplicidade pelo valor total da compra.

Limitações atuais:

- a projeção depende dos valores previstos informados pelo usuário;
- não estima juros, câmbio, rendimento ou saldo bancário externo;
- valores de salário, extras e contas fixas são premissas manuais e permanecem
  iguais até que o usuário os edite;
- pagamentos parciais de fatura não serão apresentados como suportados até que
  haja regra, persistência e testes específicos.

## Status de receitas e despesas

- `Pending`: previsto, sem impacto no saldo atual e com impacto na projeção.
- `Paid`/`Received`: realizado, com impacto no saldo da conta.
- `Overdue`: pendente cuja data de vencimento passou; continua na projeção.
- `Canceled`: sem impacto em saldo ou projeção.

A transição para pago/recebido exige conta e data de liquidação. Repetir a mesma
operação não pode aplicar o impacto duas vezes.

## Cartões e faturas

Cada cartão tem dia de fechamento e vencimento. A função de calendário deve ser
única e testada para meses curtos e viradas de ano.

Regra de alocação implementada:

- compra anterior ao fechamento pertence à fatura do ciclo corrente;
- compra no dia do fechamento pertence ao ciclo definido de forma consistente
  pela política do produto;
- compra após o fechamento pertence à próxima fatura;
- se fechamento ou vencimento não existir no mês, usa-se o último dia válido.

A compra feita no dia do fechamento pertence ao ciclo seguinte, de forma
consistente entre compras à vista e parceladas.

Uma compra no cartão:

1. cria despesa de consumo;
2. associa a cobrança à fatura correta;
3. compromete limite;
4. não altera a conta bancária.

O pagamento integral da fatura, em transação de banco:

1. valida que a fatura ainda admite pagamento;
2. cria a saída de caixa na conta escolhida;
3. marca a fatura como paga;
4. libera o limite correspondente;
5. preserva as compras como despesas de consumo sem duplicá-las.

Na primeira versão, pagamento parcial deve permanecer indisponível caso não seja
implementado integralmente. A interface e a API não devem simular suporte.

## Parcelas e arredondamento

O compromisso total de uma compra parcelada no cartão reduz o limite disponível
desde a compra. Cada parcela é ligada à fatura calculada para sua data.

O rateio trabalha em centavos:

```text
valor base em centavos = total em centavos / quantidade, com divisão inteira
resto = total em centavos % quantidade
```

As primeiras `resto` parcelas recebem um centavo adicional. Assim, todas as
parcelas diferem no máximo em um centavo e sua soma é exatamente igual ao total.

Cada parcela guarda número, quantidade total, valor e data. Constraints impedem
duas parcelas com o mesmo número na mesma compra. Reprocessar a criação não pode
duplicá-las.

## Previsões mensais

Cada previsão contém nome, tipo (`Salary`, `ExtraIncome` ou `FixedExpense`),
valor e dia esperado. Não há início, término ou geração de ocorrência: a linha
representa a premissa vigente e é atualizada manualmente pelo usuário.

- Previsões não alteram contas, faturas, lançamentos ou relatórios.
- O mesmo valor é repetido nos seis meses da análise até ser editado.
- Excluir uma previsão apenas a remove dos cálculos futuros.
- Recorrências ativas legadas são migradas como premissas, sem gerar novos
  lançamentos.

## Categorias e responsáveis

Categorias são de receita ou despesa. Desativar uma categoria impede novos usos,
mas não remove lançamentos antigos. Um lançamento pode ser individual ou
compartilhado e ter responsável, sem que isso duplique seu valor.

Relatórios por responsável e compartilhamento são classificações do mesmo
lançamento. A soma desses recortes deve reconciliar com o total do período.

## Idempotência e auditoria

Operações suscetíveis a repetição devem ter chave ou constraint própria:

- rotação de refresh token;
- transferência;
- sincronização de dados externos;
- criação das parcelas;
- associação de compra à fatura;
- pagamento de fatura.

Criação, atualização, cancelamento e estorno registram timestamps e identidade
responsável. Logs não devem incluir senhas, access tokens, refresh tokens ou
dados sensíveis completos.
