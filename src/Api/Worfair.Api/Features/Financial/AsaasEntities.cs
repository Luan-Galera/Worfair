namespace Worfair.Api.Features.Financial;

using Worfair.BuildingBlocks.Domain.Tenancy;
using Worfair.BuildingBlocks.Domain.ValueObjects;

/// <summary>Cobrança Asaas vinculada a uma fatura (valor total = valor + 15%).</summary>
public sealed class AsaasPayment : ITenantEntity
{
    private AsaasPayment() { }

    public Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public Guid InvoiceId { get; private set; }
    public string AsaasPaymentId { get; private set; } = string.Empty;
    public string? AsaasCustomerId { get; private set; }
    public string BillingType { get; private set; } = "PIX";
    public decimal ChargedValue { get; private set; }
    public string? CheckoutUrl { get; private set; }
    public string Status { get; private set; } = "PENDING";
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? LastSyncedAtUtc { get; private set; }

    public void SetTenantId(TenantId tenantId) => TenantId = tenantId;

    public static AsaasPayment Create(
        Guid invoiceId, string asaasPaymentId, string? asaasCustomerId,
        string billingType, decimal chargedValue, string? checkoutUrl, string status) => new()
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoiceId,
            AsaasPaymentId = asaasPaymentId.Trim(),
            AsaasCustomerId = asaasCustomerId,
            BillingType = billingType.Trim().ToUpperInvariant(),
            ChargedValue = chargedValue,
            CheckoutUrl = checkoutUrl,
            Status = status.Trim().ToUpperInvariant(),
            CreatedAtUtc = DateTime.UtcNow
        };

    public void SyncStatus(string status)
    {
        Status = status.Trim().ToUpperInvariant();
        LastSyncedAtUtc = DateTime.UtcNow;
    }
}

/// <summary>
/// Evento de webhook Asaas já processado (idempotência: redelivery retorna 200
/// sem reaplicar). Chave lógica (event, payment, status).
/// </summary>
public sealed class AsaasWebhookEvent : ITenantEntity
{
    private AsaasWebhookEvent() { }

    public Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public string Event { get; private set; } = string.Empty;
    public string AsaasPaymentId { get; private set; } = string.Empty;
    public string PaymentStatus { get; private set; } = string.Empty;
    public Guid? InvoiceId { get; private set; }
    public string Payload { get; private set; } = string.Empty;
    public DateTime ReceivedAtUtc { get; private set; }

    public void SetTenantId(TenantId tenantId) => TenantId = tenantId;

    public static AsaasWebhookEvent Create(
        string @event, string asaasPaymentId, string paymentStatus,
        Guid? invoiceId, string payload) => new()
        {
            Id = Guid.NewGuid(),
            Event = @event.Trim().ToUpperInvariant(),
            AsaasPaymentId = asaasPaymentId.Trim(),
            PaymentStatus = paymentStatus.Trim().ToUpperInvariant(),
            InvoiceId = invoiceId,
            Payload = payload,
            ReceivedAtUtc = DateTime.UtcNow
        };
}

public sealed record AsaasChargeRequest(
    string? BillingType,
    string CpfCnpj,
    string? CustomerName,
    int? DueDateDays);

public sealed record AsaasChargeResponse(
    Guid InvoiceId,
    string AsaasPaymentId,
    string Status,
    decimal ChargedValue,
    string? CheckoutUrl,
    bool SplitApplied);
