# 03 — CI/CD (GitHub Actions)

## 1. Visão geral dos workflows

| Workflow | Gatilho | O que faz |
| -------- | ------- | --------- |
| `ci.yml` | PR + push em `main` | restore, build, testes unitários/integração, qualidade, scan de segredos, build das imagens (sem push em PR) |
| `cd-staging.yml` | merge em `main` (ou `workflow_dispatch`) | build+push imagens, migrate-staging, deploy, health+smoke |
| `cd-production.yml` | `workflow_dispatch` (ou tag `v*`) | **aprovação manual** → backup → migrate-prod → deploy → health → smoke → notify |

**Secrets:** GitHub **Environments** (`staging`, `production`) com secrets por
ambiente; **OIDC** para cloud/registry (sem token de longa duração); chaves
JWT RSA/Asaas **nunca** em variáveis de build.

## 2. `ci.yml` (restore · build · testes · qualidade)

```yaml
name: CI

on:
  pull_request:
  push:
    branches: [main]

env:
  DOTNET_VERSION: '10.0.x'
  NODE_VERSION: '22'

jobs:
  backend-build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: ${{ env.DOTNET_VERSION }} }
      - name: Restore
        run: dotnet restore Worfair.sln
      - name: Build (Release, warnings como erro)
        run: dotnet build Worfair.sln -c Release --no-restore -warnaserror
      - name: Testes de arquitetura (fronteiras de módulo — NetArchTest)
        run: dotnet test tests/Unit/Worfair.Architecture.Tests -c Release --no-build
      - uses: actions/upload-artifact@v4
        with: { name: backend-build, path: src/Api/Worfair.Api/bin/Release, retention-days: 7 }

  backend-tests:
    needs: backend-build
    runs-on: ubuntu-latest
    services:
      postgres:
        image: postgres:16-alpine
        env:
          POSTGRES_USER: worfair
          POSTGRES_PASSWORD: worfair_test
          POSTGRES_DB: worfair_test
        ports: ['5432:5432']
        options: >-
          --health-cmd "pg_isready -U worfair -d worfair_test"
          --health-interval 10s --health-timeout 5s --health-retries 5
      rabbitmq:
        image: rabbitmq:4-management-alpine
        env:
          RABBITMQ_DEFAULT_USER: worfair
          RABBITMQ_DEFAULT_PASS: worfair_test
        ports: ['5672:5672']
        options: >-
          --health-cmd "rabbitmq-diagnostics -q ping"
          --health-interval 10s --health-timeout 10s --health-retries 5
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: ${{ env.DOTNET_VERSION }} }
      - name: Restore
        run: dotnet restore Worfair.sln
      - name: Testes de unidade (todos os módulos)
        run: dotnet test tests/Unit -c Release --no-restore
      - name: Testes de integração (Postgres real + RabbitMQ real)
        env:
          ConnectionStrings__Test: Host=localhost;Port=5432;Database=worfair_test;Username=worfair;Password=worfair_test
          RabbitMQ__Test: localhost:5672
        run: dotnet test tests/Integration -c Release --no-restore
          --collect:"XPlat Code Coverage" --results-directory coverage
      - uses: actions/upload-artifact@v4
        with: { name: coverage, path: coverage, retention-days: 14 }

  frontend:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with: { node-version: ${{ env.NODE_VERSION }}, cache: npm, cache-dependency-path: worfair-web/package-lock.json }
      - name: Instalar
        run: npm ci
        working-directory: worfair-web
      - name: Lint
        run: npm run lint
        working-directory: worfair-web
      - name: Testes (vitest)
        run: npm run test -- --coverage
        working-directory: worfair-web
      - name: Build
        run: npm run build
        working-directory: worfair-web

  quality:
    needs: [backend-tests, frontend]
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Scan de segredos (gitleaks)
        uses: gitleaks/gitleaks-action@v2
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
      - name: Análise estática (CodeQL)
        uses: github/codeql-action/init@v3
        with: { languages: csharp, javascript }
      - uses: github/codeql-action/analyze@v3

  docker-build:
    needs: [backend-build, quality]
    if: github.ref == 'refs/heads/main'
    runs-on: ubuntu-latest
    permissions:
      contents: read
      packages: write                    # GHCR via OIDC (sem PAT)
    steps:
      - uses: actions/checkout@v4
      - uses: docker/setup-buildx-action@v3
      - name: Login GHCR (token efêmero)
        uses: docker/login-action@v3
        with:
          registry: ghcr.io
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}
      - name: Build e push da API (tag imutável por sha)
        uses: docker/build-push-action@v6
        with:
          context: .
          file: src/Api/Worfair.Api/Dockerfile
          push: true
          tags: ghcr.io/worfair/api:${{ github.sha }}
      - name: Build e push do frontend (estático)
        uses: docker/build-push-action@v6
        with:
          context: worfair-web
          file: worfair-web/Dockerfile
          push: true
          tags: ghcr.io/worfair/web:${{ github.sha }}
```

## 3. `cd-staging.yml` (deploy automático pós-merge)

