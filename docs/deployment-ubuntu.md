# Deploy no Ubuntu com Docker e GitHub Actions

## Topologia

```text
Internet/VPN
    |
    v
Nginx Proxy Manager (HTTPS)
    |
    v
Ubuntu:3333 -> frontend:3000 -> /api -> api:8080
                                      |
Ubuntu:3334 -> api:8080               v
                             postgres:5432 (rede Docker)
```

O Nginx Proxy Manager precisa publicar apenas o frontend. O frontend encaminha
`/api` para a API pela rede interna do Compose. A porta `3334` fica vinculada a
`127.0.0.1` por padrão para diagnóstico local e não precisa ser pública. O
PostgreSQL não publica porta no host; somente os containers da rede interna
conseguem acessá-lo em `postgres:5432`.

O refresh token usa cookie `Secure` em produção. Portanto, acesse a aplicação
pelo domínio HTTPS configurado no Nginx Proxy Manager, não diretamente por HTTP
na porta `3333` para testar autenticação.

## 1. Preparar o Ubuntu

Confira o Docker e o plugin Compose:

```bash
docker --version
docker compose version
```

Crie um diretório de administração, caso deseje fazer a primeira subida manual:

```bash
sudo mkdir -p /opt/kiarafin
sudo chown "$USER":"$USER" /opt/kiarafin
git clone <URL-DO-REPOSITORIO> /opt/kiarafin
cd /opt/kiarafin
```

Não é obrigatório manter esse clone quando o deploy for feito exclusivamente
pelo runner self-hosted. O runner usa o próprio workspace.

## 2. Configurar o runner self-hosted

Um runner no Ubuntu é a opção recomendada quando a máquina está acessível
somente pela VPN. Ele inicia uma conexão HTTPS de saída com o GitHub; o GitHub
não precisa abrir uma conexão de entrada para o servidor.

No repositório do GitHub, abra:

```text
Settings > Actions > Runners > New self-hosted runner
```

Selecione Linux/x64 e execute no Ubuntu os comandos exibidos pelo próprio
GitHub. Durante `config.sh`, adicione a label:

```text
kiarafin-production
```

Instale o runner como serviço usando os comandos apresentados ao final da
configuração (`svc.sh install` e `svc.sh start`). O usuário do runner deve poder
usar Docker sem `sudo`:

```bash
sudo usermod -aG docker <USUARIO-DO-RUNNER>
```

Reinicie a sessão ou o serviço do runner depois de alterar o grupo. Valide com:

```bash
sudo -u <USUARIO-DO-RUNNER> docker ps
```

Use esse runner apenas para este repositório confiável. Pull requests executam
somente o job de CI hospedado pelo GitHub; o job no servidor roda apenas para
`main` ou por disparo manual.

## 3. Repository secrets do GitHub

Abra:

```text
Settings > Secrets and variables > Actions > New repository secret
```

Adicione estes repository secrets:

| Secret | Conteúdo |
| --- | --- |
| `POSTGRES_PASSWORD` | Senha forte do usuário PostgreSQL |
| `JWT_KEY` | Chave aleatória com pelo menos 32 bytes |
| `PUBLIC_ORIGIN` | URL pública exata, por exemplo `https://financas.exemplo.com.br` |

Gere valores fortes no Ubuntu:

```bash
openssl rand -hex 32
openssl rand -base64 48 | tr -d '\n'
```

Use um valor para a senha e outro para a chave JWT. Não salve esses valores no
repositório.

## 4. Primeira inicialização manual do PostgreSQL

Para uma subida manual, copie o modelo e preencha os valores:

```bash
cd /opt/kiarafin
cp .env.production.example .env.production
nano .env.production
chmod 600 .env.production
```

Inicie somente o PostgreSQL:

```bash
docker compose --env-file .env.production \
  -f docker-compose.production.yml up -d postgres

docker compose --env-file .env.production \
  -f docker-compose.production.yml ps
```

Na primeira inicialização de um volume vazio, a imagem oficial cria o usuário e
o banco definidos por `POSTGRES_USER` e `POSTGRES_DB`. O schema da aplicação é
criado separadamente pela migration do EF Core:

```bash
docker compose --env-file .env.production \
  -f docker-compose.production.yml run --rm migrate
```

Depois suba a aplicação:

```bash
docker compose --env-file .env.production \
  -f docker-compose.production.yml up -d --build
```

O serviço `migrate` também é executado como dependência antes da API. Repetir a
operação é seguro: o EF aplica somente migrations ainda pendentes.

Valide:

```bash
curl --fail http://127.0.0.1:3334/health
curl --fail http://127.0.0.1:3333/ >/dev/null

docker compose --env-file .env.production \
  -f docker-compose.production.yml ps
```

O banco persiste no volume `kiarafin-postgres-production-data`. Estes comandos
preservam os dados:

```bash
docker compose --env-file .env.production -f docker-compose.production.yml restart
docker compose --env-file .env.production -f docker-compose.production.yml down
```

Não execute `down -v` em produção: a opção `-v` remove o volume do PostgreSQL.

### Conferir banco e migrations

```bash
docker compose --env-file .env.production -f docker-compose.production.yml \
  exec postgres psql -U kiarafin -d kiarafin -c '\dt'

docker compose --env-file .env.production -f docker-compose.production.yml \
  exec postgres psql -U kiarafin -d kiarafin \
  -c 'select * from "__EFMigrationsHistory";'
```

### Backup básico

```bash
mkdir -p backups
docker compose --env-file .env.production -f docker-compose.production.yml \
  exec -T postgres pg_dump -U kiarafin -d kiarafin -Fc \
  > "backups/kiarafin-$(date +%Y%m%d-%H%M%S).dump"
```

Faça e valide backups antes de atualizar ou reverter migrations.

## 5. Nginx Proxy Manager

Crie um **Proxy Host**:

- Domain Names: domínio usado em `PUBLIC_ORIGIN`.
- Scheme: `http`.
- Forward Hostname/IP: IP do host Ubuntu alcançável pelo container do NPM.
- Forward Port: `3333`.
- Websockets Support: habilitado.
- Block Common Exploits: habilitado.

Na aba SSL, solicite ou selecione o certificado, habilite **Force SSL** e
**HTTP/2 Support**. Não é necessário criar uma regra separada para `/api`.

Se o NPM estiver na mesma máquina, use o IP LAN/VPN do Ubuntu. Garanta que o
firewall permita `3333/tcp` somente a partir da rede Docker/NPM ou da VPN. O
PostgreSQL não possui porta publicada pelo Compose de produção.

## 6. Funcionamento do pipeline

O workflow `.github/workflows/ci-cd.yml` realiza:

1. build e testes .NET com PostgreSQL real;
2. lint e build do Next.js;
3. em `main`, build das imagens no runner self-hosted;
4. inicialização/healthcheck do PostgreSQL;
5. aplicação explícita das migrations;
6. subida da API e do frontend;
7. validação HTTP das portas `3334` e `3333`.

O deploy também pode ser iniciado em **Actions > KiaraFin CI/CD > Run
workflow**. Commits de pull requests nunca executam comandos Docker no servidor
de produção.

## Operação e diagnóstico

```bash
docker compose -f docker-compose.production.yml ps
docker compose -f docker-compose.production.yml logs --tail=200 api
docker compose -f docker-compose.production.yml logs --tail=200 frontend
docker compose -f docker-compose.production.yml logs --tail=200 postgres
```

Para comandos manuais, use `--env-file .env.production`. No GitHub Actions, as
variáveis são fornecidas diretamente pelos repository secrets.
