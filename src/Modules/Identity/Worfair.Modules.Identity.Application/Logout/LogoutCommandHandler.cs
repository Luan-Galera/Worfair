namespace Worfair.Modules.Identity.Application.Logout;

using System;
using Worfair.BuildingBlocks.Application.Cqrs;
using Worfair.Modules.Identity.Application.Abstractions;
using Worfair.Modules.Identity.Domain.Abstractions;

/// <summary>Logout revoga o refresh token informado (idempotente).</summary>
public sealed class LogoutCommandHandler(
    IRefreshTokenRepository refreshTokens,
    IIdentityUnitOfWork unitOfWork)
    : ICommandHandler<LogoutCommand, Result>
{
    public async Task<Result> Handle(LogoutCommand command, CancellationToken cancellationToken)
    {
        var stored = await refreshTokens
            .GetByHashAsync(SessionTokens.Hash(command.RefreshToken), cancellationToken)
            .ConfigureAwait(false);

        if (stored is not null && !stored.WasRevoked)
        {
            stored.Revoke(DateTime.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return Result.Success();
    }
}
