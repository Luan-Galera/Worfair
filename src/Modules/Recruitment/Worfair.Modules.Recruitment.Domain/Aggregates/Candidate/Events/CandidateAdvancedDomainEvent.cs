namespace Worfair.Modules.Recruitment.Domain.Aggregates.Candidate.Events;

using Worfair.BuildingBlocks.Domain.DomainEvents;

/// <summary>Avanço no pipeline (inclui rejeição: ToStatus = Rejected).</summary>
public sealed record CandidateAdvancedDomainEvent(
    Guid CandidateId,
    int FromStatus,
    int ToStatus) : DomainEvent;
