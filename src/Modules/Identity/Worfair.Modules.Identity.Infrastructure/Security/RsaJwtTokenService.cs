namespace Worfair.Modules.Identity.Infrastructure.Security;

using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using Worfair.BuildingBlocks.Application.Ports;
using Worfair.BuildingBlocks.Application.Security;
using Worfair.Modules.Identity.Application.Ports;
using Worfair.Modules.Identity.Domain.Aggregates.User;

/// <summary>
/// Emissão RS256 (docs/security/01): claims de CONTEXTO apenas —
/// sub, jti, iss/aud, iat/exp (15min), tenant_id?, roles[], mode, auth_time.
/// NUNCA permissões nem dados sensíveis.
/// </summary>
public sealed class RsaJwtTokenService(
    IOptions<JwtOptions> options,
    IDateTimeProvider clock) : ITokenService
{
    private readonly JwtOptions _options = options.Value;

    public Task<AccessToken> IssueAccessTokenAsync(
        User user,
        BuildingBlocks.Domain.ValueObjects.TenantId? tenantId,
        IReadOnlyList<string> roleCodes,
        AccessMode mode,
        CancellationToken cancellationToken = default)
    {
        using var rsa = RsaKeyLoader.LoadPem(_options.PrivateKeyPath);
        var key = new RsaSecurityKey(rsa) { KeyId = RsaKeyLoader.ComputeKeyId(rsa) };

        var issuedAt = clock.UtcNow;
        var expiresAt = issuedAt.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(CustomClaims.Mode, ModeResolver.ToClaimValue(mode)),
            new(CustomClaims.AuthTime, EpochTime.GetIntDate(issuedAt).ToString(), ClaimValueTypes.Integer64)
        };

        if (tenantId is { } tid)
            claims.Add(new Claim(CustomClaims.TenantId, tid.Value.ToString()));

        foreach (var role in roleCodes)
            claims.Add(new Claim(CustomClaims.Roles, role));

        var credentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: issuedAt,
            expires: expiresAt,
            signingCredentials: credentials);

        var handler = new JwtSecurityTokenHandler();
        return Task.FromResult(new AccessToken(handler.WriteToken(token), expiresAt));
    }
}

public static class CustomClaims
{
    public const string TenantId = "tenant_id";
    public const string Roles = "roles";
    public const string Mode = "mode";
    public const string AuthTime = "auth_time";
}
