# Worfair — Segurança, Autenticação e Autorização

Documento de referência oficial da camada de segurança, complementar a
[`docs/architecture/`](../architecture/README.md) e
[`docs/database/`](../database/README.md). **Alinhado** às decisões D-01..D-11,
R-01..R-12 e DB-01..DB-06; novos deltas registrados na seção 2.

## 1. Índice

| Doc | Tema |
| --- | ---- |
| [01 — Autenticação JWT](01-autenticacao-jwt.md) | Estrutura do token, claims, validação no servidor e por que claims nunca são fonte de verdade |
| [02 — Proteção contra manipulação de Tenant](02-protecao-manipulacao-tenant.md) | `X-Tenant-Id` rejeitado, tenant efetivo derivado do contexto autenticado, switch de tenant controlado |
| [03 — Políticas de Autorização](03-politicas-autorizacao.md) | Requirements/Policies ASP.NET Core, Modo Contratante × Modo Prestador, matriz de permissões |
| [04 — Exemplo: `IAuthorizationHandler`](04-exemplo-iauthorizationhandler.md) | Implementação prática: permissão + tenant do recurso + múltiplas roles + modo |
| [05 — Proteção Financeiro e Auditoria](05-protecao-financeiro-auditoria.md) | Append-only, transições controladas, ledger, triggers e hash chain |

## 2. Atualizações de continuidade (v1.2)

| # | Mudança | Base anterior | Motivo |
| - | ------- | ------------- | ------ |
| DB-07 | **Catálogo oficial de roles**: `SUPER_ADMIN`, `OWNER`, `CLIENT`, `RECRUITER`, `HIRING_MANAGER`, `PROVIDER` (seed atualizado). `ADMIN`, `SOURCER`, `INTERVIEWER`, `FINANCE`, `VIEWER` saem do catálogo; granularidade passa a ser expressa por **permissões** | seed v1.1 (9 roles) | Definição oficial do ecossistema |
| DB-08 | **Modos operacionais** (Contratante/Prestador) NÃO são roles — são **contextos derivados de permissões**; persistidos apenas como claim de contexto no JWT | não existia | Definição oficial do ecossistema |
| SEC-01 | **JWT é transporte de contexto, não fonte de verdade** — autorização revalida usuário/tenant/roles/permissões no servidor a cada requisição | implícito | Requisito de segurança explícito |
| SEC-02 | **Proibido header `X-Tenant-Id`**; tenant efetivo só do token validado; switch de tenant via endpoint controlado | tenant vinha do claim (não havia regra anti-manipulação) | Requisito de segurança explícito |
| SEC-03 | **Autorização por permissão efetiva** (união de roles no banco) + modo exigido por endpoint | role única por requisição | Multi-roles + modos |
| SEC-04 | **Financeiro e auditoria append-only** no banco (GRANTs + triggers) | não especificado | Requisito de segurança explícito |

## 3. Regras de continuidade para prompts futuros

1. Nenhuma autorização pode confiar exclusivamente em claims do JWT.
2. Nenhum endpoint aceita `TenantId` informado pelo cliente (header/body) como
   definidor de contexto — somente o contexto autenticado vale.
3. `mode` (Contratante/Prestador) nunca concede permissão sozinho; é sempre
   combinado com verificação de permissão efetiva.
4. Tabelas financeiras e `audit_logs` são **append-only** (sem UPDATE/DELETE
   para a aplicação).
5. `SUPER_ADMIN` permanece global (sem `tenant_id`).