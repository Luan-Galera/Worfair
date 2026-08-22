# 04 — Exemplo Prático: `IAuthorizationHandler`

Implementação de referência para a regra:

> *"Validar se o usuário tem permissão para **gerenciar uma vaga** (ou contrato)
> **daquele tenant específico**, respeitando múltiplas roles e o contexto atual."*

## 1. Leitura do estado de acesso (fonte da verdade)

```csharp
// BuildingBlocks.Application/Security/UserAccessContext.cs
public sealed record UserAccessContext(
    Guid UserId,
    TenantId? TenantId,
    bool UserActive,
    bool MembershipActive,
    IReadOnlySet<string> Roles,
    IReadOnlySet<string> Permissions,
    AccessMode? Mode);

// Porta implementada pelo módulo Identity.Infrastructure (lê banco, R-05/R-06)
public interface IUserAccessReader
{
    Task<UserAccessContext> GetContextAsync(Guid userId, TenantId? tenantId, CancellationToken ct);
}
```

```csharp
// Módulo Identity — implementação (união de roles no tenant, ver docs/database/03)
public sealed class UserAccessReader(IdentityDbContext db) : IUserAccessReader
{
    public async Task<UserAccessContext> GetContextAsync(Guid userId, TenantId? tenantId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == new UserId(userId), ct);

        var roles = new HashSet<string>();
        var permissions = new HashSet<string>();

        if (user is not null && user.Status == UserStatus.Active)
        {
            roles = (await (from ur in db.UserRoles
                            join r in db.Roles on ur.RoleId equals r.Id
                            where ur.UserId == new UserId(userId)
                                  && (tenantId == null ? ur.TenantId == null : ur.TenantId == tenantId)
                            select r.Code).ToListAsync(ct)).ToHashSet();

            permissions = (await (from ur in db.UserRoles
                                  join rp in db.RolePermissions on ur.RoleId equals rp.RoleId
                                  join p in db.Permissions on rp.PermissionId equals p.Id
                                  where ur.UserId == new UserId(userId)
                                        && (tenantId == null ? ur.TenantId == null : ur.TenantId == tenantId)
                                  select p.Code).Distinct().ToListAsync(ct)).ToHashSet();
        }

        var membershipActive = tenantId is null || await db.TenantMemberships.AsNoTracking()
            .AnyAsync(m => m.TenantId == tenantId && m.UserId == new UserId(userId)
                && m.Status == MembershipStatus.Active, ct);

        return new UserAccessContext(
            userId, tenantId,
            user is { Status: UserStatus.Active },
            membershipActive,
            roles, permissions,
            AccessModeMapper.Derive(permissions));
    }
}
```

> Quando `tenantId == null` (contexto global), a consulta filtra `ur.TenantId ==
> null` — isolamento entre escopo global e de tenant mantido na leitura (R-04).

## 2. Requirements

```csharp
// BuildingBlocks.Application/Security/Requirements.cs
public sealed class AuthenticatedScopeRequirement : IAuthorizationRequirement { }
public sealed class TenantScopeRequirement : IAuthorizationRequirement { }
public sealed class GlobalScopeRequirement : IAuthorizationRequirement { }

public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public string PermissionCode { get; }
    public AccessMode? RequiredMode { get; }

    public PermissionRequirement(string permissionCode, AccessMode? requiredMode = null)
    {
        PermissionCode = permissionCode;
        RequiredMode = requiredMode;
    }
}

// Resource-based: o recurso (ex.: JobRequisitionId) é passado na autorização
public sealed class ManageRequisitionRequirement : IAuthorizationRequirement { }
public sealed class ManageContractRequirement : IAuthorizationRequirement { }
```

## 3. Handlers

### 3.1 Acesso autenticado + escopo (valida claims contra o banco)

