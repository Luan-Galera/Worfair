namespace Worfair.Modules.Recruitment.Domain.Aggregates.JobRequisition.Events;

using Worfair.BuildingBlocks.Domain.DomainEvents;
using Worfair.BuildingBlocks.Domain.ValueObjects;

public sealed record JobRequisitionClosedDomainEvent(
    JobRequisitionId JobRequisitionId,
    string Reason) : DomainEvent;
