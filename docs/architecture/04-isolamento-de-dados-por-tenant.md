# 04 — Isolamento de Dados por Tenant

## 1. Estratégia adotada (D-08)

**Padrão: Shared Database + Shared Schema**, com coluna `TenantId` em todas as
tabelas tenant-owned. Tiers opcionais por tenant, decididos no módulo Tenants:

| Tier | Estratégia | Quando usar |
| ---- | ---------- | ----------- |
| `Standard` (padrão) | Shared DB + Shared Schema | 95% dos casos — custo e operação simples |
| `Pro` | Shared DB + **Schema por tenant** (`recruitment_<tenant>` …) | requisitos de isolamento/RBAC reforçado no banco |
| `Enterprise` | **Database por tenant** | LGPD/GDPR estrito, auditoria dedicada, volume extremo |

As três estratégias compartilham as **mesmas abstrações** (seção 3); o que muda é a
resolução de conexão/schema. Migração entre tiers é suportada (parada de escrita,
cópia, troca de `TenantTier`).

### 1.1 O que é tenant-owned × global

| Tipo | Tabelas | Exemplos |
| ---- | ------- | -------- |
| **Tenant-owned** | `ITenantEntity` + coluna `TenantId` (PK composta ou índice) | job_requisitions, candidates, invoices, notifications… |
| **Global** | sem `TenantId`, acessível a partir do token com permissão global | `tenants`, `users` (identidade), `roles`, `permissions` |

Dados de referência (skills, categorias) são **tenant-owned**: cada tenant tem seu
catálogo, com seed por tenant no provisionamento.

## 2. Cadeia de confiança (defense in depth)

O tenant nunca vem do corpo da requisição. Ele é resolvido **uma única vez** no
token JWT e propagado por toda a stack:

```
HTTP Request (JWT: tenant_id)
   │
   ▼
[1] TenantContextMiddleware ──► ITenantProvider.TenantId
   │
   ▼
[2] Application (handlers MediatR) ── nunca aceitam TenantId como input
   │
   ▼
[3] EF Core:
      • Global Query Filters (leitura)
      • TenantSaveChangesInterceptor (escrita)
   │
   ▼
[4] PostgreSQL RLS (defesa final no banco)
```

## 3. Implementação no EF Core (tier Standard)

### 3.1 Resolução do tenant corrente

```csharp
// BuildingBlocks.Domain.Tenancy
public interface ITenantProvider
{
    TenantId? TenantId { get; }
    bool IsMultitenantRequest => TenantId is not null;
}

// Api (Worfair.Api) — lê o claim, uma única vez, no início do request
public sealed class TenantContextMiddleware(ITenantProvider tenantProvider)
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var claim = context.User.FindFirstValue(CustomClaims.TenantId);
        if (claim is not null)
            tenantProvider.SetTenant(new TenantId(Guid.Parse(claim)));

        await next(context);
    }
}
```

### 3.2 Marcação de entidades tenant-owned

```csharp
public interface ITenantEntity
{
    TenantId TenantId { get; }
    void SetTenantId(TenantId tenantId);   // internal — chamada apenas pelo interceptor
}
```

Toda `Entity` de negócio em todos os módulos implementa `ITenantEntity`
(herdada da base `TenantEntity` em BuildingBlocks, quando aplicável).

### 3.3 Global Query Filters (proteção de leitura)

Aplicados **automaticamente por convenção** no `OnModelCreating` de cada DbContext
de módulo — nenhuma entidade precisa de configuração manual:

```csharp
// BuildingBlocks.Infrastructure.Persistence.Tenant
public static class TenantQueryFilterExtensions
{
    public static void ApplyTenantQueryFilters(this ModelBuilder modelBuilder, TenantId? tenantId)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
                continue;

            var tenantIdProperty = Expression.Property(
                Expression.Parameter(entityType.ClrType, "e"),
                nameof(ITenantEntity.TenantId));

            var filter = Expression.Lambda(
                Expression.Equal(tenantIdProperty,
                    Expression.Constant(tenantId, typeof(TenantId))),
                Expression.Parameter(entityType.ClrType, "e"));

            entityType.SetQueryFilter(filter);
        }
    }
}

// Em cada DbContext de módulo (ex.: RecruitmentDbContext)
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(RecruitmentDbContext).Assembly);

    if (_tenantProvider.TenantId is { } tenantId)
        modelBuilder.ApplyTenantQueryFilters(tenantId);
}
```

> Os filtros são aplicados **dinamicamente** com o tenant da sessão do request.
> Para garantir modelo cacheado por tenant (e não por sessão), registra-se um
> `IModelCacheKeyFactory` próprio — necessário também para o tier Pro (schema).

### 3.4 TenantSaveChangesInterceptor (proteção de escrita)

```csharp
public sealed class TenantSaveChangesInterceptor(ITenantProvider tenantProvider)
    : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        var context = eventData.Context;
        if (context is null) return base.SavingChanges(eventData, result);

        var tenantId = tenantProvider.TenantId;

        foreach (var entry in context.ChangeTracker.Entries<ITenantEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    if (tenantId is null)
                        throw new TenantRequiredException(
                            "Entidade tenant-owned exige um tenant ativo no contexto.");
                    entry.Entity.SetTenantId(tenantId.Value);
                    break;

                case EntityState.Modified:
                    var original = entry.OriginalValues.GetValue<TenantId>(nameof(ITenantEntity.TenantId));
                    if (original != tenantId)
                        throw new TenantMismatchException(
                            "Tentativa de alterar (ou mover) dados de outro tenant.");
                    break;

                case EntityState.Deleted:
                    var deleted = entry.OriginalValues.GetValue<TenantId>(nameof(ITenantEntity.TenantId));
                    if (deleted != tenantId)
                        throw new TenantMismatchException(
                            "Tentativa de excluir dados de outro tenant.");
                    break;
            }
        }

        return base.SavingChanges(eventData, result);
    }
}
```

