namespace Worfair.Modules.Jobs.Contracts;

using Worfair.BuildingBlocks.Contracts.IntegrationEvents;

public sealed record JobPostingPublishedIntegrationEvent : IntegrationEvent
{
    public required Guid JobPostingId { get; init; }
    public required string Title { get; init; }
    public override string ContractType => "worfair.jobs.job-posting-published.v1";
}

public sealed record ServiceProjectPublishedIntegrationEvent : IntegrationEvent
{
    public required Guid ServiceProjectId { get; init; }
    public required string Title { get; init; }
    public override string ContractType => "worfair.jobs.service-project-published.v1";
}
