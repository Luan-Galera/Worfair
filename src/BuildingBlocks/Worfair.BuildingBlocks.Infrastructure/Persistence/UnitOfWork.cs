namespace Worfair.BuildingBlocks.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Worfair.BuildingBlocks.Domain.Abstractions;
using Worfair.BuildingBlocks.Domain.DomainEvents;
using Worfair.BuildingBlocks.Domain.Entities;

/// <summary>
/// Unidade de trabalho do módulo: despacha os domain events DENTRO da transação
/// e persiste tudo em um único commit (outbox incluído — atomicidade garantida).
/// </summary>
public class UnitOfWork<TDbContext>(
    TDbContext dbContext,
    IDomainEventDispatcher dispatcher) : IUnitOfWork
    where TDbContext : DbContext
{
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // 1. Coleta os eventos dos agregados rastreados.
        var trackedEntries = dbContext.ChangeTracker.Entries<IHasDomainEvents>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        var events = trackedEntries.SelectMany(e => e.DomainEvents).ToList();

        // 2. Handlers rodam ANTES do commit: podem gravar no Outbox do mesmo contexto
        //    (publicação efetiva apenas após o commit).
        if (events.Count > 0)
            await dispatcher.DispatchAsync(events, cancellationToken).ConfigureAwait(false);

        // 3. Commit único.
        var affected = await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        foreach (var aggregate in trackedEntries)
            aggregate.ClearDomainEvents();

        return affected;
    }
}
