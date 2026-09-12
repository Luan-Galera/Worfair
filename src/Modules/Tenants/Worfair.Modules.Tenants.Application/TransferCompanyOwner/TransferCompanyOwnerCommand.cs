namespace Worfair.Modules.Tenants.Application.TransferCompanyOwner;

using FluentValidation;
using Worfair.BuildingBlocks.Application.Cqrs;
using Worfair.BuildingBlocks.Domain.Errors;
using Worfair.BuildingBlocks.Domain.Tenancy;
using Worfair.BuildingBlocks.Domain.ValueObjects;
using Worfair.Modules.Tenants.Application.Abstractions;
using Worfair.Modules.Tenants.Application.Dtos;
using Worfair.Modules.Tenants.Domain.Aggregates.Company;
using Worfair.Modules.Tenants.Domain.Aggregates.Membership;
using Worfair.Modules.Tenants.Domain.Abstractions;
using Worfair.Modules.Tenants.Domain.Errors;

/// <summary>Transfere o vínculo dono×empresa para outro membro ativo do espaço.</summary>
public sealed record TransferCompanyOwnerCommand(Guid CompanyId, Guid NewOwnerUserId)
    : ICommand<Result<CompanyDto>>;

public sealed class TransferCompanyOwnerCommandValidator : AbstractValidator<TransferCompanyOwnerCommand>
{
    public TransferCompanyOwnerCommandValidator()
    {
        RuleFor(c => c.CompanyId).NotEmpty().WithErrorCode("Company.NotFound");
        RuleFor(c => c.NewOwnerUserId).NotEmpty().WithErrorCode("Company.OwnerRequired");
    }
}

public sealed class TransferCompanyOwnerCommandHandler(
    ICompanyRepository companies,
    ITenantMembershipRepository memberships,
    ITenancyUnitOfWork unitOfWork,
    ITenantProvider tenantProvider)
    : ICommandHandler<TransferCompanyOwnerCommand, Result<CompanyDto>>
{
    public async Task<Result<CompanyDto>> Handle(
        TransferCompanyOwnerCommand command, CancellationToken cancellationToken)
    {
        if (tenantProvider.TenantId is not { } tenantId)
            return Result.Failure<CompanyDto>(CompanyErrors.TenantRequired);

        var company = await companies.GetByTenantAndIdAsync(
            tenantId, new CompanyId(command.CompanyId), cancellationToken).ConfigureAwait(false);
        if (company is null)
            return Result.Failure<CompanyDto>(CompanyErrors.NotFound);

        var membership = await memberships.FindAsync(
            tenantId, command.NewOwnerUserId, cancellationToken).ConfigureAwait(false);
        if (membership is not { Status: MembershipStatus.Active })
            return Result.Failure<CompanyDto>(CompanyErrors.OwnerNotMember);

        var transfer = company.TransferOwner(command.NewOwnerUserId);
        if (transfer.IsFailure)
            return Result.Failure<CompanyDto>(transfer.Error!);

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
