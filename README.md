# Worfair — Backend (Monolito Modular)

Backend da plataforma Worfair (ATS + Contratação + Marketplace de serviços),
implementando o planejamento de `docs/architecture`, `docs/database`,
`docs/security` e `docs/devops`.

> **Status:** Partes 1 e 2 concluídas e compilando — BuildingBlocks + módulos
> **Identity**, **Tenants** e **Recruitment** funcionais (end-to-end), migrations
> com **RLS FORCE** validadas no PostgreSQL, 83 testes de domínio e arquitetura.
> Próximas partes: Jobs, Proposals, Financial (Asaas) e Notifications (ver Roadmap).

---

## Stack

| Camada | Tecnologia |
| ------ | ---------- |
| Runtime | .NET **10** / C# (nullable, analyzers) |
| Persistência | EF Core 10 + **Npgsql** (PostgreSQL 16) |
| CQRS | MediatR 12 (+ behaviors de validação/logging) |
| Validação | FluentValidation |
| Autenticação | JWT **RS256** (access 15min + refresh rotativo 7d) |
| Hash de senha | BCrypt (work factor 12) |
| Docs interativas | OpenAPI nativo + **Scalar** (`/scalar`) |
| Testes | xUnit + FluentAssertions + **NetArchTest** |

## Estrutura

```
src/
├── BuildingBlocks/
│   ├── Worfair.BuildingBlocks.Domain          # Entity, AggregateRoot, ValueObject,
│   │                                          # Result (D-10), TenantId, ITenantEntity…
│   ├── Worfair.BuildingBlocks.Application     # CQRS, behaviors, IEventBus, Security (SEC-03)
│   ├── Worfair.BuildingBlocks.Infrastructure  # EF tenancy interceptors, Outbox, InProcessBus
│   └── Worfair.BuildingBlocks.Contracts      # base dos IntegrationEvents
├── Modules/
│   ├── Tenants/     # tenants, companies, memberships, settings  (schema: tenancy)
│   ├── Identity/    # users, roles, permissions, user_roles, refresh_tokens (schema: identity)
│   └── Recruitment/ # job_requisitions, candidates, interviews   (schema: recruitment)
└── Api/Worfair.Api # host: middlewares SEC-02, JWT, policies, endpoints, Scalar, health
tests/
├── Unit/…Identity… / …Tenants… / …Recruitment…   # regras de negócio (83 testes)
└── Worfair.Tests.Architecture        # fronteiras de módulo (NetArchTest)
```

## Pré-requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0)
- Docker Desktop (para PostgreSQL/RabbitMQ locais) **ou** um PostgreSQL 16 acessível

## Setup rápido (dev)

```bash
# 1) Chaves RSA de desenvolvimento (RS256) — NÃO versionadas
powershell -File dev/jwt/generate.ps1        # gera dev/jwt/private.pem e public.pem

# 2) Infra local (Postgres + RabbitMQ + roles worfair_app/worfair_migrator)
cp .env.example .env
docker compose up -d postgres rabbitmq

# 3) Migrações (tenancy → identity → recruitment, com RLS/CHECKs/seed idempotente)
dotnet tool restore
dotnet dotnet-ef database update `
  --project src/Modules/Tenants/Worfair.Modules.Tenants.Infrastructure `
  --startup-project src/Api/Worfair.Api --context TenancyDbContext
dotnet dotnet-ef database update `
  --project src/Modules/Identity/Worfair.Modules.Identity.Infrastructure `
  --startup-project src/Api/Worfair.Api --context IdentityDbContext
dotnet dotnet-ef database update `
  --project src/Modules/Recruitment/Worfair.Modules.Recruitment.Infrastructure `
  --startup-project src/Api/Worfair.Api --context RecruitmentDbContext

#    (alternativa full-container: docker compose run --rm migrate)

# 4) API + Scalar
dotnet run --project src/Api/Worfair.Api
# Swagger/OpenAPI : http://localhost:5000/openapi/v1.json
# Scalar (UI)     : http://localhost:5000/scalar
# Health          : http://localhost:5000/health  ·  /health/ready
```

> O appsettings.Development aponta as chaves para `../../dev/jwt/*.pem`.
> Em produção as migrações **nunca** são automáticas (DEV-02) — passo controlado
> do pipeline com `worfair_migrator`; runtime usa `worfair_app` (sujeita ao RLS).

