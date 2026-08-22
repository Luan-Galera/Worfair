# 02 — Máquina de Estados Financeira

## 1. Estados (espinha dorsal)

```
PENDING ──► PAYMENT_CREATED ──► PAYMENT_RECEIVED ──► FUNDS_AVAILABLE ──► RELEASE_REQUESTED ──► RELEASED
   │              │                    │                    │                    │
   │              └──► PAYMENT_FAILED  └──► PAYMENT_REFUNDED └──► PAYMENT_REFUNDED│
   └──► PAYMENT_FAILED                                                          └──► RELEASE_FAILED ──► RELEASE_REQUESTED (retry)
```

| # | Estado | Significado |
| - | ------ | ----------- |
| 1 | `PENDING` | transação criada no domínio, cobrança ainda não criada no Asaas |
| 2 | `PAYMENT_CREATED` | cobrança criada no Asaas (resposta `POST /payments`) |
| 3 | `PAYMENT_RECEIVED` | webhook `PAYMENT_RECEIVED`/`PAYMENT_CONFIRMED` **confirmado no provedor** |
| 4 | `FUNDS_AVAILABLE` | splits liquidados (`GET /payments/{id}/splits` → todos `SPLIT_RECEIVED`); retenção disponível |
| 5 | `RELEASE_REQUESTED` | liberação solicitada (entrega/contrato concluído) e Transfer Asaas criado |
| 6 | `RELEASED` | `TRANSFER_DONE` confirmado; retenção creditada ao prestador |
| 7 | `PAYMENT_FAILED` | cobrança recusada/cancelada/vencida antes do recebimento (terminal) |
| 8 | `PAYMENT_REFUNDED` | estorno após recebimento (terminal; reversão via ledger) |
| 9 | `RELEASE_FAILED` | Transfer falhou (`TRANSFER_FAILED`) — reentrante para `RELEASE_REQUESTED` |

## 2. Gatilhos (origem de cada transição)

| Gatilho | Origem | Exigência |
| ------- | ------ | --------- |
| `PaymentCreated` | comando do domínio | resposta do Asaas `POST /payments` |
| `PaymentReceived` | webhook `PAYMENT_RECEIVED` | **confirmação** `GET /payments/{id}` status `RECEIVED` |
| `FundsAvailable` | reconciliação de splits | `GET /payments/{id}/splits` → todos `SPLIT_RECEIVED` |
| `ReleaseRequested` | comando do domínio | regra de negócio (contrato concluído) + `POST /transfers` |
| `Released` | webhook `TRANSFER_DONE` | confirmação `GET /transfers/{id}` |
| `PaymentFailed` | webhook/erro de criação | status Asaas `REFUSED`/`CANCELLED`/vencido |
| `PaymentRefunded` | webhook `PAYMENT_REFUNDED` | confirmação no provedor |
| `ReleaseFailed` | webhook `TRANSFER_FAILED` | confirmação no provedor |

## 3. Matriz de transições (regra única de domínio)

| De \ Para | 2 Created | 3 Received | 4 Available | 5 ReqRelease | 6 Released | 7 Failed | 8 Refunded | 9 RelFailed |
| --------- | :-------: | :--------: | :---------: | :----------: | :--------: | :------: | :--------: | :---------: |
| 1 PENDING | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ | ❌ | ❌ |
| 2 PAYMENT_CREATED | — | ✅ | ❌ | ❌ | ❌ | ✅ | ❌ | ❌ |
| 3 PAYMENT_RECEIVED | ❌ | — | ✅ | ❌ | ❌ | ❌ | ✅ | ❌ |
| 4 FUNDS_AVAILABLE | ❌ | ❌ | — | ✅ | ❌ | ❌ | ✅ | ❌ |
| 5 RELEASE_REQUESTED | ❌ | ❌ | ❌ | — | ✅ | ❌ | ❌ | ✅ |
| 6 RELEASED | ❌ | ❌ | ❌ | ❌ | — | ❌ | ❌ | ❌ |
| 7 PAYMENT_FAILED | ❌ | ❌ | ❌ | ❌ | ❌ | — | ❌ | ❌ |
| 8 PAYMENT_REFUNDED | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | — | ❌ |
| 9 RELEASE_FAILED | ❌ | ❌ | ❌ | ❌ | ✅ | ❌ | ❌ | — |

