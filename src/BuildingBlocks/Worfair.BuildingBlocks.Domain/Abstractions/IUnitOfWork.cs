namespace Worfair.BuildingBlocks.Domain.Abstractions;

using Worfair.BuildingBlocks.Domain.DomainEvents;

/// <summary>
/// Unidade de trabalho: persiste o contexto do módulo e despacha os domain events
/// acumulados dentro da mesma transação.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Despacha domain events (in-process) após a persistência.</summary>
public interface IDomainEventDispatcher
{
    Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
}
