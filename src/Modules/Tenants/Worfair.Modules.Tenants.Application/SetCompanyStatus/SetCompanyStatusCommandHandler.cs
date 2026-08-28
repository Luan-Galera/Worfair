namespace Worfair.Modules.Tenants.Application.SetCompanyStatus;

using Worfair.BuildingBlocks.Application.Cqrs;
using Worfair.BuildingBlocks.Domain.Errors;
using Worfair.BuildingBlocks.Domain.Tenancy;
using Worfair.BuildingBlocks.Domain.ValueObjects;
using Worfair.Modules.Tenants.Application.Abstractions;
using Worfair.Modules.Tenants.Domain.Abstractions;
using Worfair.Modules.Tenants.Domain.Aggregates.Company;
using Worfair.Modules.Tenants.Domain.Errors;

public sealed class SetCompanyStatusCommandHandler(
    ICompanyRepository companies,
    ITenancyUnitOfWork unitOfWork,
    ITenantProvider tenantProvider)
    : ICommandHandler<SetCompanyStatusCommand, Result>
{
    public async Task<Result> Handle(SetCompanyStatusCommand command, CancellationToken cancellationToken)
    {
        if (tenantProvider.TenantId is not { } tenantId)
            return Result.Failure(CompanyErrors.TenantRequired);

        var company = await companies
            .GetByTenantAndIdAsync(tenantId, new CompanyId(command.CompanyId), cancellationToken)
            .ConfigureAwait(false);

        if (company is null)
            return Result.Failure(CompanyErrors.NotFound); // 404 — indistinguível (SEC-02)

        var result = command.Active ? company.Activate() : company.Deactivate();
        if (result.IsFailure)
            return result;

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }
}
