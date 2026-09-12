# Worfair — Marketplace de Trabalhos e Vagas

Backend e frontend para plataforma multi-tenant de contratação e prestação de serviços.
Funcionalidades completas de cadastro, publicação, propostas, faturas e pagamentos com taxa 15%.

---

## Funcionamento

### Principais fluxos

- **Registro e login**: Primeiro usuário vira SUPER_ADMIN global. Login via JWT RS256 (token 15min + refresh 7d).
- **Contexto de tenant**: Cada operação está vinculada a um tenant (espaço/workspace). Isolamento via RLS (Row Level Security) no PostgreSQL.
- **Modo automático**: Constratante ou prestador é derivado automaticamente das permissões do usuário no tenant corrente. Não há toggle manual.
- **Taxa 15%**: Sobre todo trabalho/cobrança. Contratante paga valor + 15%; prestador recebe valor integral.
- **Publicação**: Contratantes publicam vagas (empregos) ou trabalhos (freela). Prestadores enviam propostas.
- **Faturas e pagamentos**: Geração de invoices com taxa 15%, visualização de saldo (a receber/a pagar/recebido/pago), e marcação de pagamento.
- **Mensagens e disputas**: Conversas vinculadas a faturas. Em caso de conflito, sistema encaminha ao SUPER_ADMIN para mediação.
- **Alternância de tenant**: Via `POST /api/identity/switch-tenant`. Renova token e contexto automaticamente.

---

## Como rodar (desenvolvimento)

> **Ordem importante**: Siga os passos na sequência. Pular etapas causa erros.

### Passo 1: Instalar prerequisites

- [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)

### Passo 2: Gerar chaves JWT

```powershell
powershell -ExecutionPolicy Bypass -File dev/jwt/generate.ps1
```

Isso cria `dev/jwt/private.pem` e `dev/jwt/public.pem`.

**Copie os arquivos para:**
- `dev/jwt/` (pasta raiz)
- `src/Api/Worfair.Api/dev/jwt/` (ambas as pastas)

### Passo 3: Subir o banco de dados

```powershell
docker compose up -d postgres rabbitmq
```

Aguardar containers ficarem "Healthy".

### Passo 4: Aplicar migrations (criar tabelas)

**Opção A - Automática (desenvolvimento)**:
```powershell
$env:DB_AUTO_MIGRATE='true'
```
Isso cria todas as tabelas automaticamente ao iniciar a API e aplica o seed do SUPER_ADMIN se as chaves `AdminSeed__Email`/`AdminSeed__Password` estiverem configuradas no `.env`.

**Opção B - Manual**:
```powershell
dotnet ef database update --project src/Modules/Tenants/Worfair.Modules.Tenants.Infrastructure --startup-project src/Api/Worfair.Api --context TenancyDbContext
dotnet ef database update --project src/Modules/Identity/Worfair.Modules.Identity.Infrastructure --startup-project src/Api/Worfair.Api --context IdentityDbContext
dotnet ef database update --project src/Modules/Recruitment/Worfair.Modules.Recruitment.Infrastructure --startup-project src/Api/Worfair.Api --context RecruitmentDbContext
```

### Passo 5: Iniciar o backend (API)

```powershell
dotnet run --project src/Api/Worfair.Api
```

API disponível em: `http://localhost:5000`

- Documentação: `http://localhost:5000/openapi/v1.json`
- Scalar (UI): `http://localhost:5000/scalar`
- Health: `http://localhost:5000/health`

### Passo 6: Iniciar o frontend

```powershell
cdwarfair-web
npm install           # (primeira vez apenas)
npm run dev
```

Frontend em: `http://localhost:5173`

---

## Passos pós-initial (importante ler antes)

⚠️ **SUPER_ADMIN NÃO é criado pelo `/register`**.

- O endpoint `POST /api/identity/register` cria um usuário comum sem papel.
- O SUPER_ADMIN inicial é criado **apenas no startup** se as chaves `AdminSeed__Email` e `AdminSeed__Password` estiverem definidas no arquivo `.env` (exemplo no `.env.example`).
- O banco de dados impõe unicidade: só pode existir **um único** SUPER_ADMIN global (índice parcial `uq_single_super_admin` em `user_roles` com `TenantId IS NULL`).
- Após o seed, faça login com o usuário cadastrado no passo 1 e use `POST /api/identity/switch-tenant` para derivar o modo automaticamente.

