namespace Worfair.Modules.Tenants.Application.Members;

using FluentValidation;
using Worfair.BuildingBlocks.Application.Cqrs;

/// <summary>Adiciona usuário como membro ATIVO do tenant corrente (tenants.members.manage).</summary>
public sealed record AddTenantMemberCommand(Guid TargetUserId) : ICommand<Result>;

public sealed record SetTenantMemberStatusCommand(Guid TargetUserId, bool Active) : ICommand<Result>;

public sealed class AddTenantMemberCommandValidator : AbstractValidator<AddTenantMemberCommand>
{
    public AddTenantMemberCommandValidator()
        => RuleFor(c => c.TargetUserId).NotEmpty().WithErrorCode("Membership.InvalidUserId");
}

public sealed class SetTenantMemberStatusCommandValidator : AbstractValidator<SetTenantMemberStatusCommand>
{
    public SetTenantMemberStatusCommandValidator()
        => RuleFor(c => c.TargetUserId).NotEmpty().WithErrorCode("Membership.InvalidUserId");
}
