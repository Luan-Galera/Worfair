namespace Worfair.Modules.Identity.Application.Login;

using Worfair.BuildingBlocks.Application.Cqrs;
using Worfair.BuildingBlocks.Domain.Errors;
using Worfair.Modules.Identity.Application.Abstractions;
using Worfair.Modules.Identity.Application.Dtos;
using Worfair.Modules.Identity.Application.Ports;
using Worfair.Modules.Identity.Domain.Aggregates.User;
using Worfair.Modules.Identity.Domain.Abstractions;
using Worfair.Modules.Identity.Domain.Errors;

public sealed class LoginCommandHandler(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    SessionIssuer sessions)
    : ICommandHandler<LoginCommand, Result<AuthResponseDto>>
{
    public async Task<Result<AuthResponseDto>> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        var emailResult = Domain.ValueObjects.Email.Create(command.Email);
        if (emailResult.IsFailure)
            return Result.Failure<AuthResponseDto>(AuthErrors.InvalidCredentials);

        var user = await users
            .GetByEmailAsync(emailResult.Value.Value, cancellationToken)
            .ConfigureAwait(false);

        // Mensagem única para usuário inexistente/senha errada (não revela existência).
        if (user is null || !passwordHasher.Verify(user.PasswordHash, command.Password))
            return Result.Failure<AuthResponseDto>(AuthErrors.InvalidCredentials);

        var authCheck = user.EnsureCanAuthenticate();
        if (authCheck.IsFailure)
            return Result.Failure<AuthResponseDto>(authCheck.Error!);

        var contextResult = command.TargetTenantId is { } targetId && targetId != Guid.Empty
            ? await sessions.ResolveContextAsync(user.Id, new TenantId(targetId), cancellationToken)
                .ConfigureAwait(false)
            : await sessions.ResolveDefaultContextAsync(user.Id, cancellationToken).ConfigureAwait(false);

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
