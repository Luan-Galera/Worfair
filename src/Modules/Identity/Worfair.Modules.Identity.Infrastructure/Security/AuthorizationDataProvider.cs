namespace Worfair.Modules.Identity.Infrastructure.Security;

using Microsoft.EntityFrameworkCore;
using Worfair.BuildingBlocks.Application.Security;
using Worfair.Modules.Tenants.Contracts;
using Worfair.Modules.Identity.Domain.Abstractions;
using Worfair.Modules.Identity.Infrastructure.Persistence;

/// <summary>
/// Revalidação OBRIGATÓRIA no servidor (SEC-01 §4): usuário ativo, tenant ativo,
/// membership ativa, roles ATUAIS e permissões efetivas — tudo do BANCO.
/// Recursos sensíveis nunca usam cache.
/// </summary>
public sealed class AuthorizationDataProvider(
    IdentityDbContext db,
    IEffectivePermissionReader permissionReader,
    ITenancyReadContract tenancy) : IAuthorizationDataProvider
{
    public async Task<AuthorizationSnapshot?> GetSnapshotAsync(
        Guid userId, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            .ConfigureAwait(false);

        if (user is null)
            return null;

        AccountStatus? tenantStatus = null;
        AccountStatus? membershipStatus = null;

        if (tenantId is { } tid)
        {
            var tenantActive = await tenancy.IsTenantActiveAsync(
                new BuildingBlocks.Domain.ValueObjects.TenantId(tid), cancellationToken).ConfigureAwait(false);

            tenantStatus = tenantActive ? AccountStatus.Active : AccountStatus.Suspended;

            var memberActive = await tenancy.HasActiveMembershipAsync(
                userId, new BuildingBlocks.Domain.ValueObjects.TenantId(tid), cancellationToken).ConfigureAwait(false);

            membershipStatus = memberActive ? AccountStatus.Active : AccountStatus.Disabled;
        }

        var roles = await permissionReader
            .GetRoleCodesAsync(userId, tenantId is { } t ? new TenantId(t) : null, cancellationToken)
            .ConfigureAwait(false);

        var effectivePermissions = await permissionReader
            .GetEffectivePermissionsAsync(userId, tenantId is { } tt ? new TenantId(tt) : null, cancellationToken)
            .ConfigureAwait(false);

        return new AuthorizationSnapshot(
            userId,
            MapUserStatus(user.Status),
            tenantId,
            tenantStatus,
            membershipStatus,
            roles,
            effectivePermissions);
    }

    private static AccountStatus MapUserStatus(Domain.Aggregates.User.UserStatus status) => status switch
    {
        Domain.Aggregates.User.UserStatus.Active => AccountStatus.Active,
        Domain.Aggregates.User.UserStatus.Locked => AccountStatus.Locked,
        _ => AccountStatus.Disabled
    };
}
