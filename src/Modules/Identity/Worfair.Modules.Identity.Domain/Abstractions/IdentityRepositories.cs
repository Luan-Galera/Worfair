namespace Worfair.Modules.Identity.Domain.Abstractions;

using Worfair.Modules.Identity.Domain.Aggregates.RefreshToken;
using Worfair.Modules.Identity.Domain.Aggregates.Role;
using Worfair.Modules.Identity.Domain.Aggregates.User;
using Worfair.Modules.Identity.Domain.Aggregates.UserRole;
using Worfair.Modules.Identity.Domain.ValueObjects;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);

    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken = default);

    Task<bool> HasAnyAsync(CancellationToken cancellationToken = default);

    Task AddAsync(User user, CancellationToken cancellationToken = default);
}

public interface IRoleRepository
{
    Task<Role?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Role>> GetByCodesAsync(IEnumerable<string> codes, CancellationToken cancellationToken = default);
}

public interface IUserRoleRepository
{
    Task<IReadOnlyList<string>> GetRoleCodesForTenantAsync(
        Guid userId, TenantId tenantId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetGlobalRoleCodesAsync(
        Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserRole>> ListForUserInTenantAsync(
        Guid userId, TenantId tenantId, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid userId, TenantId? tenantId, Guid roleId, CancellationToken cancellationToken = default);

    Task AddAsync(UserRole userRole, CancellationToken cancellationToken = default);

    void Remove(UserRole userRole);

    Task RemoveAsync(Guid userId, TenantId? tenantId, Guid roleId, CancellationToken cancellationToken = default);
}

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RefreshToken>> ListActiveFamilyAsync(
        Guid userId, TenantId? tenantId, CancellationToken cancellationToken = default);

    Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default);
}
