namespace Worfair.BuildingBlocks.Application.Security;

/// <summary>Status normalizado lido do banco a cada autorização (SEC-01 §4).</summary>
public enum AccountStatus
{
    Unknown = 0,
    Active = 1,
    Locked = 2,
    Disabled = 3,
    Suspended = 4,
    Cancelled = 5,
    Invited = 6
}

/// <summary>
/// Snapshot de revalidação (nunca vem do token): usuário, tenant, membership,
/// roles atuais e união de permissões efetivas.
/// </summary>
public sealed record AuthorizationSnapshot(
    Guid UserId,
    AccountStatus UserStatus,
    Guid? TenantId,
    AccountStatus? TenantStatus,
    AccountStatus? MembershipStatus,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> EffectivePermissions)
{
    public bool IsGlobalContext => TenantId is null;
}

/// <summary>
/// Porta implementada pelo módulo Identity: relê o estado real no banco a cada
/// requisição — claims do JWT nunca decidem (docs/security/01).
/// </summary>
public interface IAuthorizationDataProvider
{
    Task<AuthorizationSnapshot?> GetSnapshotAsync(
        Guid userId, Guid? tenantId, CancellationToken cancellationToken = default);
}
