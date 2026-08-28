namespace Worfair.Modules.Identity.Application.Abstractions;

using System.Security.Cryptography;
using Worfair.BuildingBlocks.Application.Ports;
using Worfair.BuildingBlocks.Application.Security;
using Worfair.BuildingBlocks.Domain.Errors;
using Worfair.BuildingBlocks.Domain.ValueObjects;
using Worfair.Modules.Identity.Domain.Aggregates.RefreshToken;
using Worfair.Modules.Identity.Domain.Aggregates.User;
using Worfair.Modules.Identity.Domain.Abstractions;
using Worfair.Modules.Identity.Domain.Errors;

/// <summary>Utilitários de sessão (hash do refresh token — nunca armazenar em claro).</summary>
public static class SessionTokens
{
    public static string GenerateOpaque() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    public static string Hash(string token)
        => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
}

/// <summary>
/// Emissão/rotação da família de refresh tokens (docs/security/01 §5):
/// rotação com uso único; reuso detectado revoga a família inteira.
/// </summary>
public sealed class SessionIssuer(
    IUserRepository users,
    IEffectivePermissionReader permissions,
    IRefreshTokenRepository refreshTokens,
    Worfair.Modules.Tenants.Contracts.ITenancyReadContract tenancy,
    ITokenService tokenService,
    IIdentityUnitOfWork unitOfWork,
    IDateTimeProvider clock)
{
    /// <summary>Resolve roles/permissões/modo para o contexto alvo (SEC-01 §2 passos 3–4).</summary>
    public async Task<Result<ResolvedContext>> ResolveContextAsync(
        Guid userId, TenantId? tenantId, CancellationToken cancellationToken)
    {
        if (tenantId is { } tid)
        {
            if (!await tenancy.IsTenantActiveAsync(tid, cancellationToken).ConfigureAwait(false))
                return Result.Failure<ResolvedContext>(AuthErrors.TenantInactive);

            if (!await tenancy.HasActiveMembershipAsync(userId, tid, cancellationToken).ConfigureAwait(false))
                return Result.Failure<ResolvedContext>(AuthErrors.MembershipNotFound);
        }

        var roleCodes = await permissions.GetRoleCodesAsync(userId, tenantId, cancellationToken).ConfigureAwait(false);
        var effective = await permissions.GetEffectivePermissionsAsync(userId, tenantId, cancellationToken).ConfigureAwait(false);

        var modeResult = ModeResolver.DeriveContextMode(effective, roleCodes);
        if (modeResult.IsFailure)
            return Result.Failure<ResolvedContext>(modeResult.Error!);

        return new ResolvedContext(tenantId, roleCodes, effective, modeResult.Value);
    }

    /// <summary>
    /// Contexto default do login (docs/security/01 §2 passo 3): membership única
    /// ⇒ entra direto; múltiplas ⇒ exige escolha explícita (switch-tenant);
    /// nenhuma ⇒ apenas SUPER_ADMIN em contexto global.
    /// </summary>
    public async Task<Result<ResolvedContext>> ResolveDefaultContextAsync(
        Guid userId, CancellationToken cancellationToken)
    {
        var memberships = await tenancy.ListActiveMembershipsAcrossTenantsAsync(userId, cancellationToken)
            .ConfigureAwait(false);

        if (memberships.Count == 1)
            return await ResolveContextAsync(userId, new TenantId(memberships[0].TenantIdValue), cancellationToken)
                .ConfigureAwait(false);

        if (memberships.Count > 1)
            return Result.Failure<ResolvedContext>(AuthErrors.NoActiveMembership);

        return await ResolveContextAsync(userId, null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Nova família de tokens (login / switch).</summary>
    public async Task<Result<AuthTokens>> IssueNewFamilyAsync(
        User user, ResolvedContext context, CancellationToken cancellationToken)
    {
        var access = await tokenService.IssueAccessTokenAsync(
            user, context.TenantId, context.RoleCodes, context.Mode, cancellationToken).ConfigureAwait(false);

        var opaque = SessionTokens.GenerateOpaque();
        var refreshToken = RefreshToken.Issue(
            user.Id, context.TenantId, SessionTokens.Hash(opaque),
            TimeSpan.FromDays(RefreshTokenLifetimeDays), clock.UtcNow);

        await refreshTokens.AddAsync(refreshToken, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new AuthTokens(access, opaque, refreshToken.ExpiresAtUtc);
    }

    public const int RefreshTokenLifetimeDays = 7;

    /// <summary>
    /// Rotação (uso único) do refresh token com detecção de reuso:
    /// revogado reapresentado ⇒ revoga toda a família ativa.
    /// </summary>
    public async Task<Result<(User User, ResolvedContext Context, AuthTokens Tokens)>> RotateAsync(
        string opaqueRefreshToken, CancellationToken cancellationToken)
    {
        var hash = SessionTokens.Hash(opaqueRefreshToken);

        var stored = await refreshTokens.GetByHashAsync(hash, cancellationToken).ConfigureAwait(false);
        if (stored is null)
            return Result.Failure<(User, ResolvedContext, AuthTokens)>(AuthErrors.RefreshTokenInvalid);

        if (stored.WasRevoked)
        {
            // REUSO: família comprometida — derruba tudo (docs/security/01 §5).
            var family = await refreshTokens
                .ListActiveFamilyAsync(stored.UserId, stored.TenantId, cancellationToken)
                .ConfigureAwait(false);

            foreach (var token in family)
                token.Revoke(clock.UtcNow);

            return Result.Failure<(User, ResolvedContext, AuthTokens)>(AuthErrors.RefreshTokenInvalid);
        }

        if (!stored.IsActive(clock.UtcNow))
        {
            stored.Revoke(clock.UtcNow);
            return Result.Failure<(User, ResolvedContext, AuthTokens)>(AuthErrors.RefreshTokenInvalid);
        }

        var user = await users.GetByIdAsync(stored.UserId, cancellationToken).ConfigureAwait(false);
        if (user is null || user.EnsureCanAuthenticate().IsFailure)
            return Result.Failure<(User, ResolvedContext, AuthTokens)>(AuthErrors.RefreshTokenInvalid);

        var tenantId = stored.TenantId is { } tid ? (TenantId?)new TenantId(tid.Value) : null;

        var contextResult = await ResolveContextAsync(user.Id, tenantId, cancellationToken).ConfigureAwait(false);
        if (contextResult.IsFailure)
            return Result.Failure<(User, ResolvedContext, AuthTokens)>(contextResult.Error!);

        // Rotação: revoga o antigo apontando o substituto e emite o novo par.
        var newOpaque = SessionTokens.GenerateOpaque();
        var replacement = RefreshToken.Issue(
            user.Id,
            tenantId,
            SessionTokens.Hash(newOpaque),
            TimeSpan.FromDays(RefreshTokenLifetimeDays),
            clock.UtcNow);

        stored.Revoke(clock.UtcNow, replacement.TokenHash);
        await refreshTokens.AddAsync(replacement, cancellationToken).ConfigureAwait(false);

        var access = await tokenService.IssueAccessTokenAsync(
            user, contextResult.Value.TenantId, contextResult.Value.RoleCodes, contextResult.Value.Mode, cancellationToken)
            .ConfigureAwait(false);

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return (user, contextResult.Value, new AuthTokens(access, newOpaque, replacement.ExpiresAtUtc));
    }
}

public sealed record ResolvedContext(
    TenantId? TenantId,
    IReadOnlyList<string> RoleCodes,
    IReadOnlyList<string> EffectivePermissions,
    AccessMode Mode);
