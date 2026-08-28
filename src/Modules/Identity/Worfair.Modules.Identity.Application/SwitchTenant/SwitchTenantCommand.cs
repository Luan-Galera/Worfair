namespace Worfair.Modules.Identity.Application.SwitchTenant;

using FluentValidation;
using Worfair.BuildingBlocks.Application.Cqrs;
using Worfair.Modules.Identity.Application.Dtos;

/// <summary>
/// ÚNICA via de troca de contexto de tenant (SEC-02 §4): valida membership ativa
/// + tenant ativo no BANCO e emite NOVO par de tokens. O tenant_id do body é
/// alvo de troca — nunca contexto.
/// </summary>
public sealed record SwitchTenantCommand(Guid TargetTenantId) : ICommand<Result<AuthResponseDto>>;

public sealed class SwitchTenantCommandValidator : AbstractValidator<SwitchTenantCommand>
{
    public SwitchTenantCommandValidator()
        => RuleFor(c => c.TargetTenantId).NotEmpty().WithErrorCode("Auth.MembershipNotFound");
}