**Regra complementar em repositórios** (nunca confiar só no EF):

```csharp
public sealed class JobRequisitionRepository(RecruitmentDbContext db, ITenantProvider tenantProvider)
    : IJobRequisitionRepository
{
    public async Task<JobRequisition?> GetByIdAsync(JobRequisitionId id, CancellationToken ct = default)
    {
        // NUNCA buscar por ID global e filtrar depois: o filtro do tenant
        // já está no query filter; o retorno null é a "não existência" para este tenant.
        return await db.JobRequisitions.SingleOrDefaultAsync(r => r.Id == id, ct);
    }
}
```

### 3.5 Migrações

- Cada módulo tem **uma** cadeia de migrações (via `MigrationsAssembly` próprio).
- Tier Standard: uma migração altera todas as tabelas do schema do módulo.
- Tier Pro/Enterprise: mesmas migrações aplicadas por tenant (script de aplicação
  parametrizado — fora do escopo deste doc, ver ADR quando implementado).

## 4. Defesa em profundidade: PostgreSQL Row-Level Security

Mesmo que um bug na aplicação ignore os filtros, o banco **recusa** o acesso
cruzado. Aplicado para os tiers Standard/Pro (Enterprise ganha isolamento físico).

```sql
-- 1) Habilitar RLS nas tabelas tenant-owned do módulo
ALTER TABLE recruitment.job_requisitions ENABLE ROW LEVEL SECURITY;

-- 2) Política padrão: somente linhas do tenant corrente
CREATE POLICY tenant_isolation ON recruitment.job_requisitions
    USING (tenant_id::text = current_setting('app.tenant_id', true));

-- 3) Forçar tenant nas operações de escrita também
CREATE POLICY tenant_isolation_write ON recruitment.job_requisitions
    FOR ALL
    USING (tenant_id::text = current_setting('app.tenant_id', true))
    WITH CHECK (tenant_id::text = current_setting('app.tenant_id', true));
```

O `app.tenant_id` é definido **por conexão** via `DbConnectionInterceptor` do EF:

```csharp
public sealed class TenantConnectionInterceptor(ITenantProvider tenantProvider)
    : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection,
        ConnectionEndEventData eventData)
    {
        if (tenantProvider.TenantId is not { } tenantId)
            return;

        using var command = connection.CreateCommand();
        command.CommandText =
            $"SELECT set_config('app.tenant_id', '{tenantId.Value}', false);";
        command.ExecuteNonQuery();
    }
}
```

> RLS exige que `current_setting(..., true)` não retorne null (nenhum tenant
> ativo) — nesse caso o acesso é negado, o que é o comportamento desejado.

## 5. Tiers Pro/Enterprise — abstrações que mantêm o código igual

```csharp
// Contratos em BuildingBlocks.Infrastructure.Persistence.Tenant
public interface ITenantDbContextAccessor
{
    // Tier Standard: devolve conexão default do módulo
    // Tier Pro:       devolve conexão + schema do tenant
    // Tier Enterprise: devolve connection string do tenant (módulo Tenants)
    DbContextOptions ResolveOptionsFor(TenantId tenantId, string moduleConnectionString);
}

public sealed class PerTenantModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime)
        => context is IHaveTenantSchema { TenantId: { } tenantId }
            ? (context.GetType(), tenantId)   // modelo por tenant (schema-per-tenant)
            : context.GetType();
}
```

O registro no composition root do módulo:

```csharp
services.AddDbContext<RecruitmentDbContext>((sp, options) =>
{
    var accessor = sp.GetRequiredService<ITenantDbContextAccessor>();
    var tenantId = sp.GetRequiredService<ITenantProvider>().TenantId;
    options.UseNpgsql(accessor.ResolveOptionsFor(tenantId, optionsRecruitment.ConnectionString));
});
```

## 6. Regras de bolso (checklist do desenvolvedor)

1. Toda entidade de negócio implementa `ITenantEntity` (exceto tabelas globais listadas em 1.1).
2. Handlers de Application **nunca** recebem `TenantId` no input; sempre de `ITenantProvider`.
3. `GetById`/`Find` de repositório: o filtro de tenant vem do query filter — retorno
   `null`/`not found` quando não pertence ao tenant corrente (sem mensagem reveladora).
4. Tabelas de módulos não têm FK para tabelas de outros módulos — única exceção
   whitelisted: `tenant_id → tenancy.tenants(id)` e FKs do núcleo Tenancy/Identity
   (ver [docs/database/02 — Regras críticas](../../docs/database/02-regras-criticas.md)).
5. SQL cru (projeções, relatórios): aplicar `WHERE tenant_id = @currentTenant` — nunca
   usar `SELECT` sem filtro, mesmo que "leitura global".
6. Testes de integração obrigatórios: escrita de um tenant invisível para outro,
   `TenantMismatchException` em updates/deletes cruzados, RLS ativo nas tabelas
   tenant-owned (teste com `SET app.tenant_id` divergente).