# 01 — Estratégia de Multi-Tenancy

## 1. Esclarecimento conceitual necessário

O requisito lista três abordagens como alternativas. Tecnicamente, porém,
**Row-Level Security não é uma alternativa ao `TenantId`** — RLS **exige** a
coluna `TenantId` para funcionar (a política compara o valor da linha com o
contexto da sessão). A comparação correta é entre as estratégias de isolamento:

| Estratégia | Como isola |
| ---------- | ---------- |
| **A — Shared DB + coluna `TenantId`** | Filtro em toda query (app) |
| **B — A + RLS (PostgreSQL)** | Filtro imposto pelo próprio banco (defesa em profundidade) |
| **C — Shared DB + Schema por tenant** | Objetos (tabelas) separados por tenant |
| **D — Database por tenant** | Instância lógica de banco separada por tenant |

A seção 2 compara **A (com e sem RLS), C e D**; a seção 3 decide; as seções 4–5
cobrem escalabilidade e a nova entidade `companies`.

## 2. Comparação técnica

### 2.1 Shared DB + TenantId (A) — com e sem RLS

| Critério | A (sem RLS) | B = A + RLS |
| -------- | ----------- | ----------- |
| Custo de infraestrutura | Baixo (1 cluster) | Baixo (1 cluster) |
| Migrações | 1 cadeia | 1 cadeia |
| Custo por tenant | Quase zero | Quase zero |
| Risco de vazamento cross-tenant | **Médio-alto** (depende de disciplina do dev; 1 `SELECT` sem filtro = vazamento) | **Muito baixo** (banco recusa linha de outro tenant, mesmo em SQL cru) |
| Proteção em erro de programação | Nenhuma | Sim (política USING/WITH CHECK + FORCE RLS) |
| Overhead de performance | — | ~3–10% em OLTP típico (avaliação de política por linha; mitigável com particionamento e políticas simples) |
| Análise cross-tenant (operacional) | Trivial | Trivial (sessão com var não setada ou role dedicada) |
| Pooling de conexões | Excelente | Excelente (var de sessão setada por conexão no interceptor EF) |
| Backup/restore | Global | Global |
| Complexidade operacional | Baixa | Baixa-média (políticas por tabela, roles BYPASSRLS) |

**Veredito:** sem RLS, a coluna `TenantId` é **necessária mas insuficiente** para a
regra "impedir consultas cross-tenant mesmo em caso de erro de programação". Com
RLS, o banco se torna o último guardião — exatamente o requisito declarado.

### 2.2 Shared DB + Schema por tenant (C)

| Critério | Avaliação |
| -------- | --------- |
| Isolamento lógico | Bom (objetos separados), mas **não físico** |
| Migrações | N (uma por tenant) — automação obrigatória, risco de drift |
| Pooling | Pior (search_path por tenant; conexões não intercambiáveis sem custo) |
| Catálogo | Incha com N× objetos (centenas de tenants = milhares de tabelas) |
| Analytics cross-tenant | Difícil (UNION ALL de N schemas) |
| Restore por tenant | Parcial (por schema, dependente de ferramenta) |
| RLS | Possível (políticas por schema) |
| Veredito | Sobrecarga operacional cresce linearmente com o número de tenants; **não se justifica** como padrão nesta fase |

### 2.3 Database per tenant (D)

| Critério | Avaliação |
| -------- | --------- |
| Isolamento | **Físico** (mais forte) — atende residência de dados (LGPD/GDPR) e auditoria dedicada |
| Custo | Alto: overhead por database, limites de `max_connections`, backups N× |
| Migrações | N (automação CI/CD obrigatória) |
| Analytics cross-tenant | Requer ETL/agregação externa |
| Ops | Restore pontual excelente; capacidade de administração multiplicada |
| Veredito | Excelente **para o tier Enterprise** (clientes com compliance estrito), inviável como padrão para SaaS de volume |

## 3. Decisão (DB-01, refine D-08)

> **Adotado: Shared Database + coluna `TenantId` obrigatória + RLS ativado e
> `FORCE` em TODAS as tabelas tenant-owned** (estratégia B), como tier único
> `Standard` em produção. `Database-per-tenant` permanece como tier `Enterprise`
> (política comercial/compliance), usando as mesmas abstrações de conexão
> definidas no doc 04 de arquitetura.

**Justificativa:**

1. **Segurança:** RLS transforma "regra de código" em "regra de banco" — a
   única forma de garantir o requisito *"impedir consultas cross-tenant mesmo em
   caso de erro de programação"*.
2. **Escalabilidade:** custo marginal por tenant ≈ 0; crescimento é resolvido
   com **particionamento por `tenant_id`** (hash para distribuir I/O) e **réplicas
   de leitura** para relatórios — sem mudança de modelo.
3. **Operação:** uma única cadeia de migrações (módulo por módulo), backup global
   simples, pooling eficiente, on-call simples.
4. **Custo:** menor TCO para 95% dos clientes; o tier Enterprise cobre o nicho
   que exige isolamento físico e paga por ele.

**Rejeitadas:** estratégia A sem RLS (não cumpre o requisito de segurança),
schema-per-tenant (complexidade linear sem ganho proporcional), DB-per-tenant
como padrão (custo/ops desproporcionais nesta fase).

## 4. Escalabilidade (horizonte de crescimento)

| Cenário | Resposta |
| ------- | -------- |
| Mais tenants, mesmo volume por tenant | Nada a fazer (modelo atual escala) |
| Poucos tenants gigantes (particionamento) | `PARTITION BY HASH (tenant_id)` nas tabelas de maior volume (`audit_logs`, `candidates`, `job_requisitions`) — RLS é compatível com particionamento (política aplicada à tabela particionada) |
| Leitura pesada (relatórios/dashboard) | Réplicas de leitura; conexões de leitura apontam para réplica |
| Dados quentes/frios | `pg_partman` para retenção de `audit_logs` (partição mensal) |
| Tenant com compliance estrita | Promoção ao tier Enterprise (Database-per-tenant) via módulo Tenants, sem mudança de código de negócio (abstração `ITenantDbContextAccessor`) |

## 5. Tenants × Empresas (novo conceito — DB-05)

O requisito distingue **Tenant** de **Empresa**. Modelagem adotada:

- **`tenants`** = espaço contratual na plataforma (assinatura, tier, status,
  timezone). É a **raiz de isolamento**: todo dado tenant-owned referencia
  `tenant_id`.
- **`companies`** = pessoa jurídica operacional dentro do tenant (CNPJ, razão
  social). Um tenant pode ter **1:N empresas** (ex.: holding com várias
  CNPJs); a tabela é tenant-owned.
- Registros de negócio (ex.: `job_requisitions`) apontam para `company_id`
  **opcional** — um tenant pode operar sem empresa formal (pessoa física).

Relacionamento: `tenants 1 ──── N companies`; `users N ──── N tenants`
(membership); `users N ──── N roles` por tenant (`user_roles`).