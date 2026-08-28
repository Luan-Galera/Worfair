namespace Worfair.BuildingBlocks.Infrastructure.DomainEvents;

using MediatR;
using Worfair.BuildingBlocks.Application.Events;
using Worfair.BuildingBlocks.Domain.Abstractions;
using Worfair.BuildingBlocks.Domain.DomainEvents;

/// <summary>Despacha domain events in-process via MediatR (wrappers tipados).</summary>
public sealed class DomainEventDispatcher(IMediator mediator) : IDomainEventDispatcher
{
    public async Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            var wrapperType = typeof(DomainEventNotification<>).MakeGenericType(domainEvent.GetType());
            var notification = (INotification)Activator.CreateInstance(wrapperType, domainEvent)!;
            await mediator.Publish(notification, cancellationToken).ConfigureAwait(false);
        }
    }
}