**Proibidas por construção (exemplos):** `RELEASED → PAYMENT_RECEIVED`,
`PAYMENT_RECEIVED → RELEASE_REQUESTED` (falta `FUNDS_AVAILABLE`),
`PENDING → RELEASED`, `PAYMENT_REFUNDED → FUNDS_AVAILABLE`.

> `PAYMENT_REFUNDED` após `RELEASED` (dinheiro já transferido) **não é transição
> de status** — é uma **reversão por ledger** (estorno com `entry_type =
> Reversal`, SEC-04) com transação financeira própria.

## 4. Implementação de domínio (código)

```csharp
// Financial.Domain/Aggregates/FinancialTransaction/FinancialTransactionStatus.cs
public enum FinancialTransactionStatus
{
    Pending = 1, PaymentCreated = 2, PaymentReceived = 3, FundsAvailable = 4,
    ReleaseRequested = 5, Released = 6, PaymentFailed = 7, PaymentRefunded = 8, ReleaseFailed = 9
}

// Financial.Domain/Aggregates/FinancialTransaction/FinancialTransactionTrigger.cs
public enum FinancialTransactionTrigger
{
    PaymentCreated = 1, PaymentReceived = 2, FundsAvailable = 3,
    ReleaseRequested = 4, Released = 5, PaymentFailed = 6,
    PaymentRefunded = 7, ReleaseFailed = 8
}

// Financial.Domain/Aggregates/FinancialTransaction/FinancialTransaction.cs
public sealed class FinancialTransaction : AggregateRoot<FinancialTransactionId>
{
    private static readonly IReadOnlyDictionary<
        (FinancialTransactionStatus From, FinancialTransactionTrigger Trigger),
        FinancialTransactionStatus> Transitions = new Dictionary<...>
    {
        [(Pending, PaymentCreated)] = PaymentCreated,
        [(Pending, PaymentFailed)] = PaymentFailed,
        [(PaymentCreated, PaymentReceived)] = PaymentReceived,
        [(PaymentCreated, PaymentFailed)] = PaymentFailed,
        [(PaymentReceived, FundsAvailable)] = FundsAvailable,
        [(PaymentReceived, PaymentRefunded)] = PaymentRefunded,
        [(FundsAvailable, ReleaseRequested)] = ReleaseRequested,
        [(FundsAvailable, PaymentRefunded)] = PaymentRefunded,
        [(ReleaseRequested, Released)] = Released,
        [(ReleaseRequested, ReleaseFailed)] = ReleaseFailed,
        [(ReleaseFailed, ReleaseRequested)] = ReleaseRequested
    };

    public FinancialTransactionId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public FinancialTransactionStatus Status { get; private set; }
    public Money Amount { get; private set; }
    public string? AsaasPaymentId { get; private set; }
    public string? AsaasTransferId { get; private set; }
    public Guid? ProviderEventId { get; private set; }        // idempotência por evento do provedor
    public DateTime? ReceivedAtUtc { get; private set; }
    public DateTime? ReleasedAtUtc { get; private set; }

    private FinancialTransaction(TenantId tenantId, Money amount) { ... }

    public static Result<FinancialTransaction> Create(TenantId tenantId, Money amount)
    {
        if (tenantId == default) return Result.Failure(FinancialTransactionErrors.TenantRequired);
        if (amount.Amount <= 0) return Result.Failure(FinancialTransactionErrors.InvalidAmount);
        return new FinancialTransaction(tenantId, amount);
    }

    public Result Transition(
        FinancialTransactionTrigger trigger,
        Guid? providerEventId = null,
        DateTime? occurredAtUtc = null)
    {
        // 1. Transição ilegal é bloqueada por regra de domínio (sem exceção de fluxo)
        if (!Transitions.TryGetValue((Status, trigger), out var next))
            return Result.Failure(FinancialTransactionErrors.InvalidTransition(Status, trigger));

        // 2. Evento do provedor não pode ser aplicado duas vezes (idempotência no agregado)
        if (providerEventId is not null && ProviderEventId == providerEventId)
            return Result.Success();

        // 3. Gatilhos originados por webhook exigem evento confirmado do provedor
        if (RequiresProviderConfirmation(trigger) && providerEventId is null)
            return Result.Failure(FinancialTransactionErrors.ProviderEventRequired);

        Apply(next, trigger, providerEventId, occurredAtUtc);
        RaiseDomainEvent(new FinancialTransactionStatusChangedDomainEvent(
            Id, TenantId, Status, next, trigger, providerEventId));
        return Result.Success();
    }

    private static bool RequiresProviderConfirmation(FinancialTransactionTrigger trigger)
        => trigger is FinancialTransactionTrigger.PaymentReceived
            or FinancialTransactionTrigger.FundsAvailable
            or FinancialTransactionTrigger.Released
            or FinancialTransactionTrigger.PaymentRefunded
            or FinancialTransactionTrigger.ReleaseFailed;
}

// Financial.Domain/Errors/FinancialTransactionErrors.cs
public static class FinancialTransactionErrors
{
    public static Error InvalidTransition(FinancialTransactionStatus from, FinancialTransactionTrigger trigger)
        => new("FinancialTransaction.InvalidTransition",
            $"Transição {from} → {trigger} é proibida pela máquina de estados.");

    public static Error ProviderEventRequired
        => new("FinancialTransaction.ProviderEventRequired",
            "Transição exige evento confirmado do provedor de pagamento.");

    public static Error InvalidAmount
        => new("FinancialTransaction.InvalidAmount", "Valor deve ser maior que zero.");

    public static Error TenantRequired
        => new("FinancialTransaction.TenantRequired", "Transação exige um tenant.");
}
```

