# 02 — Regras Críticas de Banco

Regras **normativas** (R-01 a R-12). Violação de R-01 a R-06 bloqueia o CI.

## R-01 — Toda tabela tenant-owned exige `tenant_id` explícito

- Coluna `tenant_id uuid NOT NULL` em **toda** tabela de negócio (schemas
  `jobs`, `recruitment`, `proposals`, `financial`, `notifications`, `audit`,
  e tabelas tenant-owned de `tenancy`/`identity`).
- Padrão de chave primária em tabelas tenant-owned:
  - `id uuid PRIMARY KEY DEFAULT gen_random_uuid()` + `UNIQUE (tenant_id, id)`
    **ou** PK composta `(tenant_id, id)` — o projeto adota **`id` global + índice
    único `(tenant_id, id)`** (IDs estáveis e menores, mesma garantia de unicidade
    por tenant).
- **Índices compostos sempre começando por `tenant_id`** nos caminhos quentes:

```sql
CREATE UNIQUE INDEX uq_job_requisitions_tenant_id
    ON recruitment.job_requisitions (tenant_id, id);
CREATE INDEX ix_job_requisitions_tenant_status
    ON recruitment.job_requisitions (tenant_id, status);
CREATE INDEX ix_candidates_tenant_email
    ON recruitment.candidates (tenant_id, lower(email));
```

## R-02 — RLS obrigatório e `FORCE` em todas as tabelas tenant-owned

- `ALTER TABLE ... ENABLE ROW LEVEL SECURITY;`
- `ALTER TABLE ... FORCE ROW LEVEL SECURITY;` — sem isso, o **dono** da tabela
  (role que roda migrações) ignora as políticas.
- Políticas **sempre** com `USING` **e** `WITH CHECK`:

```sql
CREATE POLICY tenant_isolation ON recruitment.job_requisitions
    FOR ALL
    USING (tenant_id::text = current_setting('app.tenant_id', true))
    WITH CHECK (tenant_id::text = current_setting('app.tenant_id', true));
```

- O contexto é definido **por conexão** (`app.tenant_id`), setado pelo
  `DbConnectionInterceptor` do EF (já documentado em
  [`docs/architecture/04`](../architecture/04-isolamento-de-dados-por-tenant.md)).
- **Roles de banco:** `worfair_migrator` (executa migrações, `BYPASSRLS`) e
  `worfair_app` (runtime da aplicação, **sujeita** a RLS).
- Com `app.tenant_id` ausente, `current_setting(..., true)` retorna NULL →
  nenhuma linha é retornada (deny-by-default).

## R-03 — Tabelas globais (sem RLS, sem `tenant_id`)

Lista fechada e única de tabelas globais:

| Tabela | Schema | Por quê |
| ------ | ------ | ------- |
| `tenants` | tenancy | raiz de isolamento — precisa ser lida antes do contexto existir |
| `users` | identity | identidade global da pessoa |
| `roles` | identity | catálogo global de roles (inclui `SUPER_ADMIN`) |
| `permissions` | identity | catálogo global de permissões |
| `role_permissions` | identity | vínculo global role↔permissão |

Qualquer nova tabela global exige **aprovação em ADR**.

## R-04 — `SUPER_ADMIN` é global; `tenant_id` é `NULL`

- Role `SUPER_ADMIN` tem `is_global = true` em `identity.roles`.
- Na tabela `identity.user_roles`, linhas com role global possuem
  `tenant_id = NULL`; linhas com role de tenant possuem `tenant_id NOT NULL`.
