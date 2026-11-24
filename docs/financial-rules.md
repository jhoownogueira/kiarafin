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
  fechada conhecida (exceto pagamentos), mais as parcelas projetadas.
- **Parcelas projetadas:** o Open Finance só publica cada parcela quando ela é
  faturada. Quando a última parcela conhecida de uma compra está na última
  fatura fechada, a parcela seguinte é projetada na fatura atual e as demais
  nas próximas faturas, com o valor da última parcela. As parcelas de uma
  mesma compra são agrupadas por descrição, data da compra e quantidade de
  parcelas. Se a última parcela conhecida está numa fatura mais antiga, a
  compra é considerada quitada e nada é projetado.
- **Parcelas futuras** também incluem lançamentos com data posterior a hoje e,
  para bancos que publicam todas as parcelas de uma vez, as parcelas de número
  maior que a menor parcela ainda em aberto da mesma compra.
- Os pagamentos listados em uma fatura (`bill.payments`) são os feitos durante
  o ciclo dela, ou seja, quitam a fatura anterior. Uma fatura fechada está paga
  quando esses pagamentos, ou os lançamentos de pagamento após o fechamento,
  cobrem o total (tolerância de 1%).

## Apelidos e categorias de lançamentos

Na fatura, cada lançamento pode receber um apelido e uma categoria do usuário. A
regra é salva pela descrição normalizada (maiúsculas, sem sufixo de parcela e
sem espaços repetidos) e vale para todos os lançamentos de cartão com essa
descrição, inclusive os próximos e as parcelas projetadas. Apelido e categoria
vazios removem a regra. A regra não altera valores nem o total da fatura.

## Previsão de saldo

A Visão geral mostra o mês atual:

```text
resultado = salários + rendas extras − contas fixas − fatura atual dos cartões
```

A fatura atual de cada cartão é a fatura aberta (com as parcelas projetadas)
somada ao saldo de faturas fechadas que ainda não venceram nem foram pagas. Salários, rendas extras e
contas fixas são as premissas cadastradas em Previsões.
