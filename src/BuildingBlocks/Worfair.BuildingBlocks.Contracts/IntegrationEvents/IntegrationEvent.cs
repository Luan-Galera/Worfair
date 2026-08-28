namespace Worfair.BuildingBlocks.Contracts.IntegrationEvents;

/// <summary>
/// Base de todo integration event: Id (idempotência), Type (contrato estável),
/// OccurredOnUtc e TenantId de origem (null = global).
/// Eventos NUNCA carregam dados sensíveis — só IDs e metadados mínimos
/// (docs/architecture/03 §5).
/// </summary>
public abstract record IntegrationEvent : Worfair.BuildingBlocks.Application.Contracts.IIntegrationEvent
{
    protected IntegrationEvent()
    {
        Id = Guid.NewGuid();
        OccurredOnUtc = DateTime.UtcNow;
    }

    public Guid Id { get; init; }

    public string Type => ContractType;

    public DateTime OccurredOnUtc { get; init; }

    public Guid? TenantId { get; init; }

    /// <summary>Nome estável do contrato (ex.: "worfair.tenants.tenant-provisioned.v1").</summary>
    public abstract string ContractType { get; }
}
