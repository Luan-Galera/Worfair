namespace Worfair.BuildingBlocks.Application.Security;

using Microsoft.AspNetCore.Authorization;

/// <summary>Usuário autenticado + ativo + (quando aplicável) tenant e membership ativos no banco.</summary>
public sealed class AuthenticatedScopeRequirement : IAuthorizationRequirement;

/// <summary>Endpoint tenant-scoped: exige claim tenant_id presente no contexto.</summary>
public sealed class TenantScopeRequirement : IAuthorizationRequirement;

/// <summary>Contexto global: tenant ausente + SUPER_ADMIN confirmado no banco.</summary>
public sealed class GlobalScopeRequirement : IAuthorizationRequirement;

/// <summary>Permissão efetiva (união das roles, lida do banco) + modo opcional exigido.</summary>
public sealed class PermissionRequirement(string code, AccessMode? mode = null) : IAuthorizationRequirement
{
    public string Code { get; } = code;

    public AccessMode? Mode { get; } = mode;
}
