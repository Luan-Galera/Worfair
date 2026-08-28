namespace Worfair.Modules.Recruitment.Application.EventHandlers;

using MediatR;
using Worfair.BuildingBlocks.Application.Contracts;
using Worfair.BuildingBlocks.Application.Events;
using Worfair.Modules.Recruitment.Domain.Aggregates.JobRequisition.Events;

/// <summary>Domain event → IntegrationEvent (Outbox transacional do módulo — D-07).</summary>
public sealed class JobRequisitionPublishedDomainEventHandler(IEventBus eventBus)
    : INotificationHandler<DomainEventNotification<JobRequisitionPublishedDomainEvent>>
{
    public Task Handle(
        DomainEventNotification<JobRequisitionPublishedDomainEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;
        return eventBus.PublishAsync(new JobRequisitionPublishedIntegrationEvent
        {
            JobRequisitionId = domainEvent.JobRequisitionId.Value,
            Title = domainEvent.Title
        }, cancellationToken);
    }
}
