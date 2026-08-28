namespace Worfair.Modules.Identity.Application.Logout;

using FluentValidation;
using Worfair.BuildingBlocks.Application.Cqrs;

public sealed record LogoutCommand(string RefreshToken) : ICommand<Result>;

public sealed class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    public LogoutCommandValidator()
        => RuleFor(c => c.RefreshToken).NotEmpty().WithErrorCode("Auth.RefreshTokenInvalid");
}
