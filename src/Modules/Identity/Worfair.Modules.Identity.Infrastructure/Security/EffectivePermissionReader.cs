namespace Worfair.Modules.Identity.Infrastructure.Security;

using Microsoft.EntityFrameworkCore;
using Worfair.Modules.Identity.Application.Abstractions;
using Worfair.Modules.Identity.Domain.Abstractions;
using Worfair.Modules.Identity.Domain.ValueObjects;
using Worfair.Modules.Identity.Infrastructure.Persistence;

/// <summary>
/// Roles atuais + permissões efetivas = união das roles (R-05) — lidos do banco
/// a cada uso. Em contexto global lê apenas linhas tenant_id NULL.
/// </summary>
public sealed class EffectivePermissionReader(IdentityDbContext db) : IEffectivePermissionReader
{
    public Task<IReadOnlyList<string>> GetRoleCodesAsync(
        Guid userId, TenantId? tenantId, CancellationToken cancellationToken = default)
        => ReadAsync(
            userId, tenantId, cancellationToken,
            userRoles => userRoles.Join(db.Roles,
                    ur => ur.RoleIdValue, r => r.Id, (ur, r) => r.Code));

    public Task<IReadOnlyList<string>> GetEffectivePermissionsAsync(
        Guid userId, TenantId? tenantId, CancellationToken cancellationToken = default)
        => ReadAsync(
            userId, tenantId, cancellationToken,
            userRoles => userRoles
                .Join(db.RolePermissions, ur => ur.RoleIdValue, rp => rp.RoleId, (ur, rp) => rp.PermissionId)
                .Join(db.Permissions, permissionId => permissionId, p => p.Id, (_, p) => p.Code));

    private async Task<IReadOnlyList<string>> ReadAsync(
        Guid userId, TenantId? tenantId,
        CancellationToken cancellationToken,
        Func<IQueryable<Worfair.Modules.Identity.Domain.Aggregates.UserRole.UserRole>, IQueryable<string>> project)
    {
        // Espelha o SQL de resolução dos docs (database/03 §4): contexto de
        // tenant lê apenas linhas do tenant; contexto global apenas tenant_id NULL.
        // O RLS do banco impõe o mesmo recorte (defesa final).
        var query = db.UserRoles.Where(ur =>
            ur.UserId == userId &&
            (tenantId != null ? ur.TenantId == tenantId.Value : ur.TenantId == null));

        var result = await project(query)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return result;
    }
}