### Variáveis de ambiente suportadas

| Var | Efeito |
| --- | ------ |
| `DB_AUTO_MIGRATE=true` | aplica migrações no startup (**somente dev**) |
| `DB_MIGRATE_ONLY=true` | aplica migrações e encerra (container one-shot) |

## Testando a API (fluxo ponta a ponta)

Use o Scalar em `/scalar` (ou curl):

1. **`POST /api/identity/register`** — primeiro usuário recebe `SUPER_ADMIN`
   global automaticamente (bootstrap da plataforma).
2. **`POST /api/identity/login`** — devolve access token + refresh token.
   Envie `Authorization: Bearer <token>` nas chamadas seguintes.
3. **`POST /api/platform/tenants`** *(policy `platform.tenants.manage`, contexto global)*
   — provisiona tenant + settings + owner opcional; publica
   `TenantProvisionedIntegrationEvent` via **Outbox**.
4. **`POST /api/identity/users/{id}/roles`** — concede `OWNER`/`RECRUITER` etc.
   no tenant corrente (exige membership ativa — R-05).
5. **`POST /api/identity/switch-tenant`** / **`switch-mode`** — única via de troca
   de contexto; sempre emite **novo token** após validar membership/permissões no banco.
6. **`GET /api/identity/me`** — espelho do contexto calculado **no servidor**
   (roles, permissões efetivas, modos disponíveis).
7. **Recruitment (ATS)** — fluxo ponta a ponta no tenant corrente:
   - `POST /api/recruitment/requisitions` → cria `JobRequisition` em `Draft`;
   - `POST …/{id}/team` → adiciona membro do time (**exigido para publicar**);
   - `POST …/{id}/publish` → `Draft → Published`; emite
     `worfair.recruitment.job-requisition-published.v1` via **Outbox**;
   - `POST …/{id}/pause|resume|close|cancel`, `PATCH …/{id}/salary-range`;
   - `POST /api/recruitment/candidates` → nasce `Sourced`/`Applied`
     (e-mail único **por tenant**, case-insensitive);
   - `POST /api/recruitment/candidates/{id}/advance` → máquina estrita
     `Sourced → Applied → Screened → Interviewing → Offered → Hired`;
     avançar p/ `Interviewing` exige entrevista agendada; histórico append-only;
   - `POST /api/recruitment/interviews` → agenda (duração 15–480 min, futuro);
     `POST …/{id}/feedback` (1 por entrevistador, nota 1–5) e `…/complete`
     (**feedback obrigatório**);
   - `POST /api/recruitment/candidates/{id}/hire` → publica
     `worfair.recruitment.candidate-hired.v1` via Outbox (→ Proposals/Notifications).

Verificações de segurança incluídas:

- Header **`X-Tenant-Id` ⇒ 400** (middleware SEC-02).
- Recurso/outro tenant ou inexistente ⇒ **404 indistinguível**.
- Sem contexto de tenant em operação tenant-scoped ⇒ **403** (deny-by-default;
  RLS recusa qualquer linha com `app.tenant_id` nulo).

## Testes automatizados

```bash
dotnet test                     # 83 testes (domínio + arquitetura)
```

Cobrem, entre outros: normalização de e-mail/slug/documento/título, máquinas de
estado (Tenant/Company/Membership/User/RefreshToken/JobRequisition/Candidate/
Interview), invariante de escopo de `user_roles` (R-04), múltiplas roles por
tenant (R-05), detecção de reuso de refresh token, invariantes do ATS (publicar
só com time de contratação, faixa salarial imutável pós-encerramento, avanço
sequencial do candidato, entrevista obrigatória p/ `Interviewing`, feedback
obrigatório p/ concluir) e as regras de dependência entre projetos (NetArchTest).

## Regras de negócio já implementadas (rastreabilidade)

