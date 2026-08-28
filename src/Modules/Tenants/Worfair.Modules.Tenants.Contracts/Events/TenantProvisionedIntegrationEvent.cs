namespace Worfair.Modules.Tenants.Contracts.Events;

using Worfair.BuildingBlocks.Contracts.IntegrationEvents;

/// <summary>
/// Catálogo v1 (docs/architecture/03 §4): Tenants → Todos.
/// Cria contexto default do tenant nos demais módulos. Só IDs/metadados.
/// </summary>
public sealed record TenantProvisionedIntegrationEvent : IntegrationEvent
{
    public required string Name { get; init; }

    public required string Slug { get; init; }

    public int Tier { get; init; } = 1;

    public override string ContractType => "worfair.tenants.tenant-provisioned.v1";
}
