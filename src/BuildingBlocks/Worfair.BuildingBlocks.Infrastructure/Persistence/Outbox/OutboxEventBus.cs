namespace Worfair.BuildingBlocks.Infrastructure.Persistence.Outbox;

using System.Text.Json;
using MediatR;
using Worfair.BuildingBlocks.Application.Contracts;
using Worfair.BuildingBlocks.Application.Events;

/// <summary>
/// Implementação padrão de IEventBus por módulo: grava a mensagem no Outbox do
/// próprio contexto — publicação só se torna efetiva após o commit
/// (docs/financial/04 §2; docs/architecture/03 §5).
/// </summary>
public sealed class OutboxEventBus<TDbContext>(TDbContext dbContext) : IEventBus
    where TDbContext : DbContext
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    public Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // Registra contrato → tipo para o processador conseguir desserializar.
        OutboxEventTypeRegistry.Register(integrationEvent.Type, integrationEvent.GetType());

        dbContext.Set<OutboxMessage>().Add(new OutboxMessage
        {
            TenantId = integrationEvent.TenantId,
            Type = integrationEvent.Type,
            Payload = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType(), SerializerOptions),
            OccurredOnUtc = integrationEvent.OccurredOnUtc
        });

        // Sem SaveChanges aqui: a mensagem é commitada junto com a unidade de trabalho.
        return Task.CompletedTask;
    }
}
