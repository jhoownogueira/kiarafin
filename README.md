# KiaraFin

KiaraFin é uma aplicação de gestão financeira pessoal e familiar. O produto está
em desenvolvimento e terá como primeiro cenário o compartilhamento de um núcleo
financeiro (`Household`) por duas pessoas.

> Status: MVP funcional. Autenticação, núcleo familiar, contas, categorias,
> lançamentos, recorrências, parcelas, cartões, dashboard e relatórios usam a API
> e o PostgreSQL reais. Consulte o roadmap para limitações e evoluções planejadas.

## Stack

- Frontend: Next.js 16 (App Router), React 19, TypeScript strict e Tailwind CSS.
- Backend: ASP.NET Core 10 e C#.
- Persistência: Entity Framework Core, Npgsql e PostgreSQL 17.
- Desenvolvimento integrado: Visual Studio, SpaProxy e Docker Compose.

Consulte [Arquitetura](docs/architecture.md) e
[Regras financeiras](docs/financial-rules.md) antes de alterar os módulos de
domínio.

Para publicar em Ubuntu com Docker, GitHub Actions e Nginx Proxy Manager,
consulte [Deploy no Ubuntu](docs/deployment-ubuntu.md).

## Pré-requisitos

- .NET SDK 10.
- Node.js 22 ou versão compatível com o Next.js instalado.
- npm.
- Docker Desktop ou Docker Engine com Docker Compose.

## Portas locais

| Serviço | Endereço |
| --- | --- |
| Frontend | `http://localhost:3000` |
| API | `http://localhost:5134` |
| PostgreSQL | `localhost:5432` |

## Preparação do ambiente

Inicie o PostgreSQL na raiz do repositório:

```powershell
docker compose up -d postgres
docker compose ps
```

O Compose existente cria o banco local `dev-kiarafin` e mantém os dados no
volume nomeado `kiarafin-postgres-data`. Evite `docker compose down -v`, pois a
opção `-v` remove esse volume e seus dados.

Restaure as dependências:

```powershell
dotnet restore backend/KiaraFin.slnx
npm ci --prefix frontend
```

### Configuração local e segredos

Arquivos `appsettings.Development.json` e `.env*` são ignorados pelo Git. Nunca
adicione tokens, senhas ou chaves ao repositório.

A API usa configuração fornecida por User Secrets, `appsettings.Development.json`
ignorado ou variáveis de ambiente. Por exemplo, em uma sessão local do PowerShell:

```powershell
$env:ConnectionStrings__DefaultConnection = "Host=localhost;Port=5432;Database=dev-kiarafin;Username=<usuario-local>;Password=<senha-local>"
$env:Jwt__Key = "<chave-local-forte-com-pelo-menos-32-bytes>"
```

O frontend recebe apenas configuração pública, como a URL da API; nenhum
segredo pode usar o prefixo `NEXT_PUBLIC_`.

## Executar em desenvolvimento

### Visual Studio

Abra `backend/KiaraFin.slnx` e execute o perfil `Iniciar KiaraFin`. A API inicia
em `http://localhost:5134`, e o SpaProxy executa `npm run dev` no frontend em
`http://localhost:3000`.

Mantenha somente a API como projeto de inicialização quando usar o SpaProxy. Se
o projeto JavaScript também for iniciado separadamente, os dois processos podem
tentar ocupar a porta 3000.

### Terminais separados

```powershell
dotnet run --project backend/src/KiaraFin.Api/KiaraFin.Api.csproj --launch-profile "Iniciar KiaraFin"
```

```powershell
cd frontend
npm run dev
```

## Banco e migrations

O schema não é alterado automaticamente no startup. As migrations do EF Core
são aplicadas explicitamente:

```powershell
dotnet tool install --global dotnet-ef --version "10.*"
dotnet ef migrations add NomeDaMigration --project backend/src/KiaraFin.Api/KiaraFin.Api.csproj --startup-project backend/src/KiaraFin.Api/KiaraFin.Api.csproj --output-dir Data/Migrations
dotnet ef database update --project backend/src/KiaraFin.Api/KiaraFin.Api.csproj --startup-project backend/src/KiaraFin.Api/KiaraFin.Api.csproj
```

