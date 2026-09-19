namespace Worfair.Modules.Identity.Application.ManageRoles;

using FluentValidation;
using Worfair.BuildingBlocks.Application.Cqrs;

/// <summary>
/// Concessão/revogação de role de TENANT (R-04/R-05): exige membership ativa do
/// alvo no tenant corrente; roles globais só em contexto de plataforma.
/// </summary>
public sealed record AssignTenantRoleCommand(Guid TargetUserId, string RoleCode) : ICommand<Result>;

public sealed record RemoveTenantRoleCommand(Guid TargetUserId, string RoleCode) : ICommand<Result>;

/// <summary>
/// Transferência da propriedade do espaço: concede OWNER ao novo dono e
/// revoga do atual na mesma operação (o espaço nunca fica sem dono).
/// </summary>
public sealed record TransferSpaceOwnershipCommand(Guid NewOwnerUserId) : ICommand<Result>;

public sealed class AssignTenantRoleCommandValidator : AbstractValidator<AssignTenantRoleCommand>
{
    public AssignTenantRoleCommandValidator()
    {
        RuleFor(c => c.TargetUserId).NotEmpty().WithErrorCode("Auth.InvalidUser");
        RuleFor(c => c.RoleCode).NotEmpty().MaximumLength(50).WithErrorCode("Auth.RoleNotFound");
    }
}

public sealed class RemoveTenantRoleCommandValidator : AbstractValidator<RemoveTenantRoleCommand>
{
    public RemoveTenantRoleCommandValidator()
    {
        RuleFor(c => c.TargetUserId).NotEmpty().WithErrorCode("Auth.InvalidUser");
        RuleFor(c => c.RoleCode).NotEmpty().MaximumLength(50).WithErrorCode("Auth.RoleNotFound");
    }
}

public sealed class TransferSpaceOwnershipCommandValidator : AbstractValidator<TransferSpaceOwnershipCommand>
{
    public TransferSpaceOwnershipCommandValidator()
    {
        RuleFor(c => c.NewOwnerUserId).NotEmpty().WithErrorCode("Auth.InvalidUser");
    }
}
