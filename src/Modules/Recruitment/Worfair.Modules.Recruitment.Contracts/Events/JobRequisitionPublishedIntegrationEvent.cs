namespace Worfair.Modules.Recruitment.Contracts.Events;

using Worfair.BuildingBlocks.Contracts.IntegrationEvents;

/// <summary>
/// Catálogo v1 (docs/architecture/03 §4): Recruitment → Notifications
/// (divulgação da vaga no processo interno).
/// </summary>
public sealed record JobRequisitionPublishedIntegrationEvent : IntegrationEvent
{
    public required Guid JobRequisitionId { get; init; }

    public required string Title { get; init; }

    public override string ContractType => "worfair.recruitment.job-requisition-published.v1";
}
