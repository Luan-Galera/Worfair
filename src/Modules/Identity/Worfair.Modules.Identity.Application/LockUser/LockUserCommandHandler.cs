namespace Worfair.Modules.Identity.Application.LockUser;

using Worfair.BuildingBlocks.Application.Cqrs;
using Worfair.BuildingBlocks.Application.Security;
using Worfair.Modules.Identity.Application.Abstractions;
using Worfair.Modules.Identity.Domain.Aggregates.User;
using Worfair.Modules.Identity.Domain.Errors;

public sealed class LockUserCommandHandler(
    IUserRepository users,
    IIdentityUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : ICommandHandler<LockUserCommand, Result>
{
    public async Task<Result> Handle(LockUserCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.UserId == command.TargetUserId)
            return Result.Failure(AuthErrors.CannotLockSelf);

        var user = await users.GetByIdAsync(command.TargetUserId, cancellationToken).ConfigureAwait(false);
        if (user is null)
            return Result.Failure(AuthErrors.UserNotFound);

        user.Lock();
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}