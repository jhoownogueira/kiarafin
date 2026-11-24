# Open Finance com Meu Pluggy

O KiaraFin integra um Item já autorizado no Meu Pluggy. A aplicação não cria
uma nova conexão bancária no frontend e não solicita atualização manual do
Item. A atualização normal vem do provedor, por webhooks, com uma verificação
periódica de contingência a cada 24 horas por padrão.

A integração é restrita ao usuário definido em `PLUGGY_AUTHORIZED_EMAIL`. As
rotas de consulta, prévia e ativação exigem autenticação e conferem esse
proprietário no backend. O Item configurado também é validado antes de qualquer
importação. O cadastro de usuários é uma configuração independente e permanece
desativado por padrão.

## Configuração

O backend lê estas opções:

| Opção .NET | Variável de produção | Tipo | Padrão |
| --- | --- | --- | --- |
| `Pluggy:Enabled` | `PLUGGY_ENABLED` | Secret | `false` |
| `Pluggy:BaseUrl` | `PLUGGY_BASE_URL` | Secret | `https://api.pluggy.ai` |
| `Pluggy:ClientId` | `PLUGGY_CLIENT_ID` | Secret | sem valor |
| `Pluggy:ClientSecret` | `PLUGGY_CLIENT_SECRET` | Secret | sem valor |
| `Pluggy:ItemId` | `PLUGGY_ITEM_ID` | Secret | sem valor |
| `Pluggy:WebhookSecret` | `PLUGGY_WEBHOOK_SECRET` | Secret | sem valor |
| `Pluggy:AuthorizedEmail` | `PLUGGY_AUTHORIZED_EMAIL` | Secret | sem valor |
| `Pluggy:SyncIntervalHours` | `PLUGGY_SYNC_INTERVAL_HOURS` | Secret | `24` |

Em variáveis de ambiente .NET, `:` é representado por `__`; por exemplo,
`Pluggy__ClientId`. O Compose traduz as variáveis `PLUGGY_*` acima para essas
opções e as injeta somente no container da API.

Para ativar em produção:

1. Cadastre todas as opções `PLUGGY_*` em GitHub Actions **Repository secrets**.
2. Mantenha `PLUGGY_ENABLED=false` durante a configuração e o primeiro deploy.
3. Configure o webhook HTTPS pela API Pluggy com o mesmo segredo no cabeçalho.
4. Altere o secret `PLUGGY_ENABLED` para `true` e faça o deploy.
5. Entre no KiaraFin como o usuário autorizado, confira a prévia e confirme a
   substituição na interface.

O deploy e as migrations nunca executam a importação. A confirmação da
interface é a única entrada para a primeira substituição.

## Primeira importação

Antes de mostrar a confirmação, o backend autentica na Pluggy, confere o Item e
o consentimento, percorre todas as páginas de contas e transações e prepara os
relacionamentos. Resposta inconsistente, paginação incompleta, Item divergente,
consentimento vencido ou conjunto de contas vazio impedem a operação.

Depois da confirmação, os dados do Item são gravados em uma única transação
PostgreSQL. Uma falha provoca rollback. Após o sucesso, a integração registra
que a importação inicial terminou e passa a sincronizar automaticamente.

## Sincronização e idempotência

As contas, cartões, faturas e lançamentos preservam os identificadores
externos, com constraints únicas no banco.

Cada sincronização espelha o Item: registros novos são inseridos, alterados
são atualizados e os que sumiram da origem são removidos. As regras de
classificação dos lançamentos de cartão e de montagem das faturas estão em
[financial-rules.md](financial-rules.md).
Contas `CREDIT` viram cartões; contas `BANK` viram contas com o saldo da
instituição. Faturas fechadas vêm de `/bills`.

Valores usam tipos decimais; timestamps são armazenados como instantes UTC e a
interface apresenta datas no fuso do usuário. Reexecutar a sincronização com o
mesmo conjunto de origem deve produzir o mesmo estado sem duplicações.

