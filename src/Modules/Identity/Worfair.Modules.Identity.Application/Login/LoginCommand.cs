namespace Worfair.Modules.Identity.Application.Login;

using FluentValidation;
using Worfair.BuildingBlocks.Application.Cqrs;
using Worfair.Modules.Identity.Application.Dtos;

/// <summary>
/// Login (SEC-01 §2). O tenant alvo é OPCIONAL e validado contra membership
/// ativa — endpoints de login/switch são os únicos que recebem tenant como
/// ALVO de troca (docs/security/02 §4); requisições de negócio jamais o fazem.
/// </summary>
public sealed record LoginCommand(
    string Email,
    string Password,
    Guid? TargetTenantId = null) : ICommand<Result<AuthResponseDto>>;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(c => c.Email).NotEmpty().EmailAddress().WithErrorCode("Auth.EmailInvalid");
        RuleFor(c => c.Password).NotEmpty().WithErrorCode("Auth.PasswordRequired");
    }
}
