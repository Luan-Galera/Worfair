namespace Worfair.Modules.Identity.Application.RefreshToken;

using FluentValidation;
using Worfair.BuildingBlocks.Application.Cqrs;
using Worfair.Modules.Identity.Application.Dtos;

public sealed record RefreshAccessTokenCommand(string RefreshToken) : ICommand<Result<AuthResponseDto>>;

public sealed class RefreshAccessTokenCommandValidator : AbstractValidator<RefreshAccessTokenCommand>
{
    public RefreshAccessTokenCommandValidator()
        => RuleFor(c => c.RefreshToken).NotEmpty().WithErrorCode("Auth.RefreshTokenInvalid");
}
