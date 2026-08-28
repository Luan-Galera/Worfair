namespace Worfair.Modules.Recruitment.Domain.Abstractions;

using Worfair.BuildingBlocks.Domain.DomainEvents;
using Worfair.BuildingBlocks.Domain.Entities;
using Worfair.BuildingBlocks.Domain.Tenancy;

/// <summary>
/// Raiz de agregado tenant-owned com domain events (combinação ausente no
/// núcleo: TenantEntity × AggregateRoot). Eventos são despachados no
/// SaveChanges → Outbox transacional do módulo (D-07).
/// </summary>
public abstract class TenantAggregateRoot<TId> : TenantEntity<TId>, IHasDomainEvents
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void RaiseDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
