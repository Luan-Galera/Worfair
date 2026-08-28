namespace Worfair.BuildingBlocks.Application.Events;

using MediatR;

/// <summary>
/// Wrapper MediatR para domain events (Domain permanece livre de dependências).
/// Handlers: INotificationHandler&lt;DomainEventNotification&lt;TEvent&gt;&gt;.
/// </summary>
public sealed record DomainEventNotification<TDomainEvent>(TDomainEvent DomainEvent) : INotification
    where TDomainEvent : notnull;

/// <summary>
/// Wrapper MediatR para integration events entregues pelo Outbox (in-process por padrão).
/// </summary>
public sealed record IntegrationEventNotification<TIntegrationEvent>(TIntegrationEvent IntegrationEvent) : INotification
    where TIntegrationEvent : notnull;
