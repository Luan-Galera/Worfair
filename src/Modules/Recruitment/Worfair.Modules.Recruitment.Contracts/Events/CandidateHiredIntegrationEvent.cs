namespace Worfair.Modules.Recruitment.Contracts.Events;

using Worfair.BuildingBlocks.Contracts.IntegrationEvents;

/// <summary>
/// Catálogo v1 (docs/architecture/03 §4): Recruitment → Proposals/Hiring
/// (inicia fluxo de oferta) + Notifications. Só IDs — sem dados sensíveis.
/// </summary>
public sealed record CandidateHiredIntegrationEvent : IntegrationEvent
{
    public required Guid CandidateId { get; init; }

    public Guid? CandidateUserId { get; init; }

    public override string ContractType => "worfair.recruitment.candidate-hired.v1";
}
