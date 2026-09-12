namespace Worfair.Modules.Identity.Application.LockUser;

using Worfair.BuildingBlocks.Application.Cqrs;

public sealed record LockUserCommand(Guid TargetUserId) : ICommand<Result>;