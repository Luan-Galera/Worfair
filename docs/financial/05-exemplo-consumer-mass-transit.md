# 05 — Exemplo: Consumer MassTransit (idempotente + Outbox)

Consumer de referência que processa o evento de ingestão do webhook Asaas
(`PAYMENT_RECEIVED`), aplica a transição financeira de forma **idempotente**,
**confirmada no provedor** e com **outbox transacional**.

## 1. Cenário

```
AsaasWebhookReceivedIntegrationEvent (fila worfair.financial.asaas-events)
   → AsaasWebhookProcessorConsumer
       → 1. idempotência (inbox/agregado)
       → 2. confirmação no Asaas (GET /payments/{id} + GET /payments/{id}/splits)
       → 3. FinancialTransaction.Transition(PaymentReceived | FundsAvailable)
       → 4. ledger + transaction_events + outbox (uma transação)
       → 5. ACK pós-commit → eventos de saída publicados pelo EF Outbox
```

## 2. Contrato do módulo

```csharp
// Financial.Contracts/Events/AsaasWebhookReceivedIntegrationEvent.cs
public sealed record AsaasWebhookReceivedIntegrationEvent(
    Guid EventId, string EventType, string? PaymentId,
    string? TransferId, TenantId? TenantId, DateTime OccurredOn);
```

## 3. Consumer (MassTransit)

```csharp
// Financial.Infrastructure/Messaging/Consumers/AsaasWebhookProcessorConsumer.cs
public sealed class AsaasWebhookProcessorConsumer(
    IWebhookInboxRepository inbox,
    IFinancialTransactionRepository transactions,
    IAsaasGateway asaas,
    ILedgerRepository ledger,
    IDateTimeProvider clock,
    ILogger<AsaasWebhookProcessorConsumer> logger)
    : IConsumer<AsaasWebhookReceivedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<AsaasWebhookReceivedIntegrationEvent> context)
    {
        var message = context.Message;
        var ct = context.CancellationToken;

        // ── 1. IDEMPOTÊNCIA (camada 1): o inbox é a porta de entrada única.
        //      Se o item ainda não existe (ex.: fila entregou antes do commit do endpoint),
        //      o consumidor NÃO processa sem o registro — NACK com retry até o item existir.
        var item = await inbox.GetByEventIdAsync(message.EventId, message.EventType, ct);
        if (item is null)
        {
            logger.LogInformation("Webhook {EventId} ainda não persistido; aguardando retry.", message.EventId);
            throw new WebhookNotPersistedException(message.EventId);   // → UseMessageRetry → reentrega
        }

        // Claim (single-writer): garante que nenhum outro consumidor processe o mesmo item
        if (!await inbox.TryClaimAsync(item.Id, ct))
        {
            if (item.Status == WebhookInboxStatus.Processed)
                return;                              // já processado ⇒ ACK silencioso (idempotente)
            throw new WebhookAlreadyClaimedException(item.Id);           // outro worker ativo ⇒ retry
        }

        // ── 2. CONFIRMAÇÃO NO PROVEDOR (FIN-04): payload nunca é fonte de verdade
        var payment = message.PaymentId is not null
            ? await asaas.GetPaymentAsync(message.PaymentId, ct)
            : null;

        if (payment is null)
        {
            await inbox.MarkFailedAsync(item.Id, "Pagamento não encontrado no Asaas", ct);
            return;                                  // ACK: irreversível — alerta via auditoria
        }

        // ── 3. LOCALIZA A TRANSAÇÃO FINANCEIRA DO TENANT (RLS garante isolamento)
        var transaction = await transactions.GetByAsaasPaymentIdAsync(payment.Id, ct);
        if (transaction is null)
        {
            await inbox.MarkFailedAsync(item.Id, "Nenhuma transação para o pagamento", ct);
            return;
        }

        // ── 4. TRANSIÇÃO DE DOMÍNIO (máquina de estados — doc 02)
        Result transition = message.EventType switch
        {
            "PAYMENT_CONFIRMED" or "PAYMENT_RECEIVED"
                => transaction.Transition(
                    FinancialTransactionTrigger.PaymentReceived,
                    providerEventId: message.EventId,
                    occurredAtUtc: payment.ConfirmedDate ?? clock.UtcNow),

            "PAYMENT_REFUNDED"
                => transaction.Transition(
                    FinancialTransactionTrigger.PaymentRefunded,
                    providerEventId: message.EventId,
                    occurredAtUtc: clock.UtcNow),

            _ => Result.Failure(FinancialTransactionErrors.UnsupportedWebhookEvent(message.EventType))
        };

        if (transition.IsFailure)
        {
            // Transição ILEGAL (ex.: duplicada/fora de ordem) — terminal: não reenfileira
            await inbox.MarkFailedAsync(item.Id, transition.Error!.Message, ct);
            return;
        }

        // ── 5. UMA ÚNICA TRANSAÇÃO DE BANCO:
        //      transação atualizada + transaction_events + ledger + OUTBOX (MassTransit)
        //      + inbox.processed_at. Commit ⇒ o EF Outbox publica os eventos de saída.
        transactions.Update(transaction);

        if (transaction.Status == FinancialTransactionStatus.PaymentReceived)
            await ledger.AppendPaymentReceivedAsync(transaction, payment.ConfirmedDate ?? clock.UtcNow, ct);

        await CheckFundsAvailabilityAsync(transaction, payment.Id, ct);   // splits → FUNDS_AVAILABLE
        await inbox.MarkProcessedAsync(item.Id, ct);

        await transactions.UnitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Webhook {EventId} aplicado: transação {Txn} → {Status}",
            message.EventId, transaction.Id, transaction.Status);
    }
}
```