| Fonte | Regra | Onde |
| ----- | ----- | ---- |
| R-01..R-09 | `tenant_id`, RLS `FORCE`, globais fechadas, escopo misto | migrations + interceptors |
| R-04 | `SUPER_ADMIN` global (`tenant_id` NULL) | `UserRole.Grant` + CHECK no banco |
| R-05 | multi-role por tenant exige membership ativa | handler AssignTenantRole + FK composta |
| R-06 | handlers nunca recebem TenantId; cross-tenant = 404/null | commands + query filters |
| R-11 | seed idempotente (`ON CONFLICT DO NOTHING`) | migration identity |
| SEC-01 | JWT = contexto; autorização relê o banco a cada request | `AuthorizationDataProvider` + handlers |
| SEC-02 | `X-Tenant-Id` ⇒ 400; switch é a única troca | middlewares + `SwitchTenantCommand` |
| SEC-03 | modos derivados de permissões efetivas | `AccessModeMapper` + `ModeResolver` |
| D-07/D-10 | Outbox transacional; Result pattern | `OutboxEventBus`, `UnitOfWork`, `Result` |
| ATS: publish exige time de contratação | `JobRequisition.Publish` + CHECKs | domínio + migration recruitment |
| ATS: faixa salarial imutável pós-encerramento | `JobRequisition.ChangeSalaryRange` | domínio + `chk_job_requisitions_salary_range` |
| ATS: recrutador único no time / travado fora de Draft | `JobRequisition.AddTeamMember` | domínio + PK `(job_requisition_id, recruiter_user_id)` |
| Candidato: e-mail único por tenant (case-insensitive) | `ContactEmail` + filtro por tenant | `uq_candidates_tenant_email` |
| Candidato: máquina estrita + Interviewing exige entrevista | `Candidate.Advance` + handler | domínio + `chk_candidates_status` |
| Entrevista: feedback obrigatório, 1/entrevistador, nota 1–5 | `Interview.Complete/AddFeedback` | domínio + CHECKs/UNIQUE no banco |

## Decisões técnicas documentadas (desvios mínimos)

0. **Mapeamento de `SalaryRange` (VO aninhado):** `Money` vive dentro de
   `SalaryRange`; para materializar VOs aninhados o EF exige construtores sem
   parâmetros + setters privados (API pública permanece imutável). Persistido em
   `salary_min/salary_currency` e `salary_max/salary_max_currency`
   (docs/architecture/05 §6).

1. **PK de `user_roles`:** PostgreSQL não admite NULL em PK composta; adotada PK
   técnica `id uuid` + UNIQUE lógica `(user_id, tenant_id, role_id)` e índice
   parcial `(user_id, role_id) WHERE tenant_id IS NULL`. Todas as garantias
   R-04/R-05 permanecem (CHECK + FKs + domínio).
2. **`refresh_tokens.tenant_id` NULLável:** sessão global do SUPER_ADMIN precisa
   de refresh; política RLS de escopo misto (mesma de `user_roles`).
3. **Leitura elevada (authz/switch):** `TenancyReadContract` define
   `app.tenant_id` **no escopo da transação** (`set_config(..., true)`), reverte
   ao final e só lê — espelha a consequência prática de R-04 ("define
   app.tenant_id manualmente na sessão, auditável"), sem bypass de RLS.
4. **Pool de conexões:** o interceptor redefine `app.tenant_id` para NULL a cada
   `ConnectionOpened` sem tenant — evita vazamento de contexto entre requests.

## Roadmap (próximas partes, seguindo os docs)

| Parte | Escopo | Status |
| ----- | ------ | ------ |
| 1 | Fundação — BuildingBlocks + Identity + Tenants | ✅ concluída |
| 2 | **Recruitment** — JobRequisition, Candidate, Interview; publica `JobRequisitionPublished` e `CandidateHired` (consumo de `JobPostingPublished` será ligado na Parte 3) | ✅ concluída |
| 3 | **Jobs** — JobPosting/ServiceProject/Skill/Category; publica posting events → ativa o consumer do Recruitment | próximo |
| 4 | Proposals/Hiring — Proposal/Offer/Contract; consome `CandidateHired` | — |
| 5 | Financial — máquina de estados FIN-02, Asaas Split, webhook inbox idempotente, RabbitMQ/MassTransit | — |
| 6 | Notifications — templates/preferências; consome eventos dos demais módulos | — |
| 7 | Observabilidade (OTel), audit_logs + hash chain (SEC-04), CI/CD GitHub Actions | — |

### Limitações conhecidas da Parte 2

- **Horário comercial da entrevista:** a invariante "dentro do horário
  comercial do tenant" depende de parsing do `hiring_workflow` (jsonb em
  `tenant_settings`) — ficará completa junto com o read contract de settings.
- **Entrevistador ∈ HiringTeam:** validado no handler (consulta cross-aggregate);
  o domínio garante unicidade de feedback e conclusão com feedback.