```csharp
// BuildingBlocks.Application/Security/AuthorizationContextLoader.cs
public static class AuthorizationContextLoader
{
    public const string ItemsKey = "__worfair_access_context";

    public static UserAccessContext? GetOrNull(HttpContext http) =>
        http.Items.TryGetValue(ItemsKey, out var ctx) ? ctx as UserAccessContext : null;

    public static void Store(HttpContext http, UserAccessContext ctx) =>
        http.Items[ItemsKey] = ctx;

    public static TenantId? EffectiveTenant(ClaimsPrincipal user) =>
        user.FindFirstValue(CustomClaims.TenantId) is { } raw && Guid.TryParse(raw, out var tid)
            ? new TenantId(tid) : null;
}

// BuildingBlocks.Application/Security/Handlers/AuthenticatedScopeHandler.cs
public sealed class AuthenticatedScopeHandler(IUserAccessReader reader, IHttpContextAccessor http)
    : AuthorizationHandler<AuthenticatedScopeRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, AuthenticatedScopeRequirement requirement)
    {
        if (http.HttpContext is null) return;

        var userId = ParseUserId(context.User);
        if (userId is null) return;

        var tenantId = AuthorizationContextLoader.EffectiveTenant(context.User);
        var access = await reader.GetContextAsync(userId.Value, tenantId, CancellationToken.None);

        // Usuário deve existir e estar ativo
        if (!access.UserActive) return;

        // Contexto de tenant exige membership ativa + tenant ativo (validação no repositório)
        if (tenantId is not null && !access.MembershipActive) return;

        AuthorizationContextLoader.Store(http.HttpContext, access);
        context.Succeed(requirement);
    }
}
```

### 3.2 Escopo de tenant (endpoints tenant-scoped)

```csharp
public sealed class TenantScopeHandler : AuthorizationHandler<TenantScopeRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, TenantScopeRequirement requirement)
    {
        if (AuthorizationContextLoader.EffectiveTenant(context.User) is not null)
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

public sealed class GlobalScopeHandler : AuthorizationHandler<GlobalScopeRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, GlobalScopeRequirement requirement)
    {
        var hasTenant = AuthorizationContextLoader.EffectiveTenant(context.User) is not null;
        var hasSuperAdmin = context.User.HasClaim(CustomClaims.Roles, RoleCodes.SuperAdmin);
        if (!hasTenant && hasSuperAdmin)
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
```

### 3.3 Permissão efetiva + modo (múltiplas roles, contexto atual)

```csharp
public sealed class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var access = AuthorizationContextLoader.GetOrNull(RequireHttp(context));
        if (access is null) return Task.CompletedTask;   // AuthenticatedScope falhou antes

        // 1. Permissão efetiva (união das roles ATUAIS no banco) — claims ignorados
        if (!access.Permissions.Contains(requirement.PermissionCode))
            return Task.CompletedTask;

        // 2. Modo exigido: o token pode "dizer" provider, mas o banco decide
        if (requirement.RequiredMode is { } required)
        {
            var claimed = context.User.FindFirstValue(CustomClaims.Mode);
            if (!string.Equals(claimed, required.ToString(), StringComparison.OrdinalIgnoreCase))
                return Task.CompletedTask;

            // Modo revalidado contra as permissões efetivas (SEC-03)
            if (access.Mode != required)
                return Task.CompletedTask;
        }

        context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
```

### 3.4 Recurso: vaga daquele tenant (o exemplo solicitado)

```csharp
// Contrato read-only do módulo Recruitment (porta, ver arquitetura: Contracts)
public interface IJobRequisitionAccessReader
{
    Task<RequisitionAccessInfo?> GetAccessInfoAsync(JobRequisitionId id, CancellationToken ct);
}

public sealed record RequisitionAccessInfo(JobRequisitionId Id, TenantId TenantId, string Status);

// Módulo Recruitment — Infrastructure (leitura mínima, sem expor o agregado)
public sealed class JobRequisitionAccessReader(RecruitmentDbContext db)
    : IJobRequisitionAccessReader
{
    public async Task<RequisitionAccessInfo?> GetAccessInfoAsync(
        JobRequisitionId id, CancellationToken ct)
    {
        var info = await db.JobRequisitions.AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new RequisitionAccessInfo(r.Id, r.TenantId, r.Status.ToString()))
            .FirstOrDefaultAsync(ct);
        return info is null || info.TenantId == default ? null : info;
    }
}

// BuildingBlocks.Application/Security/Handlers/ManageRequisitionHandler.cs
public sealed class ManageRequisitionHandler(
    IJobRequisitionAccessReader requisitions, IHttpContextAccessor http)
    : AuthorizationHandler<ManageRequisitionRequirement, JobRequisitionId>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ManageRequisitionRequirement requirement,
        JobRequisitionId resource)
    {
        var access = AuthorizationContextLoader.GetOrNull(http.HttpContext!);
        if (access is null) return;

        // 1. Permissão efetiva (multi-role) — mesma permissão da policy base
        if (!access.Permissions.Contains("recruitment.requisition.manage")) return;

        // 2. Modo Contratante (revalidado)
        if (access.Mode != AccessMode.Contracting) return;

        // 3. Tenant do contexto == tenant da vaga (SEMPRE do banco)
        var info = await requisitions.GetAccessInfoAsync(resource, CancellationToken.None);
        if (info is null) return;                              // inexistente ⇒ 404
        if (info.TenantId != access.TenantId) return;          // cross-tenant ⇒ 404

        context.Succeed(requirement);
    }
}
```

