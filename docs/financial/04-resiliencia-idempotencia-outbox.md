# 04 — Resiliência: Idempotência e Outbox Transacional

## 1. Ameaças de consistência

| Situação | Consequência sem proteção |
| -------- | ------------------------- |
| Webhook entregue 2× (at-least-once) | transição aplicada 2×, ledger duplicado |
| Webhook fora de ordem (RECEIVED antes de CONFIRMED) | transição ilegal aplicada em estado errado |
| Falha entre `SaveChanges` e `Publish` | evento perdido (pagamento recebido sem notificação/ledger) |
| Falha entre `Publish` e commit | evento publicado sem registro financeiro |
| Consumidor processa 2× em paralelo | corrida na transição |

**Estratégia combinada:** *Webhook Inbox* (idempotência de entrada) + *Transação
única* (estado + eventos + outbox) + *MassTransit EF Outbox* (garantia de
entrega) + *Ledger append-only* (trilha imutável).

## 2. Webhook Inbox (idempotência de entrada)

```sql
-- financial.asaas_webhook_inbox (DB-09)
CREATE TABLE financial.asaas_webhook_inbox (
    id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    event_id      uuid NOT NULL,                    -- id único do payload Asaas
    event_type    varchar(60) NOT NULL,             -- PAYMENT_RECEIVED, TRANSFER_DONE…
    tenant_id     uuid NULL,                        -- resolvido do conteúdo validado
    payment_id    varchar(40) NULL,                 -- pay_…
    transfer_id   varchar(40) NULL,                 -- tra_…
    payload       jsonb NOT NULL,                   -- payload íntegro (auditoria)
    received_at   timestamptz NOT NULL DEFAULT now(),
    processed_at  timestamptz NULL,
    status        smallint NOT NULL DEFAULT 1,      -- 1=Enqueued 2=Processed 3=Failed
    error         text NULL,
    CONSTRAINT uq_asaas_webhook_event UNIQUE (event_id, event_type)
);
CREATE INDEX ix_asaas_webhook_pending ON financial.asaas_webhook_inbox
    (status, received_at) WHERE status = 1;
```

**Inserção idempotente (endpoint):**

```csharp
// Financial.Infrastructure/Persistence/WebhookInboxRepository.cs
public sealed class WebhookInboxRepository(FinancialDbContext db) : IWebhookInboxRepository
{
    public async Task<bool> TryInsertAsync(WebhookInboxItem item, CancellationToken ct)
    {
        try
        {
            db.WebhookInbox.Add(item);
            await db.SaveChangesAsync(ct);
            return true;                       // novo evento ⇒ enfileira
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return false;                      // duplicado ⇒ ack/200, sem reenfileirar
        }
    }
}
```

**Claim para processamento (single-writer, sem corrida):**

```csharp
public async Task<WebhookInboxItem?> ClaimNextAsync(CancellationToken ct)
{
    return await db.WebhookInbox
        .FromSqlRaw("""
            SELECT * FROM financial.asaas_webhook_inbox
            WHERE status = 1
            ORDER BY received_at
            LIMIT 1
            FOR UPDATE SKIP LOCKED
            """)
        .FirstOrDefaultAsync(ct);
}
```

> `event_id` + `event_type` com UNIQUE ⇒ o mesmo evento só entra uma vez;
> `processed_at`/`status` atualizados **na mesma transação** da transição
> financeira (via função `mark_webhook_processed` SECURITY DEFINER, pois o
> UPDATE direto é proibido para a app — SEC-04).

## 3. Outbox transacional (garantia de entrega)

**Problema resolvido:** publicar no RabbitMQ e gravar no banco são operações
distintas; qualquer falha entre elas deixa o sistema inconsistente.

**Solução (FIN-03):** o **Transactional Outbox** grava a mensagem na mesma
transação dos dados; um **delivery service** publica após o commit.

