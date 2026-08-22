# Worfair — Banco de Dados (PostgreSQL)

Documento de referência oficial da camada de dados da plataforma multi-tenant,
complementar a [`docs/architecture/`](../architecture/README.md). Todo o conteúdo
aqui **respeita** as decisões D-01 a D-11; refinamentos são registrados na
[seção 2](#2-atualizações-de-continuidade-v11) abaixo.

## 1. Índice

| Doc | Tema |
| --- | ---- |
| [01 — Estratégia de Multi-Tenancy](01-estrategia-multi-tenancy.md) | Comparação técnica (Shared+TenantId, RLS, Schema-per-tenant, Database-per-tenant) e justificativa da escolha |
| [02 — Regras Críticas de Banco](02-regras-criticas.md) | TenantId obrigatório, RLS obrigatório, SUPER_ADMIN global, múltiplas roles por usuário, convenções |
| [03 — Modelagem Relacional](03-modelagem-relacional.md) | Tabelas, chaves estrangeiras e índices por schema (tenancy, identity, jobs, recruitment, proposals, financial, audit) |
| [04 — Scripts SQL e Mapeamento EF Core](04-scripts-sql-e-efcore.md) | DDL inicial de `tenants`, `companies`, `users`, `roles`, `user_roles` + RLS + seed + mapeamento EF Core |

## 2. Atualizações de continuidade (v1.1)

Refinamentos aplicados sobre a arquitetura v1.0 (nenhuma decisão foi contradita;
cada item sinaliza o delta):

| # | Mudança | Base anterior | Motivo |
| - | ------- | ------------- | ------ |
| DB-01 | **RLS passa a ser obrigatório** (não "defesa opcional") em todos os tiers compartilhados | doc 04 de arquitetura tratava RLS como camada adicional | Requisito explícito: impedir cross-tenant mesmo com erro de programação |
| DB-02 | **Roles codificadas** (`OWNER`, `RECRUITER`, `HIRING_MANAGER`, …) persistidas na tabela `identity.roles` | doc 03 de arquitetura listava papéis por nome | Necessário modelo relacional RBAC com permissões |
| DB-03 | **`SUPER_ADMIN` global** com `tenant_id = NULL`; demais roles vinculadas a tenant | menção implícita de papéis globais | Isolamento explícito do escopo global |
| DB-04 | **Múltiplas roles por usuário no mesmo tenant** (`user_roles` N:N) | doc 03 assumia papel único por tenant | Requisito: usuário acumula OWNER + RECRUITER + HIRING_MANAGER |
| DB-05 | **Entidade `companies`** (empresa jurídica do tenant) | não existia | Novo requisito: Tenants vs Empresas (ver 01-estrategia, seção 5) |
| DB-06 | **Exceção whitelisted de FK**: `tenant_id → tenancy.tenants(id)` e FKs entre módulos **Tenancy e Identity** (núcleo de identidade) | "nenhuma FK entre módulos" | Integridade do núcleo multi-tenant sem violar isolamento de negócio |
| DB-07 | **Catálogo oficial de roles**: `SUPER_ADMIN`, `OWNER`, `CLIENT`, `RECRUITER`, `HIRING_MANAGER`, `PROVIDER` (seed atualizado; granularidade por permissão) | seed v1.1 (9 roles) | Definição oficial do ecossistema |
| DB-08 | **Modos operacionais** (Contratante/Prestador) NÃO são roles — contextos derivados de permissões (ver [docs/security](../security/README.md)) | não existia | Definição oficial do ecossistema |
| DB-09 | **Novas tabelas financeiras** (`transactions`, `transaction_events`, `transition_rules`, `asaas_webhook_inbox`, `transfers`) para o ciclo Asaas + Payment Split (ver [docs/financial](../financial/README.md)) | schema financial v1.1 | Integração com provedor de pagamento |

## 3. Regras de continuidade para prompts futuros

1. Nenhum prompt futuro pode permitir tabela tenant-owned **sem** `tenant_id` ou sem RLS.
2. `SUPER_ADMIN` nunca possui `tenant_id`.
3. Roles são dados (tabela `identity.roles`), não enums espalhados no código de negócio.
4. FKs entre módulos de negócio continuam proibidas (regra DB-06 é a única exceção).
5. Migrações de schema são executadas com role `worfair_migrator` (BYPASSRLS); a
   aplicação roda com `worfair_app` (sujeita a RLS).