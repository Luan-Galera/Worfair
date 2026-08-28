namespace Worfair.BuildingBlocks.Application.Contracts;

/// <summary>
/// Evento de integração publicado via Outbox (at-least-once). Consumidores devem
/// ser idempotentes por <see cref="Id"/> (docs/architecture/03 §5).
/// </summary>
public interface IIntegrationEvent
{
    Guid Id { get; }

    /// <summary>Nome estável do contrato (ex.: "worfair.tenants.tenant-provisioned.v1").</summary>
    string Type { get; }

    DateTime OccurredOnUtc { get; }

    /// <summary>Tenant de origem; null para eventos globais (SUPER_ADMIN/plataforma).</summary>
    Guid? TenantId { get; }
}

/// <summary>
/// Barramento assíncrono. A implementação padrão grava no Outbox transacional do
/// módulo — publicação só após o commit (docs/financial/04).
/// </summary>
public interface IEventBus
{
    Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default);
}