```csharp
// Financial.Infrastructure — registro do MassTransit com EF Outbox (Postgres)
services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();

    // Outbox do MassTransit: mensagens só são enviadas DEPOIS do commit
    x.AddEntityFrameworkOutbox<FinancialDbContext>(o =>
    {
        o.UsePostgres();
        o.UseBusOutbox();               // entrega via serviço em background
    });

    x.AddConsumer<AsaasWebhookProcessorConsumer>(c =>
    {
        c.UseMessageRetry(r => r.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), 2.0));
        c.UseInMemoryOutbox();          // evita publicar eventos se o consume falhar depois
    });

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host("rabbitmq://localhost", h =>
        {
            h.Username("worfair");
            h.Password("***");
        });

        cfg.ReceiveEndpoint("worfair.financial.asaas-events", e =>
        {
            e.ConfigureConsumer<AsaasWebhookProcessorConsumer>(ctx);
            e.UseMessageRetry(r => r.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), 2.0));
        });
    });
});
```

**Fluxo garantido (single atomic operation):**

```
1. Consumidor: claim inbox → confirma Asaas → FinancialTransaction.Transition(...)
2. db.Add(transaction_events) + db.Add(ledger entries) + db.Add(outbox message)   ──┐
3. await db.SaveChangesAsync();                       commit único                    │
4. MassTransit OutboxDeliveryService publica no RabbitMQ (após commit) ◄────────────┘
5. ACK ao RabbitMQ (mensagem de entrada concluída)
```

Se o passo 3 falhar ⇒ NACK/retry (nenhum evento foi publicado — nada
inconsistente). Se a entrega do outbox falhar ⇒ o delivery service reenvia até
sucesso; consumidores são idempotentes.

## 4. Trilha de auditoria imutável

| Tabela | Registro | Imutabilidade |
| ------ | -------- | ------------- |
| `financial.asaas_webhook_inbox` | payload íntegro + recebido/processado | UPDATE/DELETE bloqueados p/ app (só via função) |
| `financial.transaction_events` | cada transição (from, to, trigger, provider_event_id) | append-only (sem UPDATE/DELETE) |
| `financial.ledger_entries` | cada lançamento contábil | append-only (sem UPDATE/DELETE) |
| `audit.audit_logs` | ações de usuários/sistema + hash chain | append-only + hash (SEC-04) |

`transaction_events` com `provider_event_id` **UNIQUE por transação** é a
segunda linha de idempotência (a primeira é o inbox): mesmo que dois caminhos
entreguem o mesmo evento, o agregado bloqueia (`ProviderEventId` já setado) e o
banco rejeita duplicidade.

```sql
CREATE TABLE financial.transaction_events (
    id               uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id        uuid NOT NULL REFERENCES tenancy.tenants (id),
    transaction_id   uuid NOT NULL REFERENCES financial.transactions (id) ON DELETE RESTRICT,
    from_status      smallint NOT NULL,
    to_status        smallint NOT NULL,
    trigger_code     smallint NOT NULL,
    provider_event_id uuid NULL,
    occurred_at      timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_transaction_provider_event UNIQUE (transaction_id, provider_event_id)
);
CREATE INDEX ix_transaction_events_tenant ON financial.transaction_events (tenant_id, occurred_at DESC);
```

## 5. Testes de resiliência (obrigatórios)

1. **Duplicação:** mesmo webhook entregue 2× (mesmo `event_id`) ⇒ 1 transição,
   1 evento no outbox, 1 lançamento no ledger.
2. **Fora de ordem:** `PAYMENT_RECEIVED` antes de `PAYMENT_CREATED` ⇒ falha de
   transição → retry/DLQ, nunca estado inválido (matriz bloqueia).
3. **Falha entre SaveChanges e publish:** simular broker fora do ar ⇒ outbox
   acumula; após broker voltar, mensagens são entregues (nenhuma perdida).
4. **Falha no publish antes do commit:** nada é publicado; sem estado fantasma.
5. **Concorrência:** dois consumidores com o mesmo item do inbox ⇒ `SKIP LOCKED`
   garante processamento único.
6. **Payload falsificado:** webhook com `payment.id` de outro tenant ⇒ confirmação
   no Asaas revela divergência; transição não aplicada; alerta em auditoria.