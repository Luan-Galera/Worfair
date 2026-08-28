namespace Worfair.Modules.Identity.Domain.Aggregates.RefreshToken;

using Worfair.BuildingBlocks.Domain.Entities;
using Worfair.BuildingBlocks.Domain.ValueObjects;
using Worfair.Modules.Identity.Domain.ValueObjects;

/// <summary>
/// Refresh token rotativo, uso único (docs/security/01 §5):
/// guarda SOMENTE hash; reuso de token revogado ⇒ revoga a família inteira.
/// tenant_id NULLável: sessão global do SUPER_ADMIN (R-04).
/// </summary>
public sealed class RefreshToken : Entity<Guid>
{
    public const int TokenHashMaxLength = 128;

    private RefreshToken()
    {
        // EF Core
    }

    private RefreshToken(
        Guid id, Guid userId, TenantId? tenantId, string tokenHash, DateTime expiresAtUtc, DateTime createdAtUtc)
    {
        Id = id;
        UserId = userId;
        TenantId = tenantId;
        TokenHash = tokenHash;
        ExpiresAtUtc = expiresAtUtc;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid UserId { get; private set; }

    public TenantId? TenantId { get; private set; }

    /// <summary>SHA-256 do token — nunca o valor em claro.</summary>
    public string TokenHash { get; private set; } = default!;

    public DateTime ExpiresAtUtc { get; private set; }

    public DateTime? RevokedAtUtc { get; private set; }

    public string? ReplacedByTokenHash { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public static RefreshToken Issue(
        Guid userId, TenantId? tenantId, string tokenHash, TimeSpan lifetime, DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;

        return new RefreshToken(Guid.NewGuid(), userId, tenantId, tokenHash, now.Add(lifetime), now);
    }

    public bool IsActive(DateTime utcNow) =>
        RevokedAtUtc is null && ExpiresAtUtc > utcNow;

    public bool WasRevoked => RevokedAtUtc is not null;

    public void Revoke(DateTime utcNow, string? replacedByTokenHash = null)
    {
        RevokedAtUtc = utcNow;
        ReplacedByTokenHash = replacedByTokenHash;
    }
}