```yaml
name: CD Staging

on:
  workflow_run:
    workflows: [CI]
    types: [completed]
    branches: [main]

jobs:
  migrate-staging:        # passo controlado de migrations (doc 02)
    environment: staging
    steps:
      - name: Backup do banco (staging)
        env: { STAGING_DB_MIGRATOR: ${{ secrets.STAGING_DB_MIGRATOR }} }
        run: |
          pg_dump "$STAGING_DB_MIGRATOR" | gzip > "staging-$(date +%s).sql.gz"
          # artefato arquivado como evidência
      - name: Aplicar migrations (worfair_migrator)
        run: |
          docker run --rm \
            -e ConnectionStrings__Default="$STAGING_DB_MIGRATOR" \
            -e DB_MIGRATE_ONLY=true \
            ghcr.io/worfair/api:${{ github.sha }} dotnet Worfair.Api.dll

  deploy-staging:
    needs: migrate-staging
    environment: staging
    steps:
      - name: Deploy (compose de staging com imagens por sha)
        run: |
          # SSH/runner de staging: atualiza imagens e recria os containers
          docker compose -f docker-compose.staging.yml up -d --force-recreate \
            --build=false api=ghcr.io/worfair/api:${{ github.sha }}
      - name: Health checks
        run: |
          curl -fsS --retry 10 --retry-delay 5 --retry-all-errors \
            https://staging.worfair.app/health
          curl -fsS https://staging.worfair.app/health/ready
      - name: Smoke tests (E2E críticos)
        run: |
          # playwright: login → criar vaga → proposta → pagamento sandbox (Asaas)
          npx playwright test --grep "smoke" --config e2e/staging.config.ts
```

## 4. `cd-production.yml` (aprovação + rollback)

```yaml
name: CD Production

on:
  workflow_dispatch:
    inputs:
      version:
        description: 'SHA da imagem (default: main)'
        required: false

permissions:
  contents: read
  id-token: write          # OIDC p/ credenciais de curta duração no cloud/registry

jobs:
  backup-db:
    environment: production
    steps:
      - name: Snapshot do banco (pgBackRest ou snapshot cloud)
        env: { BACKUP_ROLE: ${{ secrets.PROD_BACKUP }} }
        run: pgbackrest backup --type=full --stanza=worfair-prod   # ou snapshot gerenciado

  migrate-prod:
    needs: backup-db
    environment: production      # protection rules: REQUIRED REVIEWERS
    concurrency: migrate-prod    # uma migração por vez
    steps:
      - name: Aplicar migrations
        env: { PROD_DB_MIGRATOR: ${{ secrets.PROD_DB_MIGRATOR }} }
        run: |
          docker run --rm \
            -e ConnectionStrings__Default="$PROD_DB_MIGRATOR" \
            -e DB_MIGRATE_ONLY=true \
            -e PGPASSWORD_OPTS="statement_timeout=300000 lock_timeout=30000" \
            ghcr.io/worfair/api:${{ inputs.version || github.sha }} dotnet Worfair.Api.dll
      - name: Verificar versão do esquema
        run: |
          docker run --rm -e ConnectionStrings__Default="$PROD_DB_MIGRATOR" \
            ghcr.io/worfair/api:${{ inputs.version || github.sha }} \
            dotnet Worfair.Api.dll --verify-migrations

  deploy-prod:
    needs: migrate-prod
    environment: production
    strategy:
      max-parallel: 1
    steps:
      - name: Deploy rolling (K8s) / blue-green (compose prod)
        run: |
          # kubectl rollout (imagem imutável por sha) — maxUnavailable 0, surge 1
          kubectl set image deployment/worfair-api api=ghcr.io/worfair/api:${{ inputs.version || github.sha }}
          kubectl rollout status deployment/worfair-api --timeout=10m
      - name: Health + readiness
        run: |
          curl -fsS --retry 15 --retry-delay 10 --retry-all-errors https://app.worfair.app/health/ready
      - name: Smoke tests (produção, somente leitura/transações sandbox)
        run: npx playwright test --grep "smoke-prod" --config e2e/prod.config.ts

  rollback-manual:      # rota explícita de rollback (acionada em incidente)
    if: false            # habilitada via workflow_dispatch manual
    environment: production
    steps:
      - name: Redeploy da imagem anterior (rollback)
        run: |
          kubectl set image deployment/worfair-api api=ghcr.io/worfair/api:${{ inputs.previous_sha }}
          kubectl rollout status deployment/worfair-api --timeout=10m
      # NOTA: rollback de SCHEMA = restore do snapshot (backup-db) + fix-forward
```

## 5. Segurança de secrets (resumo)

| Prática | Implementação |
| ------- | ------------- |
| Segredos por ambiente | GitHub Environments (`staging`, `production`) — nunca `env` global para credenciais |
| Credenciais efêmeras | OIDC (`permissions: id-token: write`) p/ registry/cloud — sem PAT/vendor token |
| Chaves de app | JWT RSA/Asaas/DB: apenas nos environments; imagem nunca embute secret |
| Scan | Gitleaks no CI (falha bloqueia merge) + CodeQL |
| Rotação | chaves JWT rotacionáveis (JWKS, doc security/01); Asaas key por ambiente |
| Auditoria | approval events do GitHub Environment + workflow logs retidos |

## 6. Estratégias de rollback e health

- **Rollback de app:** imagem imutável por `sha` → redeploy da anterior
  (rolling com `maxUnavailable: 0` — sem downtime).
- **Rollback de schema:** *fix-forward* (nova migração) ou restore do snapshot
  pré-migração; nunca reverter "down".
- **Health checks:** `/health` (liveness) e `/health/ready` (readiness —
  DB ping, RabbitMQ ping, atraso do outbox) — detalhes no doc 04.
- **Order de deploy:** banco primeiro (migrar → validar), depois API, depois
  frontend estático (cache-bust por sha).

## 7. Logs e observabilidade no pipeline

- Artefatos: coverage, logs de teste, relatório CodeQL — retidos por política.
- Em runtime: OpenTelemetry → coletor → Grafana (Loki/Prometheus/Tempo) —
  doc 04; cada deploy publica `deployment.revision` como label de métricas.