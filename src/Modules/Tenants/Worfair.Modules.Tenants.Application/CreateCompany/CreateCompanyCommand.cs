namespace Worfair.Modules.Tenants.Application.CreateCompany;

using FluentValidation;
using Worfair.BuildingBlocks.Application.Cqrs;
using Worfair.Modules.Tenants.Application.Dtos;

/// <summary>Cria empresa no tenant corrente (tenant vem do token — R-06).</summary>
public sealed record CreateCompanyCommand(
    string LegalName,
    string? TradeName,
    string Document,
    string? Email = null,
    string? Phone = null) : ICommand<Result<CompanyDto>>;

public sealed class CreateCompanyCommandValidator : AbstractValidator<CreateCompanyCommand>
{
    public CreateCompanyCommandValidator()
    {
        RuleFor(c => c.LegalName)
            .NotEmpty().WithErrorCode("Company.LegalNameRequired")
            .MaximumLength(200).WithErrorCode("Company.FieldTooLong");

        RuleFor(c => c.TradeName)
            .MaximumLength(200).WithErrorCode("Company.FieldTooLong");

        RuleFor(c => c.Document)
            .NotEmpty().Must(d => d.Count(char.IsDigit) is 11 or 14)
            .WithErrorCode("Company.DocumentInvalid");

        RuleFor(c => c.Email)
            .EmailAddress().When(c => !string.IsNullOrWhiteSpace(c.Email))
            .WithErrorCode("Company.EmailInvalid")
            .MaximumLength(320).WithErrorCode("Company.FieldTooLong");

        RuleFor(c => c.Phone)
            .MaximumLength(30).WithErrorCode("Company.FieldTooLong");
    }
}
