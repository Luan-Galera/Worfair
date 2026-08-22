# 03 — Políticas de Autorização (ASP.NET Core)

## 1. Modelo de autorização

Autorização é **por permissão efetiva**, calculada no servidor:

```
permissões efetivas (tenant) = ⋃ permissões de todas as roles do usuário no tenant
```

- Claims de `roles`/`mode` do JWT **nunca** decidem; servem apenas como cache
  de conveniência da UI.
- Múltiplas roles simultâneas (OWNER + RECRUITER + HIRING_MANAGER) entram
  naturalmente na união.
- `SUPER_ADMIN`: permissões globais (`platform.*`) fora de qualquer tenant.

## 2. Requirements (hierarquia)

| Requirement | Valida | Falha ⇒ |
| ----------- | ------ | ------- |
| `AuthenticatedScopeRequirement` | usuário ativo; se `tenant_id` no token: tenant ativo + membership ativa (banco) | 401/403 |
| `TenantScopeRequirement` | claim `tenant_id` presente (endpoint tenant-scoped) | 403 |
| `PermissionRequirement(code, mode?)` | permissão na união efetiva (banco) e, se `mode` exigido, modo disponível pelas permissões | 403 |
| `GlobalScopeRequirement` | contexto global (`tenant_id` ausente) + role `SUPER_ADMIN` no banco | 403 |
| `ResourceRequirement<T>` | recurso carregado pertence ao tenant do contexto (ex.: `ManageRequisitionRequirement`) | 404 |

## 3. Modos operacionais (Contratante × Prestador) — SEC-03

**Modos NÃO são roles e NÃO concedem nada.** São classificações derivadas das
permissões efetivas do usuário; o `mode` do token é revalidado contra elas.

| Modo | Derivado de permissões das roles | Uso |
| ---- | -------------------------------- | --- |
| `Contracting` | `CLIENT`, `OWNER`, `RECRUITER`, `HIRING_MANAGER` | contratar, gerir vagas/requisições, decidir propostas, emitir ofertas |
| `Provider` | `PROVIDER` | enviar propostas, executar serviços, receber pagamentos |
| `Global` | `SUPER_ADMIN` (permissões `platform.*`) | administração da plataforma |

```csharp
// BuildingBlocks.Application/Security/AccessMode.cs
public enum AccessMode { Global = 0, Contracting = 1, Provider = 2 }

// Mapeamento estático modo → prefixos de permissão (única fonte da verdade)
public static class AccessModeMapper
{
    public static readonly IReadOnlySet<string> ContractingPermissions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "tenants.settings", "tenants.members", "recruitment.", "jobs.",
            "proposals.decide", "proposals.offer", "financial.invoice"
        };

    public static readonly IReadOnlySet<string> ProviderPermissions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "proposals.submit", "jobs.project.apply", "financial.payout", "contracts.deliver"
        };

    public static AccessMode? Derive(IEnumerable<string> permissions)
    {
        var hasContracting = permissions.Any(p => ContractingPermissions.Any(p.StartsWith));
        var hasProvider = permissions.Any(p => ProviderPermissions.Any(p.StartsWith));
        return (hasContracting, hasProvider) switch
        {
            (true, true) => throw new InvalidOperationException("Modos exclusivos: um contexto de token define um único modo."),
            (true, false) => AccessMode.Contracting,
            (false, true) => AccessMode.Provider,
            _ => null
        };
    }
}
```

> Um usuário com `CLIENT` **e** `PROVIDER` tem os dois modos **disponíveis**, mas
> um token carrega **um** `mode` por vez (o modo do contexto ativo). A
> alternância de modo no frontend exige novo token (mesmo fluxo do switch), e o
> backend **sempre** verifica permissão efetiva + modo exigido — alternar a UI
> para "Modo Prestador" não concede nada que o backend não autorize.

## 4. Registro de policies

