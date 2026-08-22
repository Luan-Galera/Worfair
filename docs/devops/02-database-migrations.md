# 02 — Database Migrations (versão + aprovação + deploy controlado)

## 1. Princípios (DEV-02)

1. **Nunca** executar migrations automaticamente na inicialização em
   staging/produção. `DB_AUTO_MIGRATE` existe **apenas** no dev local.
2. Migrações são **artefatos versionados** — geradas por desenvolvedor,
   revisadas (DBA), aplicadas em staging, aprovadas e aplicadas em produção
   como **passo explícito** do pipeline.
3. **Forward-only:** nunca editar uma migração já aplicada/mergiada; correção =
   nova migração.
4. **Aditivas primeiro** (expand/contract): renomear/dropar colunas exige
   migração em 3 fases (expandir → liberar → contrair).
5. Uma cadeia de migrações **por módulo** (por DbContext/schema) — R-09.

## 2. Ciclo de vida de uma migração

```
┌──────────┐   dotnet ef migrations add     ┌──────────────┐
│ Dev      │ ─────────────────────────────► │ Migration    │
│ (branch) │                                │ (C# + SQL    │
└──────────┘                                │  gerado)     │
                                            └──────┬───────┘
                                                   │
                        ┌──────────────────────────▼──────────────────────────┐
                        │ PR: review humana (módulo owner + DBA)              │
                        │ • SQL gerado auditado (colunas, índices, locks)     │
                        │ • testes de integração (Testcontainers) rodam       │
                        │ • rollback planejado (nova migração, nunca "down")  │
                        └──────────────────────────┬──────────────────────────┘
                                                   │ merge
                        ┌──────────────────────────▼──────────────────────────┐
                        │ STAGING (CI/CD): job migrate-staging aplica         │
                        │ • backup do banco antes                             │
                        │ • verificação pós (queries de sanidade, health)     │
                        └──────────────────────────┬──────────────────────────┘
                                                   │ aprovação humana (GitHub Environment)
                        ┌──────────────────────────▼──────────────────────────┐
                        │ PRODUÇÃO (passo explícito migrate-prod):            │
                        │ 1. snapshot/backup pré-migração                     │
                        │ 2. aplica (statement_timeout, lock_timeout)         │
                        │ 3. valida (health + smoke)                          │
                        │ 4. falha ⇒ restore + fix-forward                    │
                        └─────────────────────────────────────────────────────┘
```

## 3. Geração e revisão

```bash
# Por módulo (cada DbContext tem sua própria cadeia)
dotnet ef migrations add AddRequisitionTeamIndex \
  --project src/Modules/Recruitment/Worfair.Modules.Recruitment.Infrastructure \
  --startup-project src/Api/Worfair.Api \
  --output-dir Persistence/Migrations

# Gera o SQL para review (commitado no PR — o revisor vê exatamente o que roda)
dotnet ef migrations script --idempotent \
  --project src/Modules/Recruitment/Worfair.Modules.Recruitment.Infrastructure \
  --startup-project src/Api/Worfair.Api \
  -o migrations/recruitment/20260819_add_index.sql
```

Regras de review (checklist DBA):
- `LOCK` scope: `ALTER TABLE ... ADD COLUMN` com default não-volátil (sem lock
  longo); `CREATE INDEX CONCURRENTLY` (fora de transação) para tabelas grandes.
- `statement_timeout`/`lock_timeout` definidos no job.
- RLS: novas tabelas tenant-owned devem vir com `ENABLE ROW LEVEL SECURITY` +
  `FORCE` + política (R-02) **na mesma migração** — nunca em migração posterior.
- Tabelas financeiras/auditoria: GRANTs restritos + triggers append-only
  (SEC-04) na criação.
- Nada de `DROP` em produção sem fase contract + aprovação explícita.

## 4. Execução controlada (staging/produção)

O pipeline usa uma **imagem migrator** (mesma imagem da API, modo `--migrate`):

```yaml
# trecho do workflow de deploy (detalhe completo no doc 03)
migrate-staging:
  steps:
    - uses: docker/login ... # GHCR
    - name: Backup do banco (staging: snapshot)
      run: pg_dump "$STAGING_DB_URL" | gzip > backups/staging-$(date +%s).sql.gz
    - name: Aplicar migrations
      env:
        ConnectionStrings__Default: ${{ secrets.STAGING_DB_APP }}   # worfair_migrator
        DB_MIGRATE_ONLY: "true"
      run: |
        docker run --rm \
          -e ConnectionStrings__Default="$ConnectionStrings__Default" \
          -e DB_MIGRATE_ONLY=true \
          ghcr.io/worfair/api:${{ github.sha }} dotnet Worfair.Api.dll
    - name: Sanidade
      run: |
        # checagem de esquema (ex.: versão da migração aplicada) + health do app
        docker run --rm ghcr.io/worfair/api:${{ github.sha }} \
          -e ConnectionStrings__Default="$ConnectionStrings__Default" \
          dotnet Worfair.Api.dll --verify-migrations
```

**Guardas de produção (imutáveis):**

| Guarda | Valor |
| ------ | ----- |
| `DB_AUTO_MIGRATE` | sempre `false`/ausente em staging/prod |
| `DB_MIGRATE_ONLY` | `true` apenas no job de migração |
| Conexão de migração | `worfair_migrator` (BYPASSRLS); app usa `worfair_app` |
| `statement_timeout` | 300s (jobs de migração) |
| `lock_timeout` | 30s |
| Aprovação | `environment: production` com required reviewers |
| Backup | obrigatório antes de migrar (snapshot ou pgBackRest) |

## 5. Rollback de migração

- Migrações **não** têm "down" em produção (forward-only).
- Rollback de schema = **nova migração reversa** (fix-forward).
- Rollback de dados = **restore do backup** pré-migração + reaplicar versão
  anterior do código (imagem imutável por sha).
- Imagem da API é versionada por `sha`; voltar = redeploy da imagem anterior
  (o app roda mesmo com schema adiante — padrão expand).

## 6. O que é proibido

- ❌ `Database.Migrate()` no startup em prod (`DB_AUTO_MIGRATE`).
- ❌ `EnsureCreated`/`EnsureDeleted` fora de testes locais.
- ❌ Migração editada após merge (forward-only).
- ❌ Aplicar migração "no braço" em produção (SSH manual) — somente via pipeline.