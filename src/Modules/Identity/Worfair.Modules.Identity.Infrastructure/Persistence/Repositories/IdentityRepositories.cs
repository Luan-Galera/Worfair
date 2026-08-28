namespace Worfair.Modules.Identity.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using Worfair.BuildingBlocks.Domain.ValueObjects;
using Worfair.Modules.Identity.Domain.Aggregates.RefreshToken;
using Worfair.Modules.Identity.Domain.Aggregates.Role;
using Worfair.Modules.Identity.Domain.Aggregates.User;
using Worfair.Modules.Identity.Domain.Aggregates.UserRole;
using Worfair.Modules.Identity.Domain.Abstractions;

public sealed class UserRepository(IdentityDbContext db) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default)
    {
        // Comparação via VO: o EF aplica o value converter nos dois lados
        // (u.Email.Value não é traduzível — ver índice único lower(email)).
        var email = Email.Create(normalizedEmail).Value;
        return db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
    }

    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken = default)
    {
        var email = Email.Create(normalizedEmail).Value;
        return db.Users.AnyAsync(u => u.Email == email, cancellationToken);
    }

    public Task<bool> HasAnyAsync(CancellationToken cancellationToken = default) =>
        db.Users.AnyAsync(cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken = default) =>
        await db.Users.AddAsync(user, cancellationToken).ConfigureAwait(false);
}

public sealed class RoleRepository(IdentityDbContext db) : IRoleRepository
{
    public Task<Role?> GetByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        db.Roles.FirstOrDefaultAsync(r => r.Code == code.ToUpperInvariant(), cancellationToken);

    public async Task<IReadOnlyList<Role>> GetByCodesAsync(
        IEnumerable<string> codes, CancellationToken cancellationToken = default)
    {
        var normalized = codes.Select(c => c.ToUpperInvariant()).ToList();
        return await db.Roles.Where(r => normalized.Contains(r.Code)).ToListAsync(cancellationToken).ConfigureAwait(false);
    }
}

public sealed class UserRoleRepository(IdentityDbContext db) : IUserRoleRepository
{
    public async Task<IReadOnlyList<string>> GetRoleCodesForTenantAsync(
        Guid userId, TenantId tenantId, CancellationToken cancellationToken = default)
    {
        // RLS do banco garante visibilidade apenas das linhas do tenant corrente.
        return await db.UserRoles
            .Where(ur => ur.UserId == userId && ur.TenantId == tenantId.Value)
            .Join(db.Roles, ur => ur.RoleIdValue, r => r.Id, (ur, r) => r.Code)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> GetGlobalRoleCodesAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        return await db.UserRoles
            .Where(ur => ur.UserId == userId && ur.TenantId == null)
            .Join(db.Roles, ur => ur.RoleIdValue, r => r.Id, (ur, r) => r.Code)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<IReadOnlyList<UserRole>> ListForUserInTenantAsync(
        Guid userId, TenantId tenantId, CancellationToken cancellationToken = default) =>
        ListImpl(userId, tenantId, cancellationToken);

    private async Task<IReadOnlyList<UserRole>> ListImpl(
        Guid userId, TenantId? tenantId, CancellationToken ct)
    {
        var query = db.UserRoles.Where(ur => ur.UserId == userId);
        query = tenantId is null
            ? query.Where(ur => ur.TenantId == null)
            : query.Where(ur => ur.TenantId == tenantId.Value);

        var list = await query.ToListAsync(ct).ConfigureAwait(false);
        return list;
    }

    public Task<bool> ExistsAsync(
        Guid userId, TenantId? tenantId, Guid roleId, CancellationToken cancellationToken = default) =>
        (tenantId is null
            ? db.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.TenantId == null && ur.RoleIdValue == roleId, cancellationToken)
            : db.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.TenantId == tenantId.Value && ur.RoleIdValue == roleId, cancellationToken));

    public async Task AddAsync(UserRole userRole, CancellationToken cancellationToken = default) =>
        await db.UserRoles.AddAsync(userRole, cancellationToken).ConfigureAwait(false);

    public void Remove(UserRole userRole) => db.UserRoles.Remove(userRole);

    public async Task RemoveAsync(Guid userId, TenantId? tenantId, Guid roleId, CancellationToken cancellationToken = default)
    {
        var query = db.UserRoles.Where(ur => ur.UserId == userId && ur.RoleIdValue == roleId);
        query = tenantId is null
            ? query.Where(ur => ur.TenantId == null)
            : query.Where(ur => ur.TenantId == tenantId.Value);

        var found = await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (found is not null)
            db.UserRoles.Remove(found);
    }
}

public sealed class RefreshTokenRepository(IdentityDbContext db) : IRefreshTokenRepository
{
    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        db.RefreshTokens.FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyList<RefreshToken>> ListActiveFamilyAsync(
        Guid userId, TenantId? tenantId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var query = db.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAtUtc == null && rt.ExpiresAtUtc > now);

        query = tenantId is { } tid
            ? query.Where(rt => rt.TenantId == tid.Value)
            : query.Where(rt => rt.TenantId == null);

        var list = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
        return list;
    }

    public async Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default) =>
        await db.RefreshTokens.AddAsync(refreshToken, cancellationToken).ConfigureAwait(false);
}
