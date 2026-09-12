namespace Worfair.Modules.Tenants.Application.ProvisionTenant;

using Worfair.BuildingBlocks.Domain.Errors;
using Worfair.BuildingBlocks.Domain.ValueObjects;
using Worfair.BuildingBlocks.Application.Cqrs;
using Worfair.Modules.Tenants.Application.Abstractions;
using Worfair.Modules.Tenants.Application.Dtos;
using Worfair.Modules.Tenants.Domain.Aggregates.Membership;
using Worfair.Modules.Tenants.Domain.Aggregates.Settings;
using Worfair.Modules.Tenants.Domain.Aggregates.Tenant;
using Worfair.Modules.Tenants.Domain.Abstractions;

public sealed class ProvisionTenantCommandHandler(
    ITenantRepository tenants,
    ITenantSettingsRepository settings,
    ITenantMembershipRepository memberships,
    ITenancyUnitOfWork unitOfWork)
    : ICommandHandler<ProvisionTenantCommand, Result<TenantDto>>
{
    public async Task<Result<TenantDto>> Handle(ProvisionTenantCommand command, CancellationToken cancellationToken)
    {
        var slug = command.Slug.Trim().ToLowerInvariant();

        if (await tenants.SlugExistsAsync(slug, cancellationToken).ConfigureAwait(false))
            return Result.Failure<TenantDto>(TenantErrors.SlugTaken);

        var tier = (TenantTier)command.Tier;

        var createResult = Tenant.Create(
            command.Name, slug, tier, command.Timezone, command.Locale);
        if (createResult.IsFailure)
            return Result.Failure<TenantDto>(createResult.Error!);

        var tenant = createResult.Value;
        await tenants.AddAsync(tenant, cancellationToken).ConfigureAwait(false);

        await settings.AddAsync(TenantSettings.DefaultFor(tenant.Key), cancellationToken).ConfigureAwait(false);

        if (command.OwnerUserId is { } ownerUserId && ownerUserId != Guid.Empty)
        {
            var membershipResult = TenantMembership.Add(ownerUserId, tenant.Key);
            if (membershipResult.IsSuccess)
                await memberships.AddAsync(membershipResult.Value, cancellationToken).ConfigureAwait(false);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new TenantDto(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            (int)tenant.Tier,
            (int)tenant.Status,
            tenant.Timezone,
            tenant.Locale,
            tenant.CreatedAtUtc);
    }
}
