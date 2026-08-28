namespace Worfair.Modules.Identity.Application.GetMe;

using Worfair.BuildingBlocks.Application.Cqrs;
using Worfair.BuildingBlocks.Application.Security;
using Worfair.Modules.Tenants.Contracts;
using Worfair.Modules.Identity.Application.Abstractions;
using Worfair.Modules.Identity.Application.Dtos;
using Worfair.Modules.Identity.Domain.Abstractions;
using Worfair.Modules.Identity.Domain.ValueObjects;

/// <summary>
/// Espelho do contexto (FE-04): a UI exibe o que o SERVIDOR calcula — roles,
/// permissões efetivas e modos disponíveis lidos do banco neste instante.
/// </summary>
public sealed record GetMeQuery : IQuery<Result<MeDto>>;

public sealed class GetMeQueryHandler(
    IUserRepository users,
    IEffectivePermissionReader permissions,
    ITenancyReadContract tenancy,
    ICurrentUser currentUser)
    : IQueryHandler<GetMeQuery, Result<MeDto>>
{
    public async Task<Result<MeDto>> Handle(GetMeQuery query, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
            return Result.Failure<MeDto>(AuthErrors.InvalidCredentials);

        var user = await users.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null || user.EnsureCanAuthenticate().IsFailure)
            return Result.Failure<MeDto>(AuthErrors.InvalidCredentials);

        var tenantId = currentUser.TenantId is { } t ? (TenantId?)new TenantId(t) : null;

        var roleCodes = await permissions.GetRoleCodesAsync(userId, tenantId, cancellationToken).ConfigureAwait(false);
        var effective = await permissions
            .GetEffectivePermissionsAsync(userId, tenantId, cancellationToken).ConfigureAwait(false);

        // Modos DISPONÍVEIS (união entre todos os contextos do usuário).
        var available = new List<string>();

        foreach (var membership in await tenancy
            .ListActiveMembershipsAcrossTenantsAsync(userId, cancellationToken).ConfigureAwait(false))
        {
            var mPerms = await permissions
                .GetEffectivePermissionsAsync(userId, new TenantId(membership.TenantIdValue), cancellationToken)
                .ConfigureAwait(false);

            if (ModeResolver.HasContracting(mPerms) && !available.Contains(ModeResolver.ContractingMode))
                available.Add(ModeResolver.ContractingMode);
            if (ModeResolver.HasProvider(mPerms) && !available.Contains(ModeResolver.ProviderMode))
                available.Add(ModeResolver.ProviderMode);
        }

        if (ModeResolver.HasGlobal(roleCodes))
            available.Insert(0, ModeResolver.GlobalMode);

        var currentModeResult = ModeResolver.DeriveContextMode(effective, roleCodes);
        var currentMode = currentModeResult.IsSuccess
            ? ModeResolver.ToClaimValue(currentModeResult.Value)
            : null;

        return new MeDto(
            user.Id,
            user.Email.Value,
            user.FullName,
            (int)user.Status,
            tenantId?.Value,
            currentMode,
            roleCodes,
            effective,
            available,
            (await tenancy.ListActiveMembershipsAcrossTenantsAsync(userId, cancellationToken).ConfigureAwait(false))
                .Select(m => new MembershipDto(m.TenantIdValue, m.TenantName, m.Status, m.JoinedAtUtc))
                .ToList());
    }
}
