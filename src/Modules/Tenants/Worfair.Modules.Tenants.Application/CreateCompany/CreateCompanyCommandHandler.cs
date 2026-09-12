namespace Worfair.Modules.Tenants.Application.CreateCompany;

using Worfair.BuildingBlocks.Application.Cqrs;
using Worfair.BuildingBlocks.Domain.Errors;
using Worfair.BuildingBlocks.Application.Security;
using Worfair.BuildingBlocks.Domain.Tenancy;
using Worfair.BuildingBlocks.Domain.ValueObjects;
using Worfair.Modules.Tenants.Application.Abstractions;
using Worfair.Modules.Tenants.Application.Dtos;
using Worfair.Modules.Tenants.Domain.Aggregates.Company;
using Worfair.Modules.Tenants.Domain.Abstractions;
using Worfair.Modules.Tenants.Domain.Errors;
using Worfair.Modules.Tenants.Domain.ValueObjects;

public sealed class CreateCompanyCommandHandler(
    ICompanyRepository companies,
    ITenancyUnitOfWork unitOfWork,
    ITenantProvider tenantProvider,
    ICurrentUser currentUser)
    : ICommandHandler<CreateCompanyCommand, Result<CompanyDto>>
{
    public async Task<Result<CompanyDto>> Handle(CreateCompanyCommand command, CancellationToken cancellationToken)
    {
        if (tenantProvider.TenantId is not { } tenantId)
            return Result.Failure<CompanyDto>(CompanyErrors.TenantRequired);

        var documentResult = Document.Create(command.Document);
        if (documentResult.IsFailure)
            return Result.Failure<CompanyDto>(documentResult.Error!);

        if (await companies.DocumentExistsAsync(documentResult.Value, cancellationToken).ConfigureAwait(false))
            return Result.Failure<CompanyDto>(CompanyErrors.DocumentDuplicated);

        // Vínculo automático conta-empresa × conta-usuário: quem cria é o dono.
        var createResult = Company.Create(
            command.LegalName, command.TradeName, documentResult.Value, command.Email, command.Phone,
            utcNow: null, ownerUserId: currentUser.UserId);
        if (createResult.IsFailure)
            return Result.Failure<CompanyDto>(createResult.Error!);

        var company = createResult.Value;
        await companies.AddAsync(company, cancellationToken).ConfigureAwait(false);

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new CompanyDto(
            company.Id.Value,
            company.LegalName,
            company.TradeName,
            company.Document.Value,
            company.Email,
            company.Phone,
            (int)company.Status,
            company.OwnerUserId);
    }
}
