namespace Worfair.BuildingBlocks.Infrastructure.Events;

using MediatR;
using Worfair.BuildingBlocks.Application.Contracts;
using Worfair.BuildingBlocks.Application.Events;

/// <summary>
/// Transporte padrão (docs/architecture/02 §2): entrega integration events
/// in-process, via MediatR — usado pelo processador do Outbox após o commit.
/// </summary>
public sealed class InProcessEventBus(IMediator mediator) : IEventBus
{
    public async Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        var wrapperType = typeof(IntegrationEventNotification<>).MakeGenericType(integrationEvent.GetType());
        var notification = (INotification)Activator.CreateInstance(wrapperType, integrationEvent)!;
        await mediator.Publish(notification, cancellationToken).ConfigureAwait(false);
    }
}