1. **Primeiro login**: Rode a API uma vez com `DB_AUTO_MIGRATE=true` e `AdminSeed__*` preenchidos no `.env`. O SUPER_ADMIN será criado automaticamente.
2. **Login**: `POST /api/identity/login` → recebe token (modo `global`, roles `SUPER_ADMIN`).
3. **Criar tenant**: Usar `POST /api/tenants/bootstrap` (SUPER_ADMIN) ou `POST /api/platform/tenants` (SUPER_ADMIN).
4. **Adicionar usuários ao tenant**: `POST /api/identity/users/{id}/roles` com role (Owner, Provider, Client, Recruiter, HiringManager).
5. **Publicar**: Acesse `/contratar` e preencher formulário (requer permissão `financial.invoice.issue` e modo `Contracting`).
6. **Usar marketplace**: Vagas em `/vagas`, Trabalhos em `/trabalhos`, Financeiro em `/financeiro`, Mensagens em `/mensagens`.

---

## Variáveis de ambiente importantes

| Variável | Descrição | Obrigatório? |
|----------|-----------|--------------|
| `DB_AUTO_MIGRATE=true` | Aplica migrations no startup (somente dev) | Não (somente dev) |
| `AdminSeed__Email` | Email do SUPER_ADMIN inicial (cria 1 único no startup) | Não, mas recomendado para testar o super admin |
| `AdminSeed__Password` | Senha ≥12 chars do SUPER_ADMIN (cria 1 único no startup) | Não, mas recomendado para testar o super admin |
| `ASAAS_API_KEY` | Chave Asaac Sandbox (opcional, pagamentos ainda não totalmente integrados) | Não |
| `ASAAS_WEBHOOK_TOKEN` | Token webhook Asaac (cadastre no painel Asaac → cobrança → webhook) | Não |
| `ASAAS_SPLIT_WALLET_ID` | Wallet da plataforma para split automático da taxa 15% (opcional) | Não |

---

## Comportamento de SUPER_ADMIN e usuários

- **SUPER_ADMIN**: Contexto global (`tenant_id` NULL), role `SUPER_ADMIN` no banco. Pode ver todas as tenants/faturas/disputas via leitura elevada. Não pode ser criado pelo `/register` — só pelo seed inicial.
- **Usuário comum**: Contexto de tenant (via claim `tenant_id` no JWT). Só vê e age no que pertence ao seu tenant. Não pode banir — as policies e endpoints exigem `tenants.members.manage` + modo `Contracting`, que usuários comuns não têm. Só pode denunciar (`DisputeOpen`), conversar (`MessageRead/MessageSend`) e marcar com estrela.
- **Taxa 15%**: Sobre todo trabalho/cobrança. Fatura gera `PlatformFeeAmount = amount × 0.15` e `TotalAmount = amount × 1.15`. Contratante paga o valor + 15%; prestador recebe o valor líquido (o sistema controla a taxa localmente; split via Asaas wallet é opcional).

- **Perfil de usuário**: O endpoint `GET /api/identity/me` agora retorna os campos adicionais:
  - `AvatarUrl`: URL da imagem de perfil do usuário
  - `PortfolioUrl`: URL do portfólio ou links de projetos do usuário
  - Ambos os campos são opcionais e podem ser atualizados através de futuros endpoints de edição de perfil.

---

## Estrutura de arquivos críticos

- `dev/jwt/generate.ps1` - Gera chaves de segurança
- `dev/jwt/private.pem` / `public.pem` - Chaves JWT (necessárias para login)
- `docker-compose.yml` - Subir PostgreSQL + RabbitMQ
- `src/Api/Worfair.Api/Program.cs` - Configuração da API (middlewares, JWT, endpoints)
- `src/BuildingBlocks/Worfair.BuildingBlocks.Infrastructure/Persistence/Tenant/TenantConnectionInterceptor.cs` - Interceptor RLS (define `app.tenant_id` por conexão)
- `src/Modules/Identity/Worfair.Modules.Identity.Application/Abstractions/ModeResolver.cs` - Lógica do modo automático

---

## Solução de problemas básicos

- **Erro "Chave PEM não encontrada"**: Regenerar com o Passo 2 e copiar para ambas as pastas `dev/jwt/`
- **Erro "Não conecta no PostgreSQL"**: Verificar `docker compose up` e containers saudáveis
- **Erro "Modo indisponível (500)"**: Segundo usuário precisa de role no tenant via `POST /api/identity/users/{id}/roles`
- **Build com erros**: Rodar `dotnet build Worfair.slnx` para ver linhas exatas
- **SUPER_ADMIN não aparece**: Verifique se `AdminSeed__Email` e `AdminSeed__Password` estão no `.env` e reinicie a API com `$env:DB_AUTO_MIGRATE='true'`

---