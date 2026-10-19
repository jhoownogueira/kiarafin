# Arquitetura do KiaraFin

## Estado e objetivo

Este documento descreve a arquitetura implementada no MVP funcional. Evoluções
posteriores estão registradas no roadmap do `README.md`.

O KiaraFin será um monólito modular. A escolha mantém implantação e transações
simples sem misturar regras de domínios diferentes ou criar projetos artificiais
para cada camada.

## Visão geral

```text
Next.js :3000
    |
    | Axios + JWT de curta duração
    v
ASP.NET Core :5134
    |
    | EF Core / Npgsql
    v
PostgreSQL :5432
```

Em desenvolvimento, o perfil `Iniciar KiaraFin` executa a API por HTTP e usa o
SpaProxy para iniciar o Next.js. Em produção, origem, cookies e transporte seguro
deverão ser configurados para HTTPS.

## Backend

A API é organizada por funcionalidade, mantendo contratos, validações e
operações próximos ao domínio que representam:

```text
KiaraFin.Api/
├── Common/
│   ├── Exceptions/
│   ├── Security/
│   └── Web/
├── Data/
│   ├── Configurations/
│   ├── Migrations/
│   └── AppDbContext.cs
├── Features/
│   ├── Auth/
│   ├── Households/
│   ├── Accounts/
│   ├── Categories/
│   ├── Transactions/
│   ├── RecurringTransactions/
│   ├── Installments/
│   ├── CreditCards/
│   ├── Dashboard/
│   └── Reports/
└── Program.cs
```

Princípios:

- Endpoints ou controllers finos; regras financeiras ficam em serviços de
  aplicação/domínio testáveis.
- Dependências registradas pela injeção nativa do ASP.NET Core.
- Interfaces somente quando há uma fronteira concreta, como relógio, geração
  de tokens ou serviço com mais de uma implementação.
- `ProblemDetails` como formato de erro, com validação antes da mutação.
- Operações financeiras compostas usam transação de banco.
- Consultas são assíncronas, paginadas e projetadas para evitar N+1.
- Valores monetários usam `decimal` no C# e precisão explícita no PostgreSQL.
- Instantes técnicos são persistidos em UTC; competência, compra e vencimento
  usam datas locais explícitas.

### Persistência e isolamento

Entidades financeiras pertencem a um `Household`. Toda consulta e mutação deve
derivar os núcleos permitidos da identidade autenticada. O backend valida a
associação mesmo quando a rota ou o corpo contém `householdId`.

Índices e constraints devem reforçar regras que não podem depender apenas da
aplicação, incluindo e-mail normalizado único, token de convite único, refresh
token único, ocorrência recorrente por período e parcela por compra/número.

Migrations são versionadas em `Data/Migrations` e aplicadas por comando de
manutenção. A API não chama `EnsureCreated()` nem atualiza schema no startup.

### Autenticação

- Senhas com hasher seguro e parâmetros mantidos pela plataforma.
- Access token JWT curto mantido em memória no frontend.
- Refresh token aleatório, armazenado apenas como hash no banco e entregue em
  cookie `HttpOnly`.
- Rotação atômica do refresh token, revogação no logout e detecção de reuso.
- Cookie `Secure` em HTTPS futuro; em HTTP local, configuração restrita ao
  ambiente de desenvolvimento.
- Validação de origem nos endpoints que usam cookie e CORS limitado à origem
  conhecida do frontend.

## Frontend

O frontend segue o App Router e módulos por funcionalidade:

```text
frontend/
├── app/
│   ├── (auth)/
│   └── (app)/
├── components/
│   ├── layout/
│   ├── shared/
│   └── ui/
├── features/
├── hooks/
├── lib/
│   ├── api/
│   ├── formatters/
│   └── utils/
├── providers/
└── types/
```

- Axios centraliza URL, timeout, headers, erros e renovação de sessão.
- Um refresh em andamento é compartilhado por requisições concorrentes.
- TanStack Query gerencia server state, cache e invalidação.
- React Hook Form e Zod validam formulários; o backend continua sendo a
  autoridade final.
- shadcn/ui e Radix formam a base acessível dos componentes.
- Cores, tipografia e marcas vêm de `frontend/public/identidade` e são expostas
  por tokens reutilizáveis.
- Textos usam português brasileiro, moeda usa `Intl.NumberFormat` com `BRL` e
  datas seguem o contexto `America/Sao_Paulo`.

## Fronteiras dos módulos

| Módulo | Responsabilidade principal |
| --- | --- |
| Auth | Credenciais, tokens e sessão |
| Households | Membros, associação e convites |
| Accounts | Contas e saldo de caixa |
| Categories | Classificação estável do histórico |
| Transactions | Receitas, despesas e transferências |
| RecurringTransactions | Modelos e ocorrências mensais |
| Installments | Divisão, compromisso e parcelas |
| CreditCards | Compras, ciclos, faturas e pagamentos |
| Dashboard | Visões agregadas por período |
| Reports | Consultas analíticas e filtros |

Dashboard e relatórios consultam os outros domínios, mas não são responsáveis
por alterar lançamentos.

## Contratos HTTP

Os grupos abaixo estão implementados. Os recursos financeiros usam a rota base
`/api/households/{householdId}` para reforçar o isolamento do núcleo familiar:

| Grupo | Operações previstas |
| --- | --- |
| `/api/auth` | login, refresh, logout e me; `register` permanece apenas como resposta `403` permanente |
| `/api/households` | current, members, invites, accept invite |
| `/api/households/{householdId}/accounts` | CRUD, saldo e histórico |
| `/api/households/{householdId}/categories` | listagem e CRUD de categorias próprias |
| `/api/households/{householdId}/transactions` | CRUD, liquidação, cancelamento e transferência |
| `/api/households/{householdId}/recurring-transactions` | CRUD e geração idempotente |
| `/api/households/{householdId}/installments` | criação e consulta de parcelas |
| `/api/households/{householdId}/credit-cards` | CRUD, compras, faturas e pagamentos |
| `/api/households/{householdId}/dashboard` | resumo por mês e projeções |
| `/api/households/{householdId}/reports` | agregações filtradas e paginadas |

OpenAPI documentará os contratos concretos da implementação.

## Testes

- A suíte xUnit cobre calendário de cartão, rateio de centavos, recorrências,
  saldo, projeção, autenticação e isolamento por `Household`.
- O teste de integração aceita uma conexão PostgreSQL isolada via
  `KIARAFIN_TEST_CONNECTION` e valida migration e precisão monetária.
- O frontend é validado atualmente por lint, checagem TypeScript e build de
  produção; testes de componentes e E2E permanecem no roadmap.

As validações mínimas de entrega serão build da solução, lint e build do
frontend, migrations aplicáveis em banco vazio e suíte automatizada aprovada.
