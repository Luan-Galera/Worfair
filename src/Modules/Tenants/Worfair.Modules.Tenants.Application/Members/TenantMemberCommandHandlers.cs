namespace Worfair.Modules.Tenants.Application.Members;

using Worfair.BuildingBlocks.Application.Cqrs;
using Worfair.BuildingBlocks.Domain.Errors;
using Worfair.BuildingBlocks.Domain.Tenancy;
using Worfair.Modules.Tenants.Application.Abstractions;
using Worfair.Modules.Tenants.Domain.Abstractions;
using Worfair.Modules.Tenants.Domain.Aggregates.Membership;
using Worfair.Modules.Tenants.Domain.Errors;

public sealed class AddTenantMemberCommandHandler(
    ITenantMembershipRepository memberships,
    ITenancyUnitOfWork unitOfWork,
    ITenantProvider tenantProvider)
    : ICommandHandler<AddTenantMemberCommand, Result>
{
    public async Task<Result> Handle(AddTenantMemberCommand command, CancellationToken cancellationToken)
    {
        if (tenantProvider.TenantId is not { } tenantId)
            return Result.Failure(MembershipErrors.TenantRequired);

        var existing = await memberships
            .FindAsync(tenantId, command.TargetUserId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
            return Result.Failure(MembershipErrors.AlreadyMember);

        var createResult = TenantMembership.Add(command.TargetUserId);
        if (createResult.IsFailure)
            return createResult;

        await memberships.AddAsync(createResult.Value, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}

public sealed class SetTenantMemberStatusCommandHandler(
    ITenantMembershipRepository memberships,
    ITenancyUnitOfWork unitOfWork,
    ITenantProvider tenantProvider)
    : ICommandHandler<SetTenantMemberStatusCommand, Result>
{
    public async Task<Result> Handle(SetTenantMemberStatusCommand command, CancellationToken cancellationToken)
    {
        if (tenantProvider.TenantId is not { } tenantId)
            return Result.Failure(MembershipErrors.TenantRequired);

        var membership = await memberships
            .FindAsync(tenantId, command.TargetUserId, cancellationToken)
            .ConfigureAwait(false);

        if (membership is null)
            return Result.Failure(MembershipErrors.NotFound);

        var result = command.Active ? membership.Activate() : membership.Disable();
        if (result.IsFailure)
            return result;

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }
}
