namespace Worfair.Modules.Identity.Application.RefreshToken;

using Worfair.BuildingBlocks.Application.Cqrs;
using Worfair.BuildingBlocks.Domain.Errors;
using Worfair.Modules.Identity.Application.Abstractions;
using Worfair.Modules.Identity.Application.Dtos;

public sealed class RefreshAccessTokenCommandHandler(
    SessionIssuer sessions)
    : ICommandHandler<RefreshAccessTokenCommand, Result<AuthResponseDto>>
{
    public async Task<Result<AuthResponseDto>> Handle(RefreshAccessTokenCommand command, CancellationToken cancellationToken)
    {
        var userResult = await sessions.RotateAsync(command.RefreshToken, cancellationToken).ConfigureAwait(false);
        if (userResult.IsFailure)
            return Result.Failure<AuthResponseDto>(userResult.Error!);

        var (user, context, tokens) = userResult.Value;

        return new AuthResponseDto(
            user.Id,
            user.Email.Value,
            user.FullName,
            context.TenantId?.Value,
            ModeResolver.ToClaimValue(context.Mode),
            context.RoleCodes,
            tokens.AccessToken.Value,
            tokens.AccessToken.ExpiresAtUtc,
            tokens.RefreshToken,
            tokens.RefreshTokenExpiresAtUtc);
    }
}