**Código que chama (handler):** apenas o agregado decide. O handler carrega a
transação pelo ID (filtrado por tenant via query filter), chama `Transition` e
persiste — jamais seta `Status` diretamente.

## 5. Reforço no banco (defesa em profundidade, SEC-04)

Nenhum endpoint altera status diretamente: a tabela `financial.transactions` é
**append-only para a aplicação** (sem UPDATE/DELETE via GRANT) e a transição é
executada pela função dedicada:

```sql
-- financial.transactions / financial.transaction_events (novas tabelas, ver doc 04 de resiliência)
REVOKE UPDATE, DELETE ON financial.transactions, financial.transaction_events FROM worfair_app;
GRANT SELECT, INSERT ON financial.transactions, financial.transaction_events TO worfair_app;

CREATE FUNCTION financial.transition_transaction(
    p_transaction_id uuid, p_trigger smallint, p_provider_event_id uuid)
RETURNS void
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = financial, pg_temp
AS $$
DECLARE
    v_tenant uuid; v_from smallint; v_to smallint;
BEGIN
    SELECT tenant_id, status INTO v_tenant, v_from
    FROM financial.transactions WHERE id = p_transaction_id;
    IF v_tenant IS NULL THEN RAISE EXCEPTION 'Transação inexistente'; END IF;

    -- Espelho da matriz de domínio (manter em sincronia via testes de contrato)
    SELECT to_status INTO v_to FROM financial.transition_rules
    WHERE from_status = v_from AND trigger_code = p_trigger;

    IF v_to IS NULL THEN
        RAISE EXCEPTION 'Transição % -> trigger % proibida', v_from, p_trigger;
    END IF;

    UPDATE financial.transactions SET status = v_to, updated_at = now()
    WHERE id = p_transaction_id;

    INSERT INTO financial.transaction_events
        (tenant_id, transaction_id, from_status, to_status, trigger_code,
         provider_event_id, occurred_at)
    VALUES (v_tenant, p_transaction_id, v_from, v_to, p_trigger,
            p_provider_event_id, now());
END; $$;
GRANT EXECUTE ON FUNCTION financial.transition_transaction(uuid, smallint, uuid) TO worfair_app;
```

> A matriz de domínio e a tabela `financial.transition_rules` são mantidas em
> sincronia por **teste de contrato** (CI): todas as transições válidas do
> agregado devem existir na tabela e vice-versa.

## 6. Transições e ledger (mesma transação)

Toda transição de estado dispara lançamentos no ledger (append-only):

| Transição | Ledger |
| --------- | ------ |
| `PAYMENT_RECEIVED` | crédito da cobrança na conta da plataforma |
| `FUNDS_AVAILABLE` | débito split prestador (já creditado via Asaas) + crédito retenção |
| `RELEASED` | débito retenção + crédito prestador |
| `PAYMENT_REFUNDED` | lançamento de reversão (estorno) |
| `RELEASE_FAILED` | nenhum lançamento (sem movimentação) |

Lançamentos e transição de status são gravados na **mesma transação de banco**
do Outbox (doc 04) — impossível ter evento publicado sem registro financeiro.