```csharp
// Api/Modules/SecurityPolicies.cs
public static class SecurityPolicies
{
    public const string RequisitionManage = "recruitment.requisition.manage";
    public const string CandidateAdvance = "recruitment.candidate.advance";
    public const string OfferIssue = "proposals.offer.issue";
    public const string ProposalSubmit = "proposals.submit";
    public const string InvoiceIssue = "financial.invoice.issue";
    public const string PayoutManage = "financial.payout.manage";
    public const string PlatformManage = "platform.tenants.manage";
    public const string AuditRead = "audit.read";
}

public static IServiceCollection AddSecurityPolicies(this IServiceCollection services)
{
    services.AddAuthorization(options =>
    {
        // Contratante: gerir requisição exige permissão + modo Contratante
        options.AddPolicy(SecurityPolicies.RequisitionManage, p => p
            .AddRequirements(new AuthenticatedScopeRequirement())
            .AddRequirements(new TenantScopeRequirement())
            .AddRequirements(new PermissionRequirement("recruitment.requisition.manage", AccessMode.Contracting)));

        // Prestador: submeter proposta exige permissão + modo Prestador
        options.AddPolicy(SecurityPolicies.ProposalSubmit, p => p
            .AddRequirements(new AuthenticatedScopeRequirement())
            .AddRequirements(new TenantScopeRequirement())
            .AddRequirements(new PermissionRequirement("proposals.submit", AccessMode.Provider)));

        // Global: somente SUPER_ADMIN em contexto global
        options.AddPolicy(SecurityPolicies.PlatformManage, p => p
            .AddRequirements(new AuthenticatedScopeRequirement())
            .AddRequirements(new GlobalScopeRequirement())
            .AddRequirements(new PermissionRequirement("platform.tenants.manage", AccessMode.Global)));

        // Auditoria: leitura (contexto de tenant) — nunca escrita via API
        options.AddPolicy(SecurityPolicies.AuditRead, p => p
            .AddRequirements(new AuthenticatedScopeRequirement())
            .AddRequirements(new TenantScopeRequirement())
            .AddRequirements(new PermissionRequirement("audit.read", AccessMode.Contracting)));
    });

    return services;
}
```

## 5. Uso nos endpoints

```csharp
// Minimal API do módulo Recruitment
app.MapPost("/api/recruitment/requisitions/{id:guid}/publish",
        async (Guid id, IAuthorizationService auth, ClaimsPrincipal user,
               IJobRequisitionApplicationService service, CancellationToken ct) =>
    {
        var authz = await auth.AuthorizeAsync(user, new JobRequisitionId(id),
            SecurityPolicies.RequisitionManage);
        if (!authz.Succeeded)
            return Results.Forbid();

        var result = await service.PublishAsync(new JobRequisitionId(id), ct);
        return result.IsSuccess ? Results.NoContent() : Results.Problem(...);
    })
    .RequireAuthorization();   // qualquer usuário autenticado passa; a política decide
```

> A política com resource (`AuthorizeAsync(user, resource, policy)`) dispara os
> handlers resource-based (doc 04) — onde o tenant do recurso é comparado ao
> tenant do contexto.

## 6. Matriz exemplo por módulo

| Endpoint | Policy | Modo | Permissões efetivas |
| -------- | ------ | ---- | ------------------- |
| POST `/recruitment/requisitions` | `RequisitionManage` | Contracting | `recruitment.requisition.manage` |
| POST `/recruitment/candidates/{id}/advance` | `CandidateAdvance` | Contracting | `recruitment.candidate.advance` |
| POST `/proposals` | `ProposalSubmit` | Provider | `proposals.submit` |
| POST `/proposals/offers` | `OfferIssue` | Contracting | `proposals.offer.issue` |
| POST `/financial/invoices` | `InvoiceIssue` | Contracting | `financial.invoice.issue` |
| POST `/financial/payouts/request` | `PayoutManage` | Provider | `financial.payout.manage` |
| GET `/platform/tenants` | `PlatformManage` | Global | `platform.tenants.manage` |

**Cobertura de ataques:**

| Ataque | Como é bloqueado |
| ------ | ---------------- |
| Forjar claims (`roles`, `mode`, `tenant_id`) | Assinatura RS256; autorização ignora claims e relê o banco |
| Alternar modo no frontend para ganhar acesso | `mode` revalidado contra permissões efetivas + policy exige modo + permissão |
| Reutilizar token de outro tenant | `tenant_id` do token ≠ recurso ⇒ 404; RLS ⇒ vazio |
| Role revogada ainda "no token" | Permissões lidas do banco a cada request |
| Replay de token | TTL 15 min; refresh rotativo com detecção de reuso |
| User desativado / tenant suspenso | `AuthenticatedScopeRequirement` valida status no banco |

## 7. Mapeamento de falhas (middleware)

- 401: token ausente/inválido/expirado.
- 403: autenticado, porém sem permissão efetiva (ou modo incompatível).
- 404: recurso inexistente **ou** de outro tenant (indistinguível de propósito).