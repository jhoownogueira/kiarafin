# KiaraFin

KiaraFin é um projeto pessoal e open source de gestão financeira individual e
familiar. Ele reúne contas, lançamentos, cartões, previsões e relatórios em um
núcleo financeiro compartilhável (`Household`). A integração opcional com a
Pluggy importa dados via Open Finance, recebe atualizações por webhook e pode
enviar resumos privados pelo Telegram.

O código pode ser estudado, clonado e implantado em infraestrutura própria nos
termos da [licença MIT](LICENSE). Cada instalação deve usar suas próprias
credenciais, consentimentos e contas nos serviços externos.

Instância pessoal: [kiarafin.jhonata.dev](https://kiarafin.jhonata.dev/). O
cadastro público permanece fechado nessa implantação.

> Status: MVP funcional. Autenticação, núcleo familiar, contas, categorias,
> lançamentos, previsões, parcelas, cartões, dashboard e relatórios usam a API
> e o PostgreSQL reais. Consulte o roadmap para limitações e evoluções planejadas.

O cadastro de novos usuários fica desativado por padrão. Em uma instalação nova,
o operador pode habilitá-lo temporariamente com `REGISTRATION_ENABLED=true`,
criar a primeira conta e voltar a desativá-lo. A integração Open Finance possui
uma verificação adicional e só pode ser operada pelo e-mail configurado em
`PLUGGY_AUTHORIZED_EMAIL`.

> O KiaraFin não presta aconselhamento financeiro e não é um produto oficial da
> Pluggy ou do Telegram. Nunca use credenciais ou dados reais em ambientes de
> teste e mantenha backups do banco de produção.

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

A configuração e a operação da integração Open Finance estão descritas em
[Open Finance com Meu Pluggy](docs/open-finance-pluggy.md).

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

A integração Pluggy nasce desabilitada. Para desenvolvimento local, configure
as opções `Pluggy__*` somente no backend por User Secrets ou variáveis de
ambiente; nunca envie Client Secret ou segredo do webhook ao Next.js.

Para criar a primeira conta de uma instalação vazia, habilite temporariamente
`Registration__Enabled=true` no backend e envie um `POST /api/auth/register`.
Desabilite a opção imediatamente após o bootstrap; mantê-la habilitada em uma
instância pública permite cadastro por qualquer visitante.

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
`.github/workflows/ci-cd.yml` recebe os segredos pelos Repository secrets do
GitHub e faz deploy por um runner self-hosted no Ubuntu.

Todas as credenciais de PostgreSQL, JWT, Pluggy e Telegram pertencem à instalação
e devem ser fornecidas por variáveis de ambiente ou GitHub Actions Secrets. O
repositório não contém banco, dados financeiros nem credenciais de produção.

## Endpoints principais

Em desenvolvimento, a especificação OpenAPI exposta pela API é a fonte de verdade.

- `/api/auth`: cadastro configurável, login, refresh, logout e usuário autenticado.
- `/api/households`: núcleo familiar, membros e convites.
- `/api/households/{householdId}/accounts`: contas, saldos e histórico.
- `/api/households/{householdId}/categories`: categorias padrão e personalizadas.
- `/api/households/{householdId}/transactions`: receitas, despesas, cancelamentos e transferências.
- `/api/households/{householdId}/forecasts`: salários, rendas extras e contas fixas previstas.
- `/api/households/{householdId}/installments`: planos e parcelas futuras.
- `/api/households/{householdId}/credit-cards`: cartões, compras, faturas e pagamento integral.
- `/api/households/{householdId}/dashboard`: indicadores, gráficos e vencimentos.
- `/api/households/{householdId}/reports`: agregações e filtros financeiros.

Todos os endpoints financeiros deverão validar o `Household` do usuário
autenticado; um identificador enviado pelo cliente nunca será suficiente para
autorizar acesso.

## Contribuição e segurança

Contribuições são bem-vindas por pull request. A branch `main` é protegida e
alterações de terceiros exigem revisão do mantenedor definido em
`.github/CODEOWNERS`. Não abra uma issue pública contendo vulnerabilidades,
tokens ou dados financeiros; siga as orientações de [SECURITY.md](SECURITY.md).
