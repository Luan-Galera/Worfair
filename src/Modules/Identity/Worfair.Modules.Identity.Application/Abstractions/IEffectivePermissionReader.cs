namespace Worfair.Modules.Identity.Application.Abstractions;

using Worfair.Modules.Identity.Domain.ValueObjects;

/// <summary>
/// Lê roles e permissões efetivas (união das roles — R-05) direto do banco.
/// Claims do token NUNCA decidem autorização (SEC-01).
/// </summary>
public interface IEffectivePermissionReader
{
    Task<IReadOnlyList<string>> GetRoleCodesAsync(
        Guid userId, TenantId? tenantId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetEffectivePermissionsAsync(
        Guid userId, TenantId? tenantId, CancellationToken cancellationToken = default);
}
