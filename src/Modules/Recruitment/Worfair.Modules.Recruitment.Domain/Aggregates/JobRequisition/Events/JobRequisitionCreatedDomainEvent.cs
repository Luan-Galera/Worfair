namespace Worfair.Modules.Recruitment.Domain.Aggregates.JobRequisition.Events;

using Worfair.BuildingBlocks.Domain.DomainEvents;
using Worfair.BuildingBlocks.Domain.ValueObjects;

public sealed record JobRequisitionCreatedDomainEvent(
    JobRequisitionId JobRequisitionId,
    string Title) : DomainEvent;
