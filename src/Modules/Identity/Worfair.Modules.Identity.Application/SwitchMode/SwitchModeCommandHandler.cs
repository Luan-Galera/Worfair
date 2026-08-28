namespace Worfair.Modules.Identity.Application.SwitchMode;

using Worfair.BuildingBlocks.Application.Cqrs;
using Worfair.BuildingBlocks.Application.Security;
using Worfair.BuildingBlocks.Domain.Errors;
using Worfair.BuildingBlocks.Domain.ValueObjects;
using Worfair.Modules.Tenants.Contracts;
using Worfair.Modules.Identity.Application.Abstractions;
using Worfair.Modules.Identity.Application.Dtos;
using Worfair.Modules.Identity.Domain.Abstractions;

public sealed class SwitchModeCommandHandler(
    IUserRepository users,
    SessionIssuer sessions,
    IEffectivePermissionReader permissions,
    ITenancyReadContract tenancy,
    ICurrentUser currentUser)
    : ICommandHandler<SwitchModeCommand, Result<AuthResponseDto>>
{
    public async Task<Result<AuthResponseDto>> Handle(SwitchModeCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
            return Result.Failure<AuthResponseDto>(AuthErrors.InvalidCredentials);

        if (!ModeResolver.TryParse(command.RequestedMode, out var requestedMode))
            return Result.Failure<AuthResponseDto>(AuthErrors.ModeUnavailable);

        var user = await users.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null || user.EnsureCanAuthenticate().IsFailure)
            return Result.Failure<AuthResponseDto>(AuthErrors.InvalidCredentials);

        // 1. Enumera os contextos disponíveis: atual + demais memberships ativas.
        var candidates = new List<TenantId?>();

        if (currentUser.TenantId is { } currentTenant)
        {
            candidates.Add(new TenantId(currentTenant));
        }

        foreach (var membership in await tenancy
            .ListActiveMembershipsAcrossTenantsAsync(userId, cancellationToken).ConfigureAwait(false))
        {
            var candidate = new TenantId(membership.TenantIdValue);
            if (candidates.All(c => c.GetValueOrDefault().Value != candidate.Value))
                candidates.Add(candidate);
        }

        if (!candidates.Contains(null) && ModeResolver.HasGlobal(
                await permissions.GetRoleCodesAsync(userId, null, cancellationToken).ConfigureAwait(false)))
        {
            candidates.Add(null); // contexto global disponível
        }

        // 2. Encontra um contexto cujo modo derivado = modo pedido.
        ResolvedContext? chosen = null;
        foreach (var tenantId in candidates)
        {
            var resolved = await sessions.ResolveContextAsync(userId, tenantId, cancellationToken).ConfigureAwait(false);
            if (resolved.IsFailure || resolved.Value.Mode != requestedMode)
                continue;

            chosen = resolved.Value;
            break;
        }

        if (chosen is null)
            return Result.Failure<AuthResponseDto>(AuthErrors.ModeUnavailable);

        var tokensResult = await sessions.IssueNewFamilyAsync(user, chosen, cancellationToken).ConfigureAwait(false);
        if (tokensResult.IsFailure)
            return Result.Failure<AuthResponseDto>(tokensResult.Error!);

        var tokens = tokensResult.Value;

        return new AuthResponseDto(
            user.Id,
            user.Email.Value,
            user.FullName,
            chosen.TenantId?.Value,
            ModeResolver.ToClaimValue(chosen.Mode),
            chosen.RoleCodes,
            tokens.AccessToken.Value,
            tokens.AccessToken.ExpiresAtUtc,
            tokens.RefreshToken,
            tokens.RefreshTokenExpiresAtUtc);
    }
}
