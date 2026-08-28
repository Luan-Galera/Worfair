namespace Worfair.Modules.Recruitment.Domain.Aggregates.Candidate.Events;

using Worfair.BuildingBlocks.Domain.DomainEvents;

public sealed record CandidateAppliedDomainEvent(
    CandidateId CandidateId) : DomainEvent;
