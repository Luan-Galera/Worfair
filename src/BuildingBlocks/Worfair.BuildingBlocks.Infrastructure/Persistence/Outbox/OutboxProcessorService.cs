namespace Worfair.BuildingBlocks.Infrastructure.Persistence.Outbox;

using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Worfair.BuildingBlocks.Application.Contracts;
using Worfair.BuildingBlocks.Application.Events;

/// <summary>
/// Processador do Outbox: entrega mensagens pendentes após o commit
/// (at-least-once; consumidores idempotentes por IntegrationEvent.Id).
/// Uma instância por módulo — cada uma varre apenas o schema do próprio contexto.
/// </summary>
public sealed class OutboxProcessorService<TDbContext>(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxProcessorService<TDbContext>> logger)
    : BackgroundService where TDbContext : DbContext
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 20;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingBatchAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha ao processar lote do Outbox ({DbContext}).", typeof(TDbContext).Name);
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessPendingBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
        var eventBus = scope.ServiceProvider.GetRequiredService<InProcessEventBus>();

        var messages = await dbContext.Set<OutboxMessage>()
            .Where(m => m.ProcessedOnUtc == null)
            .OrderBy(m => m.OccurredOnUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var message in messages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!OutboxEventTypeRegistry.TryResolve(message.Type, out var clrType) || clrType is null)
            {
                // Contrato ainda não registrado neste processo (módulo não carregado):
                // mantém pendente para tentativa futura.
                continue;
            }

            try
            {
                var integrationEvent =
                    (IIntegrationEvent)JsonSerializer.Deserialize(message.Payload, clrType, SerializerOptions)!;

                await eventBus.PublishAsync(integrationEvent, cancellationToken).ConfigureAwait(false);

                message.ProcessedOnUtc = DateTime.UtcNow;
                message.Error = null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // At-least-once: mantém pendente para retry no próximo ciclo.
                message.Error = $"{ex.GetType().Name}: {ex.Message}";
                logger.LogError(ex,
                    "Outbox: falha ao entregar {Type} ({Id}). Nova tentativa no próximo ciclo.",
                    message.Type, message.Id);
            }

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