> **Contrato:** o mesmo padrão vale para `ManageContractHandler` — basta trocar
> o reader por `IContractAccessReader` (módulo Proposals) e a permissão por
> `proposals.offer.issue`/`contracts.manage`. Nenhuma entidade de outro módulo
> é referenciada (D-07).

## 4. Registro e uso

```csharp
// Api — registro dos handlers (DI)
builder.Services.AddScoped<IUserAccessReader, UserAccessReader>();
builder.Services.AddScoped<IJobRequisitionAccessReader, JobRequisitionAccessReader>();
builder.Services.AddScoped<IAuthorizationHandler, AuthenticatedScopeHandler>();
builder.Services.AddScoped<IAuthorizationHandler, TenantScopeHandler>();
builder.Services.AddScoped<IAuthorizationHandler, GlobalScopeHandler>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();
builder.Services.AddScoped<IAuthorizationHandler, ManageRequisitionHandler>();
builder.Services.AddScoped<IAuthorizationHandler, ManageContractHandler>();

builder.Services.AddSecurityPolicies();   // doc 03
```

```csharp
// Endpoint: gerenciar vaga (recurso = JobRequisitionId)
app.MapDelete("/api/recruitment/requisitions/{id:guid}",
        async (Guid id, IAuthorizationService auth, ClaimsPrincipal user,
               IJobRequisitionApplicationService service, CancellationToken ct) =>
    {
        var authz = await auth.AuthorizeAsync(user, new JobRequisitionId(id),
            SecurityPolicies.RequisitionManage);
        if (!authz.Succeeded)
            return Results.Forbid();   // 403; recurso de outro tenant também ⇒ 404 no handler de recurso

        return await service.CloseAsync(new JobRequisitionId(id), ct) is { IsSuccess: true }
            ? Results.NoContent()
            : Results.Problem(statusCode: 404);
    })
    .RequireAuthorization();
```

**Fluxo executado:** JwtBearer valida assinatura/exp → `AuthenticatedScope`
(banco: usuário/tenant/membership) → `TenantScope` → `Permission` (união de
roles + modo) → `ManageRequisition` (tenant do recurso). Qualquer etapa falha ⇒
403/404; nada depende dos claims.

## 5. Alternativa: Action Filter (quando usar)

O **handler** é a opção recomendada (composição de requirements, resource-based,
testável). Um Action Filter é aceitável apenas para guardas transversais sem
lógica de negócio, ex.:

```csharp
public sealed class TenantHeaderGuardFilter : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.HttpContext.Request.Headers.ContainsKey("X-Tenant-Id"))
            context.Result = new BadRequestObjectResult(new
                { error = "X-Tenant-Id não é aceito. O tenant é derivado do token." });
    }
}
```

## 6. Testes de unidade (amostra)

```csharp
[Fact]
public async Task User_with_requisition_manage_in_tenant_can_manage_own_requisition()
{
    var tenant = new TenantId(Guid.NewGuid());
    var access = new UserAccessContext(userId, tenant, true, true,
        new HashSet<string> { RoleCodes.HiringManager },
        new HashSet<string> { "recruitment.requisition.manage" }, AccessMode.Contracting);

    var reader = new FakeAccessReader(access, requisitionTenant: tenant);

    var result = await new ManageRequisitionHandler(reader, HttpContextHelper.WithItems(access))
        .AuthorizeAsync(new ClaimsPrincipal(), new ManageRequisitionRequirement(),
            new JobRequisitionId(Guid.NewGuid()));

    result.Succeeded.Should().BeTrue();
}

[Fact]
public async Task Requisition_of_another_tenant_is_denied()
{
    var access = new UserAccessContext(userId, tenantA, true, true, ...);

    var reader = new FakeAccessReader(access, requisitionTenant: tenantB);   // vaga do tenant B

    var result = await ...;   // tenant B != tenant A

    result.Succeeded.Should().BeFalse();   // mapeado para 404
}
```