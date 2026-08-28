namespace Worfair.Modules.Identity.Application.EventHandlers;

using MediatR;
using Worfair.BuildingBlocks.Application.Contracts;
using Worfair.BuildingBlocks.Application.Events;
using Worfair.Modules.Identity.Contracts.Events;

/// <summary>
/// Domain event → Integration Event (Outbox): Identity → Todos
/// (docs/architecture/03 §4 — UserProvisioned).
/// </summary>
public sealed class UserRegisteredDomainEventHandler(IEventBus eventBus)
    : INotificationHandler<DomainEventNotification<Worfair.Modules.Identity.Domain.Events.UserRegisteredDomainEvent>>
{
    public Task Handle(
        DomainEventNotification<Worfair.Modules.Identity.Domain.Events.UserRegisteredDomainEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        return eventBus.PublishAsync(new UserProvisionedIntegrationEvent
        {
            UserId = domainEvent.UserId.Value,
            Email = domainEvent.Email,
            FullName = domainEvent.FullName
        }, cancellationToken);
    }
}
