namespace Worfair.Modules.Recruitment.Application.EventHandlers;

using MediatR;
using Worfair.BuildingBlocks.Application.Contracts;
using Worfair.BuildingBlocks.Application.Events;
using Worfair.Modules.Recruitment.Domain.Aggregates.Candidate.Events;

/// <summary>
/// CandidateHired → CandidateHiredIntegrationEvent (Outbox) → Proposals/Hiring
/// e Notifications (docs/architecture/03 §2.3/§4). Handler idempotente por
/// deduplicação de IntegrationEvent.Id (docs/architecture/03 §5).
/// </summary>
public sealed class CandidateHiredDomainEventHandler(IEventBus eventBus)
    : INotificationHandler<DomainEventNotification<CandidateHiredDomainEvent>>
{
    public Task Handle(
        DomainEventNotification<CandidateHiredDomainEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;
        return eventBus.PublishAsync(new CandidateHiredIntegrationEvent
        {
            TenantId = domainEvent.TenantId.Value,
            CandidateId = domainEvent.CandidateId.Value
        }, cancellationToken);
    }
}
