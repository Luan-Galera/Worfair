namespace Worfair.Modules.Identity.Application.SwitchTenant;

using Worfair.BuildingBlocks.Application.Cqrs;
using Worfair.BuildingBlocks.Application.Security;
using Worfair.BuildingBlocks.Domain.Errors;
using Worfair.BuildingBlocks.Domain.ValueObjects;
using Worfair.Modules.Identity.Application.Abstractions;
using Worfair.Modules.Identity.Application.Dtos;
using Worfair.Modules.Identity.Domain.Abstractions;

public sealed class SwitchTenantCommandHandler(
    IUserRepository users,
    SessionIssuer sessions,
    ICurrentUser currentUser)
    : ICommandHandler<SwitchTenantCommand, Result<AuthResponseDto>>
{
    public async Task<Result<AuthResponseDto>> Handle(SwitchTenantCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
            return Result.Failure<AuthResponseDto>(AuthErrors.InvalidCredentials);

        var user = await users.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null || user.EnsureCanAuthenticate().IsFailure)
            return Result.Failure<AuthResponseDto>(AuthErrors.InvalidCredentials);

        var contextResult = await sessions.ResolveContextAsync(
            userId, new TenantId(command.TargetTenantId), cancellationToken).ConfigureAwait(false);
        if (contextResult.IsFailure)
            return Result.Failure<AuthResponseDto>(contextResult.Error!);

        var tokensResult = await sessions.IssueNewFamilyAsync(user, contextResult.Value, cancellationToken)
            .ConfigureAwait(false);
        if (tokensResult.IsFailure)
            return Result.Failure<AuthResponseDto>(tokensResult.Error!);

        var context = contextResult.Value;
        var tokens = tokensResult.Value;

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