- A coerência é **imposta pelo banco** (FK composta + CHECK, DDL em
  [04 — Scripts](04-scripts-sql-e-efcore.md#tabelas-críticas)):

```sql
CHECK ((tenant_id IS NULL) = is_global)
```

- Consequência prática: `SUPER_ADMIN` **nunca** é filtrado por tenant; acessa
  dados globais (ex.: `tenants`, `users`) e, para inspeção de um tenant
  específico, define `app.tenant_id` manualmente na sessão (auditável).

## R-05 — Múltiplas roles por usuário no mesmo tenant

- `user_roles` é N:N: `(user_id, tenant_id, role_id)` com PK composta —
  **um usuário pode ter `OWNER`, `RECRUITER` e `HIRING_MANAGER` ao mesmo tempo**
  no mesmo tenant (3 linhas).
- **Permissões efetivas = união das permissões de todas as roles** do usuário
  no tenant corrente (SQL de resolução em [03 — Modelagem](03-modelagem-relacional.md#identity)).
- A relação exige membership ativa: FK composta
  `(user_id, tenant_id) → tenancy.tenant_memberships(user_id, tenant_id)`
  (com `MATCH SIMPLE`, o caso global `tenant_id NULL` não é validado).

## R-06 — Impedir cross-tenant mesmo com erro de programação

Cadeia de três camadas (se uma falhar, a próxima segura):

| Camada | Mecanismo | Falha que cobre |
| ------ | --------- | --------------- |
| Aplicação | `ITenantProvider` + Global Query Filters + `TenantSaveChangesInterceptor` (docs architecture/04) | fluxo normal |
| Persistência | RLS `FORCE` (R-02) | SQL cru, query sem filtro, ORM burlado |
| Banco | Políticas com `WITH CHECK` | escrita/update cross-tenant, `INSERT` com tenant errado |

**Regras complementares:**
- Handlers **nunca** aceitam `tenant_id` como input (vem do JWT).
- Repositório: `GetById` devolve `null` para ID de outro tenant (nunca erro de
  autorização com mensagem reveladora).
- SQL cru / projeções: obrigatório `WHERE tenant_id = current_setting(...)` ou
  acesso via view com RLS herdada (views com `security_invoker`).
- Testes de integração obrigatórios: leitura/escrita/delete cross-tenant
  retornam vazio ou exceção (ver [03 — Modelagem](03-modelagem-relacional.md#testes-obrigatórios)).

## R-07 — FKs entre módulos: proibidas, com exceção whitelisted (DB-06)

- **Proibido:** FK entre módulos de negócio (`jobs`, `recruitment`, `proposals`,
  `financial`, `notifications`) entre si e para outros módulos.
- **Permitido (whitelist):**
  1. `tenant_id → tenancy.tenants(id)` em qualquer tabela tenant-owned
     (integridade do isolamento);
  2. FKs **dentro do núcleo Tenancy/Identity** (`tenancy` ↔ `identity`):
     `tenant_memberships.user_id → identity.users(id)`,
     `user_roles.user_id → identity.users(id)`,
     `user_roles.(user_id, tenant_id) → tenant_memberships`,
     `user_roles.(role_id, is_global) → roles(id, is_global)`.
- FKs dentro do mesmo módulo: permitidas (ex.: `interview_feedbacks → interviews`).
- Referências a entidades de outros módulos: apenas `uuid` sem FK.

## R-08 — Auditoria registra tenant, inclusive ações globais

- `audit.audit_logs.tenant_id` é **nullable**: `NULL` para ações de
  `SUPER_ADMIN`/plataforma, valor presente para ações dentro de tenant.
- Toda alteração de dados sensíveis (financeiro, proposta, contrato, role)
  gera registro auditável (via `UPDATE` trigger genérico ou interceptor da app).

## R-09 — Convenções

| Item | Regra |
| ---- | ----- |
| Schemas | `tenancy`, `identity`, `jobs`, `recruitment`, `proposals`, `financial`, `notifications`, `audit` |
| Nomes | `snake_case`, tabelas no plural, colunas no singular |
| Chaves | `id uuid PRIMARY KEY DEFAULT gen_random_uuid()` |
| Timestamps | `timestamptz` (`created_at`, `updated_at`, sempre UTC) |
| Enums | `smallint` + `CHECK` nomeado (ex.: `chk_job_requisitions_status`) — lookup tables apenas quando há catálogo mutável |
| Índices | `ix_<tabela>_<colunas>`; `uq_<tabela>_<colunas>` para únicos |
| Moeda | `numeric(12,2)` + coluna `currency char(3)` (ISO 4217) — **nunca** `float` |
| Status | sempre `smallint` com `CHECK` e mapeamento constante no código |

## R-10 — Testes de integração obrigatórios

Sempre que uma tabela tenant-owned for criada, adicionar ao menos:
1. Leitura de tenant B não enxerga linhas do tenant A (query filter + RLS).
2. `UPDATE`/`DELETE` em linha de outro tenant é bloqueado (RLS WITH CHECK /
   interceptor).
3. `INSERT` sem `app.tenant_id` é recusado (deny-by-default).
4. `SUPER_ADMIN` (`tenant_id NULL`) não vaza para contexto de tenant.

## R-11 — Migrações e permissões

- Migrações rodam como `worfair_migrator` (BYPASSRLS) — necessário para
  `ALTER`/seed; **nunca** usado em runtime.
- `worfair_app` recebe `GRANT SELECT/INSERT/UPDATE/DELETE` em tabelas de módulos
  via default privileges; sem `TRUNCATE`, sem `ALTER`.
- Seed de roles/permissões é **idempotente** (`ON CONFLICT DO NOTHING`).

## R-12 — Membro inativo não executa nada

- `tenant_memberships.status` (active/invited/disabled) é validado na
  aplicação (middleware de autorização) **antes** de qualquer query; RLS não
  substitui controle de membership ativo.