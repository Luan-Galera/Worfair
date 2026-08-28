namespace Worfair.Modules.Identity.Application.Ports;

using Worfair.BuildingBlocks.Application.Security;
using Worfair.Modules.Identity.Domain.ValueObjects;

/// <summary>Hash de senha (argon2id/bcrypt — docs/security/01 §2).</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string hash, string password);
}

/// <summary>Access token emitido (RS256, 15 min).</summary>
public sealed record AccessToken(string Value, DateTime ExpiresAtUtc);

/// <summary>Tokens de uma sessão emitida/rotacionada.</summary>
public sealed record AuthTokens(AccessToken AccessToken, string RefreshToken, DateTime RefreshTokenExpiresAtUtc);

/// <summary>
/// Emissão de JWT RS256 com claims de CONTEXTO (nunca permissões — SEC-01 §2.1):
/// sub, jti, tenant_id?, roles[], mode, auth_time.
/// </summary>
public interface ITokenService
{
    Task<AccessToken> IssueAccessTokenAsync(
        User user,
        TenantId? tenantId,
        IReadOnlyList<string> roleCodes,
        AccessMode mode,
        CancellationToken cancellationToken = default);
}
