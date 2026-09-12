namespace Worfair.Modules.Identity.Application.LockUser;

using Worfair.BuildingBlocks.Application.Cqrs;

public sealed record UnlockUserCommand(Guid TargetUserId) : ICommand<Result>;