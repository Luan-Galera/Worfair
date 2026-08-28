namespace Worfair.Api.Authorization;

using Microsoft.AspNetCore.Authorization;
using Worfair.BuildingBlocks.Application.Security;

/// <summary>
/// Handlers resource-based: revalidam TUDO no banco a cada request (SEC-01 §4).
/// Claims de roles/mode do token são apenas informativos — nunca decisivos.
/// </summary>
public sealed class AuthenticatedScopeHandler(IAuthorizationDataProvider dataProvider)
    : AuthorizationHandler<AuthenticatedScopeRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AuthenticatedScopeRequirement requirement)
    {
        var snapshot = await SnapshotResolver.ResolveAsync(dataProvider, context).ConfigureAwait(false);
        if (snapshot is null)
        {
            context.Fail();
            return;
        }

        // 1. usuário ativo
        if (snapshot.UserStatus != AccountStatus.Active)
        {
            context.Fail();
            return;
        }

        // 2/3. tenant ativo + membership ativa (quando contexto de tenant)
        if (snapshot.TenantId is not null &&
            (snapshot.TenantStatus != AccountStatus.Active || snapshot.MembershipStatus != AccountStatus.Active))
        {
            context.Fail();
            return;
        }

        context.Succeed(requirement);
    }
}

/// <summary>Endpoint tenant-scoped: claim tenant_id PRESENTE (SEC-02).</summary>
public sealed class TenantScopeHandler : AuthorizationHandler<TenantScopeRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, TenantScopeRequirement requirement)
    {
        if (Guid.TryParse(context.User.FindFirst("tenant_id")?.Value, out _))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}

/// <summary>Contexto global: SEM tenant_id + SUPER_ADMIN confirmado no banco.</summary>
public sealed class GlobalScopeHandler(IAuthorizationDataProvider dataProvider)
    : AuthorizationHandler<GlobalScopeRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, GlobalScopeRequirement requirement)
    {
        if (Guid.TryParse(context.User.FindFirst("tenant_id")?.Value, out _))
            return;

        var snapshot = await SnapshotResolver.ResolveAsync(dataProvider, context).ConfigureAwait(false);
        if (snapshot is { IsGlobalContext: true } && snapshot.Roles.Contains("SUPER_ADMIN", StringComparer.Ordinal))
            context.Succeed(requirement);
    }
}

/// <summary>Permissão efetiva (união das roles, do BANCO) + modo revalidado (SEC-03).</summary>
public sealed class PermissionHandler(IAuthorizationDataProvider dataProvider)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var sub = context.User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(sub, out var userId))
            return;

        Guid? tenantId = Guid.TryParse(context.User.FindFirst("tenant_id")?.Value, out var t) ? t : null;

        var snapshot = await dataProvider.GetSnapshotAsync(userId, tenantId).ConfigureAwait(false);
        if (snapshot is null)
            return;

        // Permissão efetiva na união lida do banco.
        if (!snapshot.EffectivePermissions.Contains(requirement.Code, StringComparer.OrdinalIgnoreCase))
            return;

        // Modo exigido: derivado das permissões ATUAIS — claims nunca decidem.
        if (requirement.Mode is { } requiredMode)
        {
            switch (requiredMode)
            {
                case AccessMode.Global:
                    if (!(snapshot.IsGlobalContext && snapshot.Roles.Contains("SUPER_ADMIN", StringComparer.Ordinal)))
                        return;
                    break;

                case AccessMode.Contracting:
                case AccessMode.Provider:
                    try
                    {
                        if (AccessModeMapper.Derive(snapshot.EffectivePermissions) != requiredMode)
                            return;
                    }
                    catch (InvalidOperationException)
                    {
                        return; // permissões exclusivas conflitantes — falha segura
                    }

                    if (snapshot.IsGlobalContext)
                        return; // modo de tenant exige contexto de tenant
                    break;
            }
        }

        context.Succeed(requirement);
    }
}

internal static class SnapshotResolver
{
    public static async Task<AuthorizationSnapshot?> ResolveAsync(
        IAuthorizationDataProvider dataProvider, AuthorizationHandlerContext context)
    {
        var sub = context.User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(sub, out var userId))
            return null;

        Guid? tenantId = Guid.TryParse(context.User.FindFirst("tenant_id")?.Value, out var t) ? t : null;

        return await dataProvider.GetSnapshotAsync(userId, tenantId).ConfigureAwait(false);
    }
}
