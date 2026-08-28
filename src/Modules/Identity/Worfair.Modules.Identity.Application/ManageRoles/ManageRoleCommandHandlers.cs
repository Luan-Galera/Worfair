namespace Worfair.Modules.Identity.Application.ManageRoles;

using Worfair.BuildingBlocks.Application.Cqrs;
using Worfair.BuildingBlocks.Application.Security;
using Worfair.BuildingBlocks.Domain.Errors;
using Worfair.BuildingBlocks.Domain.ValueObjects;
using Worfair.Modules.Tenants.Contracts;
using Worfair.Modules.Identity.Application.Abstractions;
using Worfair.Modules.Identity.Domain.Aggregates.UserRole;
using Worfair.Modules.Identity.Domain.Abstractions;
using Worfair.Modules.Identity.Domain.Errors;

public sealed class AssignTenantRoleCommandHandler(
    IRoleRepository roles,
    IUserRoleRepository userRoles,
    ITenancyReadContract tenancy,
    IIdentityUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : ICommandHandler<AssignTenantRoleCommand, Result>
{
    public async Task<Result> Handle(AssignTenantRoleCommand command, CancellationToken cancellationToken)
    {
        var role = await roles.GetByCodeAsync(command.RoleCode, cancellationToken).ConfigureAwait(false);
        if (role is null)
            return Result.Failure(AuthErrors.RoleNotFound);

        // Roles globais NUNCA por este endpoint (exigem contexto de plataforma).
        if (role.IsGlobal || currentUser.TenantId is not { } tenantGuid)
            return Result.Failure(AuthErrors.GlobalRoleRequiresPlatformScope);

        var tenantId = new TenantId(tenantGuid);

        // R-05: role de tenant exige membership ATIVA do alvo.
        if (!await tenancy.HasActiveMembershipAsync(command.TargetUserId, tenantId, cancellationToken).ConfigureAwait(false))
            return Result.Failure(AuthErrors.TenantRoleRequiresMembership);

        if (await userRoles
            .ExistsAsync(command.TargetUserId, tenantId, role.Id, cancellationToken)
            .ConfigureAwait(false))
            return Result.Failure(AuthErrors.RoleAlreadyGranted);

        var grant = UserRole.Grant(command.TargetUserId, role, tenantId, grantedBy: currentUser.UserId);
        if (grant.IsFailure)
            return grant;

        await userRoles.AddAsync(grant.Value, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}

public sealed class RemoveTenantRoleCommandHandler(
    IRoleRepository roles,
    IUserRoleRepository userRoles,
    ITenancyReadContract tenancy,
    IIdentityUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : ICommandHandler<RemoveTenantRoleCommand, Result>
{
    public async Task<Result> Handle(RemoveTenantRoleCommand command, CancellationToken cancellationToken)
    {
        var role = await roles.GetByCodeAsync(command.RoleCode, cancellationToken).ConfigureAwait(false);
        if (role is null)
            return Result.Failure(AuthErrors.RoleNotFound);

        if (role.IsGlobal || currentUser.TenantId is not { } tenantGuid)
            return Result.Failure(AuthErrors.GlobalRoleRequiresPlatformScope);

        var tenantId = new TenantId(tenantGuid);
        if (!await tenancy.HasActiveMembershipAsync(command.TargetUserId, tenantId, cancellationToken).ConfigureAwait(false))
            return Result.Failure(AuthErrors.TenantRoleRequiresMembership);

        await userRoles.RemoveAsync(command.TargetUserId, tenantId, role.Id, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
