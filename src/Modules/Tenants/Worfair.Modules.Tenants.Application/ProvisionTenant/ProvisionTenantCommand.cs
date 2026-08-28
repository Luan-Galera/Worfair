namespace Worfair.Modules.Tenants.Application.ProvisionTenant;

using FluentValidation;
using Worfair.BuildingBlocks.Application.Cqrs;
using Worfair.Modules.Tenants.Application.Dtos;

/// <summary>
/// Provisiona um novo tenant (contexto GLOBAL — policy platform.tenants.manage).
/// Owner opcional já entra como membro ativo.
/// </summary>
public sealed record ProvisionTenantCommand(
    string Name,
    string Slug,
    int Tier = 1,
    string? Timezone = null,
    string? Locale = null,
    Guid? OwnerUserId = null) : ICommand<Result<TenantDto>>;

public sealed class ProvisionTenantCommandValidator : AbstractValidator<ProvisionTenantCommand>
{
    public ProvisionTenantCommandValidator()
    {
        RuleFor(c => c.Name)
            .NotEmpty().WithErrorCode("Tenant.NameRequired")
            .MaximumLength(150).WithErrorCode("Tenant.NameTooLong");

        RuleFor(c => c.Slug)
            .NotEmpty().WithErrorCode("Tenant.SlugInvalid")
            .Matches("^[a-z0-9]+(-[a-z0-9]+)*$")
            .WithErrorCode("Tenant.SlugInvalid");

        RuleFor(c => c.Tier)
            .InclusiveBetween(1, 3).WithErrorCode("Tenant.InvalidTier");

        RuleFor(c => c.Timezone)
            .MaximumLength(50).WithErrorCode("Tenant.TimezoneTooLong");

        RuleFor(c => c.Locale)
            .MaximumLength(10).WithErrorCode("Tenant.LocaleTooLong");
    }
}
