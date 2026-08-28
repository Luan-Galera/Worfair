namespace Worfair.Modules.Recruitment.Domain.Aggregates.JobRequisition.Events;

using Worfair.BuildingBlocks.Domain.DomainEvents;
using Worfair.BuildingBlocks.Domain.ValueObjects;

/// <summary>Publicado → Notifications divulga; origem do fluxo ATS.</summary>
public sealed record JobRequisitionPublishedDomainEvent(
    JobRequisitionId JobRequisitionId,
    string Title) : DomainEvent;
