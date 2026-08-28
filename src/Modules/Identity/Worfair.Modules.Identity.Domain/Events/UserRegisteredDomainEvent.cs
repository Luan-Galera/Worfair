namespace Worfair.Modules.Identity.Domain.Events;

using Worfair.BuildingBlocks.Domain.DomainEvents;
using Worfair.Modules.Identity.Domain.ValueObjects;

public sealed record UserRegisteredDomainEvent(
    UserId UserId,
    string Email,
    string FullName) : DomainEvent;
