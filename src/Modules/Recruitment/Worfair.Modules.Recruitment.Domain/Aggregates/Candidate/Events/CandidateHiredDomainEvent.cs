namespace Worfair.Modules.Recruitment.Domain.Aggregates.Candidate.Events;

using Worfair.BuildingBlocks.Domain.DomainEvents;
using Worfair.BuildingBlocks.Domain.ValueObjects;

/// <summary>
/// Escolha do candidato vencedor — dispara CandidateHiredIntegrationEvent
/// → Proposals/Hiring (docs/architecture/03 §2.3 e §4).
/// </summary>
public sealed record CandidateHiredDomainEvent(
    CandidateId CandidateId,
    TenantId TenantId) : DomainEvent;