Quando as notificações Telegram estão habilitadas, uma sincronização bem-sucedida
que altere contas, cartões ou lançamentos envia um resumo privado ao chat
definido em `TELEGRAM_CHAT_ID`. A API também valida o e-mail configurado em
`TELEGRAM_AUTHORIZED_EMAIL`; não existe endpoint de entrada do Telegram,
portanto mensagens de outros usuários não são processadas nem respondidas. O
token do bot permanece somente no backend como secret e nunca é incluído no
frontend ou nos logs da aplicação.

Webhooks apenas registram/disparam trabalho persistente e respondem rapidamente.
O worker consulta a API autenticada antes de aplicar mudanças quando o evento
não for fonte suficiente. Webhook e scheduler usam lock PostgreSQL por
integração para impedir duas sincronizações simultâneas.

## Webhook

Publique a aplicação exclusivamente por HTTPS e cadastre no recurso Webhook da
Pluggy a URL `https://<dominio>/api/integrations/pluggy/webhook`. Configure o
cabeçalho de autenticação `X-Pluggy-Webhook-Secret` com o mesmo valor de
`PLUGGY_WEBHOOK_SECRET`.

O backend rejeita segredo ausente ou inválido e Item ID que não pertença à
integração autorizada. Eventos repetidos são idempotentes. Somente eventos
oferecidos pelo conector devem ser selecionados no Dashboard; a integração não
depende de que todos sejam emitidos em todos os cenários.

Nunca coloque o segredo na URL, no frontend, em capturas de tela ou em logs.
Para trocar o segredo, atualize a configuração no Dashboard Pluggy e o GitHub
Secret de forma coordenada, depois faça novo deploy.

## Endpoints

As operações do proprietário usam autenticação normal do KiaraFin:

| Método e rota | Função |
| --- | --- |
| `GET /api/integrations/pluggy/status` | Consulta status, consentimento e última sincronização |
| `POST /api/integrations/pluggy/preview` | Valida a origem e retorna a prévia da substituição |
| `POST /api/integrations/pluggy/activate` | Executa a substituição inicial com corpo `{ "confirmed": true }` |
| `POST /api/integrations/pluggy/sync` | Solicita sincronização incremental |
| `POST /api/integrations/pluggy/webhook` | Recebe evento com `X-Pluggy-Webhook-Secret` |

O endpoint de webhook não usa a sessão do usuário porque é chamado pela Pluggy;
o segredo do cabeçalho e a associação do Item fazem sua autenticação. Os demais
endpoints rejeitam usuários anônimos ou qualquer usuário diferente do
proprietário configurado.

## Consentimento e falhas

O status usa a data `consentExpiresAt` informada pelo Item. Perto do vencimento,
o KiaraFin mostra a data real e orienta a renovação pelo Dashboard Pluggy. Após
o vencimento, a sincronização é suspensa e os dados existentes são preservados.
Depois da renovação, atualize `PLUGGY_ITEM_ID` caso o Item tenha mudado. O
backend valida o novo Item antes de substituir a associação e mantém os dados
anteriores se a validação falhar. Como a sincronização é espelhada, se o novo
Item trouxer contas com identificadores diferentes, as antigas são removidas e
as novas são importadas.

Erros temporários, timeouts e respostas vazias inesperadas não significam que
as transações foram removidas. Nesses casos, a última versão sincronizada é
mantida, o erro sanitizado é registrado e uma tentativa posterior pode ocorrer.

## Limitações dos dados de origem

A representação de cartões e faturas depende dos campos disponibilizados pela
instituição e pelo plano Pluggy. Limite, saldo utilizado, fechamento,
vencimento, parcelas e faturas só aparecem quando a origem fornece informação
confiável. A categoria exibida em cada lançamento é a retornada pela Pluggy.

A próxima atualização bancária não é exibida como horário exato quando a
Pluggy não fornece esse dado. O horário mostrado para a próxima verificação é o
do scheduler do KiaraFin, não uma promessa de atualização da instituição.

## Diagnóstico seguro

Consulte o status pela tela Open Finance e os logs do container da API:

```bash
docker compose --env-file .env.production -f docker-compose.production.yml \
  logs --tail=200 api
```

Os logs devem conter apenas códigos de status, IDs técnicos necessários e erro
sanitizado. Client Secret, API key temporária, segredo do webhook, payloads
financeiros completos e cabeçalhos de autenticação nunca devem ser registrados.
