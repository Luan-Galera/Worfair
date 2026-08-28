namespace Worfair.Modules.Tenants.Application.SetCompanyStatus;

using FluentValidation;
using Worfair.BuildingBlocks.Application.Cqrs;

public sealed record SetCompanyStatusCommand(Guid CompanyId, bool Active) : ICommand<Result>;

public sealed class SetCompanyStatusCommandValidator : AbstractValidator<SetCompanyStatusCommand>
{
    public SetCompanyStatusCommandValidator()
        => RuleFor(c => c.CompanyId).NotEmpty().WithErrorCode("Company.InvalidId");
}
