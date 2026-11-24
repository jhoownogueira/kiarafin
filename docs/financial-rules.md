# Regras financeiras

O KiaraFin é de uso pessoal e espelha o Open Finance (Pluggy). Ele não registra
lançamentos manuais: contas, cartões, faturas e lançamentos vêm da instituição.

## Convenções

- Dinheiro usa `decimal`, com precisão `(18,2)` no PostgreSQL.
- Instantes técnicos usam UTC.
- Datas de lançamento são datas locais de `America/Sao_Paulo`. A Pluggy envia
  ISO8601 em UTC: meia-noite UTC exata é tratada como data pura; qualquer outro
  horário é convertido para o horário de Brasília.

## Sincronização espelhada

Cada sincronização lê o Item completo e deixa o banco igual ao snapshot:
insere o que é novo, atualiza o que mudou e remove o que deixou de existir na
origem (contas, cartões, faturas e lançamentos). Isso evita duplicidade quando
um lançamento pendente é substituído por um lançamento postado com outro id.

## Contas

- Contas do Open Finance usam o saldo (`balance`) retornado pela instituição.
- Contas manuais têm o saldo informado e editado pelo usuário.

## Cartões

Valor e tipo de cada lançamento de cartão:

- O valor usado é `amountInAccountCurrency` quando informado (compras
  internacionais); caso contrário, `amount`.
- Valor positivo é compra (inclui tarifas, juros e IOF) e soma na fatura.
- Valor negativo é pagamento de fatura quando a descrição/categoria indica
  pagamento ou quando o valor é igual ao total de uma fatura fechada; nos demais
  casos é estorno/crédito e reduz a fatura.
- O campo `type` (DEBIT/CREDIT) da Pluggy é ignorado em cartões, pois não é
  confiável para esse tipo de conta.

Montagem das faturas:

- **Faturas fechadas** são as retornadas por `/bills`; o total é o
  `totalAmount` oficial. Lançamentos com `billId` dessa fatura ficam nela.
- **Fatura atual (aberta)** são os lançamentos sem `billId` de uma fatura
  fechada conhecida, exceto pagamentos e parcelas futuras.
- **Parcelas futuras** são lançamentos com data posterior a hoje ou, para
  bancos que publicam todas as parcelas de uma vez, as parcelas de número
  maior que a menor parcela ainda em aberto da mesma compra.
- Uma fatura fechada está paga quando `bill.payments` ou os pagamentos
  lançados no cartão após o fechamento cobrem o total.

## Previsão de saldo

A Visão geral mostra o mês atual:

```text
resultado = salários + rendas extras − contas fixas − fatura atual dos cartões
```

A fatura atual de cada cartão é a fatura aberta somada ao saldo ainda não pago
das faturas fechadas que vencem a partir do mês atual. Salários, rendas extras e
contas fixas são as premissas cadastradas em Previsões.