Para voltar a uma migration anterior:

```powershell
dotnet ef database update <NomeDaMigrationAnterior> --project backend/src/KiaraFin.Api/KiaraFin.Api.csproj --startup-project backend/src/KiaraFin.Api/KiaraFin.Api.csproj
dotnet ef migrations remove --project backend/src/KiaraFin.Api/KiaraFin.Api.csproj --startup-project backend/src/KiaraFin.Api/KiaraFin.Api.csproj
```

Faça backup antes de reverter um banco que contenha dados. `migrations remove`
só deve remover a última migration ainda não compartilhada/aplicada em outros
ambientes.

## Validação local

```powershell
dotnet build backend/KiaraFin.slnx
dotnet test backend/KiaraFin.slnx
npm run lint --prefix frontend
npm run build --prefix frontend
```

Os testes xUnit cobrem autenticação e isolamento, DDL PostgreSQL, saldo,
transferências, recorrências, ciclos de fatura, parcelas, pagamento de fatura e
projeção financeira.

O teste de integração PostgreSQL é ativado ao definir
`KIARAFIN_TEST_CONNECTION`. Por segurança, ele recusa bancos cujo nome não
contenha `test`:

```powershell
$env:KIARAFIN_TEST_CONNECTION = "Host=localhost;Port=5432;Database=test-kiarafin;Username=<usuario>;Password=<senha>"
dotnet test backend/KiaraFin.slnx
```

## Produção

O Compose de produção publica o frontend em `3333`, mantém a API em `3334` e
executa as migrations explicitamente antes de iniciar a API:

```bash
docker compose --env-file .env.production \
  -f docker-compose.production.yml up -d --build
```

O arquivo `.env.production` não deve ser versionado. Use
`.env.production.example` apenas como modelo. O pipeline em
`.github/workflows/ci-cd.yml` recebe os segredos pelo environment `production`
do GitHub e faz deploy por um runner self-hosted no Ubuntu.

## Endpoints principais

Em desenvolvimento, a especificação OpenAPI exposta pela API é a fonte de verdade.

- `/api/auth`: cadastro, login, refresh, logout e usuário autenticado.
- `/api/households`: núcleo familiar, membros e convites.
- `/api/households/{householdId}/accounts`: contas, saldos e histórico.
- `/api/households/{householdId}/categories`: categorias padrão e personalizadas.
- `/api/households/{householdId}/transactions`: receitas, despesas, cancelamentos e transferências.
- `/api/households/{householdId}/recurring-transactions`: recorrências e geração mensal.
- `/api/households/{householdId}/installments`: planos e parcelas futuras.
- `/api/households/{householdId}/credit-cards`: cartões, compras, faturas e pagamento integral.
- `/api/households/{householdId}/dashboard`: indicadores, gráficos e vencimentos.
- `/api/households/{householdId}/reports`: agregações e filtros financeiros.

Todos os endpoints financeiros deverão validar o `Household` do usuário
autenticado; um identificador enviado pelo cliente nunca será suficiente para
autorizar acesso.

## Roadmap

- [x] Fundação: arquitetura modular, EF Core, PostgreSQL e migration inicial.
- [x] Autenticação: JWT, refresh token rotativo, sessão e núcleo familiar.
- [x] Financeiro: contas, categorias, transações e transferências.
- [x] Planejamento: recorrências, parcelas, cartões e faturas com pagamento integral.
- [x] Experiência: dashboard, relatórios, responsividade e modos claro/escuro.
- [x] Qualidade: testes críticos, revisão de segurança e documentação OpenAPI.
- [ ] Evolução: pagamento parcial de fatura, notificações e importação bancária.

Marque itens apenas quando API, interface, persistência e validações associadas
estiverem funcionais, sem dados mockados permanentes.
