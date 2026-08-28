namespace Worfair.Modules.Tenants.Application.EventHandlers;

using MediatR;
using Worfair.BuildingBlocks.Application.Contracts;
using Worfair.BuildingBlocks.Application.Events;
using Worfair.Modules.Tenants.Contracts.Events;

/// <summary>
/// Domain event → Integration Event (via Outbox): Tenants → Todos
/// (docs/architecture/03 §4 — TenantProvisioned).
/// </summary>
public sealed class TenantProvisionedDomainEventHandler(IEventBus eventBus)
    : INotificationHandler<DomainEventNotification<Worfair.Modules.Tenants.Domain.Events.TenantProvisionedDomainEvent>>
{
    public Task Handle(
        DomainEventNotification<Worfair.Modules.Tenants.Domain.Events.TenantProvisionedDomainEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        return eventBus.PublishAsync(new TenantProvisionedIntegrationEvent
        {
            TenantId = domainEvent.TenantId.Value,
            Name = domainEvent.Name,
            Slug = domainEvent.Slug,
            Tier = 1
        }, cancellationToken);
    }
}