## 4. Confirmação de splits → `FUNDS_AVAILABLE`

```csharp
private async Task CheckFundsAvailabilityAsync(
    FinancialTransaction transaction, string paymentId, CancellationToken ct)
{
    if (transaction.Status != FinancialTransactionStatus.PaymentReceived)
        return;

    var splits = await asaas.GetSplitsAsync(paymentId, ct);
    if (splits.Count == 0)                              // pagamento sem split: disponível imediato
    {
        transaction.Transition(FinancialTransactionTrigger.FundsAvailable,
            providerEventId: null, occurredAtUtc: clock.UtcNow);
        return;
    }

    // Somente quando TODOS os splits estão liquidados a retenção fica disponível
    if (splits.All(s => s.Status == "SPLIT_RECEIVED"))
    {
        transaction.Transition(FinancialTransactionTrigger.FundsAvailable,
            providerEventId: null, occurredAtUtc: clock.UtcNow);
    }
}
```

## 5. Definições auxiliares

```csharp
// Domain: enum de status/trigger e contrato do evento de saída (após commit)
public sealed record FinancialTransactionStatusChangedDomainEvent(
    FinancialTransactionId TransactionId, TenantId TenantId,
    FinancialTransactionStatus From, FinancialTransactionStatus To,
    FinancialTransactionTrigger Trigger, Guid? ProviderEventId) : IDomainEvent;

public sealed record PaymentReceivedIntegrationEvent(
    Guid EventId, TenantId TenantId, Guid TransactionId, decimal Amount,
    string Currency, DateTime ReceivedAtUtc) : IIntegrationEvent;
```

```csharp
// Application/Integration/PaymentReceivedIntegrationEventPublisher.cs
// Disparado a partir do domain event, DENTRO da transação: a mensagem é
// gravada no outbox e entregue apenas após o commit (AddEntityFrameworkOutbox).
public sealed class PaymentReceivedDomainEventHandler(
    IIntegrationEventOutbox outbox, IDateTimeProvider clock)
    : INotificationHandler<FinancialTransactionStatusChangedDomainEvent>
{
    public Task Handle(FinancialTransactionStatusChangedDomainEvent e, CancellationToken ct)
    {
        if (e.To != FinancialTransactionStatus.PaymentReceived)
            return Task.CompletedTask;

        return outbox.AddAsync(new PaymentReceivedIntegrationEvent(
            EventId: Guid.NewGuid(), TenantId: e.TenantId,
            TransactionId: e.TransactionId.Value, Amount: 0m,   // valor resolvido do agregado
            Currency: "BRL", ReceivedAtUtc: clock.UtcNow), ct);
    }
}
```

## 6. Por que este desenho é seguro

| Garantia | Mecanismo |
| -------- | --------- |
| **Idempotente** | `event_id` UNIQUE no inbox + `provider_event_id` UNIQUE em `transaction_events` + `ProviderEventId` no agregado (3 camadas) |
| **Sem corrida** | `FOR UPDATE SKIP LOCKED` no claim; dois workers nunca processam o mesmo item |
| **Payload não é verdade** | `GET /payments/{id}` e `GET /payments/{id}/splits` antes da transição |
| **Estado consistente** | estado + eventos + ledger + outbox em **um** commit; outbox entrega após commit |
| **Transição ilegal bloqueada** | matriz de domínio (`Transition`) + função `transition_transaction` no banco |
| **Isolamento multi-tenant** | `tenant_id` em todas as tabelas + RLS + `ITenantProvider` do contexto |
| **Trilha imutável** | `transaction_events`/`ledger_entries`/inbox append-only (SEC-04) |
| **Sem perda de evento** | NACK/retry até sucesso; DLQ apenas para falhas permanentes |

## 7. Alternativa: RabbitMQ Client puro (sem MassTransit)

Se preferir o client direto, o mesmo desenho vale com:

1. `IBasicConsumer`/`AsyncEventingBasicConsumer` no consumidor.
2. `channel.BasicAck` **somente após** `SaveChangesAsync` (nunca antes).
3. `BasicNack(requeue: true)` em falha transitória; após N tentativas
   `BasicNack(requeue: false)` + publica na fila de erro.
4. Outbox próprio: `outbox_messages` gravado na mesma transação + serviço
   background que publica e marca `processed_on` — exatamente o contrato já
   definido em `Worfair.BuildingBlocks.Infrastructure.Persistence.Outbox`
   (arquitetura, doc 02).

**Recomendação:** MassTransit — elimina bugs de ack/requeue/outbox que o client
puro deixa a cargo do time, e seu EF Outbox já cobre o padrão pedido.