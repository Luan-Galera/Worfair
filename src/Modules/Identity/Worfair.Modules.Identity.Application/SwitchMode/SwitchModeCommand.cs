namespace Worfair.Modules.Identity.Application.SwitchMode;

using FluentValidation;
using Worfair.BuildingBlocks.Application.Cqrs;
using Worfair.Modules.Identity.Application.Dtos;

/// <summary>
/// Alternância Contratante ↔ Prestador ↔ Global (SEC-03/FE-03): valida o modo
/// pedido contra as permissões efetivas DISPONÍVEIS e reemite o token.
/// Alternar a UI nunca concede nada.
/// </summary>
public sealed record SwitchModeCommand(string RequestedMode) : ICommand<Result<AuthResponseDto>>;

public sealed class SwitchModeCommandValidator : AbstractValidator<SwitchModeCommand>
{
    public SwitchModeCommandValidator()
        => RuleFor(c => c.RequestedMode)
            .Must(m => m is "contracting" or "provider" or "global")
            .WithErrorCode("Auth.ModeUnavailable");
}
