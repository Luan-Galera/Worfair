# Worfair — DevOps (Infraestrutura, Containerização e Automação)

Documento de referência oficial da camada de operações, fechando o ciclo com
`docs/architecture/`, `docs/database/`, `docs/security/`, `docs/financial/` e
`docs/frontend/`. Artefatos de apoio na raiz do repositório.

## 1. Índice

| Doc | Tema |
| --- | ---- |
| [01 — Monolito Modular → Microsserviços](01-estrategia-microservices.md) | Estratégia de extração para larga escala, gatilhos e plano em fases |
| [02 — Database Migrations](02-database-migrations.md) | Migrações versionadas, review, aprovação e deploy controlado (nunca automático em produção) |
| [03 — CI/CD (GitHub Actions)](03-cicd-github-actions.md) | Pipeline completo: build, testes, qualidade, imagens, secrets, migrations, staging/prod, rollback |
| [04 — Saúde, Logs e Observabilidade](04-observabilidade-saude-logs.md) | Health checks, logs estruturados, OpenTelemetry, métricas e alertas |
| [05 — Validação do Ciclo Completo](05-validacao-ciclo-completo.md) | Fechamento: como todas as camadas se integram e checklist de validação |

## 2. Artefatos na raiz do repositório

| Arquivo | Papel |
| ------- | ----- |
| `docker-compose.yml` | Ambiente de dev local: **PostgreSQL 16**, **RabbitMQ 4 (painel 15672)**, **API ASP.NET Core**, frontend Vite, `migrate` (one-shot) e MailHog (profile `tools`) |
| `.env.example` | Variáveis de ambiente de dev (nunca commitar `.env` real) |
| `src/Api/Worfair.Api/Dockerfile` | Imagem multi-stage da API (SDK → publish → aspnet runtime) |
| `.dockerignore` | Contexto de build limpo (sem bin/obj/docs/tests) |
| `dev/postgres/init/00-init.sql` | Schemas + roles `worfair_app`/`worfair_migrator` (R-02/R-11) |

**Comandos de uso:**

```bash
cp .env.example .env
docker compose up -d                        # postgres + rabbitmq + api + frontend
docker compose run --rm migrate             # executa migrations (one-shot, perfil tools)
docker compose up mailhog                   # e-mails de dev (painel :8025)
```

## 3. Atualizações de continuidade (v1.5)

| # | Mudança | Base anterior | Motivo |
| - | ------- | ------------- | ------ |
| DEV-01 | Stack de infra: Docker Compose (dev) + GitHub Actions (CI/CD) + container registry (GHCR) | não havia | Definição do ciclo de operações |
| DEV-02 | Migrações **nunca automáticas em produção** — versão + review + aprovação (env `DB_AUTO_MIGRATE` restrito ao dev) | implicações da execução via EF | Requisito explícito de segurança de deploy |
| DEV-03 | Frontend: **interceptor do Axios corrigido** (refresh single-flight retry correto; `auth:expired`; guarda no próprio refresh — FE-05) | interceptor v1.4 | Versão congelada do planejamento |
| DEV-04 | `docker-compose.yml` espelha o modelo de banco: role `worfair_app` (RLS) e `worfair_migrator` (BYPASSRLS) | docs/database/02 | Paridade dev × prod |

## 4. Ambientes

| Ambiente | Como sobe | Migrations | Secrets |
| -------- | --------- | ----------- | ------- |
| **Dev local** | `docker compose up` | automáticas (dev) via `DB_AUTO_MIGRATE=true` ou `compose run --rm migrate` | `.env` local + chaves RSA em `dev/jwt/` |
| **Staging** | CI/CD automático após merge em `main` | passo `migrate` do pipeline (guardado) | GitHub Environments secrets |
| **Produção** | CI/CD com **aprovação manual** (`environment: production`) | passo `migrate-prod` explícito + backup prévio | GitHub Environments + OIDC (sem chaves longas) |