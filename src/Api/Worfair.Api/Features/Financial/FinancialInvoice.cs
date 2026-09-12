namespace Worfair.Api.Features.Financial;

using Worfair.BuildingBlocks.Domain.Tenancy;
using Worfair.BuildingBlocks.Domain.ValueObjects;

public enum FinancialStatus { Issued = 1, Paid = 2, Reversed = 3 }

public sealed class FinancialInvoice : ITenantEntity
{
    public const decimal PlatformFeeRate = 0.15m;
    private FinancialInvoice() { }

    public Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public Guid ClientUserId { get; private set; }
    public Guid ProviderUserId { get; private set; }
    public Guid? ProviderCompanyId { get; private set; }
    public decimal Amount { get; private set; }
    public decimal PlatformFeeAmount { get; private set; }
    public decimal TotalAmount { get; private set; }
    public string Currency { get; private set; } = "BRL";
    public string Description { get; private set; } = string.Empty;
    public FinancialStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    public void SetTenantId(TenantId tenantId) => TenantId = tenantId;

    public static FinancialInvoice Create(Guid clientUserId, Guid providerUserId, Guid? providerCompanyId,
        decimal amount, string? currency, string description)
    {
        return new FinancialInvoice
        {
            Id = Guid.NewGuid(), ClientUserId = clientUserId, ProviderUserId = providerUserId,
            ProviderCompanyId = providerCompanyId, Amount = amount,
            PlatformFeeAmount = decimal.Round(amount * PlatformFeeRate, 2, MidpointRounding.AwayFromZero),
            TotalAmount = decimal.Round(amount * (1 + PlatformFeeRate), 2, MidpointRounding.AwayFromZero),
            Currency = (currency ?? "BRL").Trim().ToUpperInvariant(), Description = description.Trim(),
            Status = FinancialStatus.Issued, CreatedAtUtc = DateTime.UtcNow
        };
    }

    public void Settle()
    {
        if (Status == FinancialStatus.Issued)
        {
            Status = FinancialStatus.Paid;
            UpdatedAtUtc = DateTime.UtcNow;
        }
    }
}

public sealed record FinancialInvoiceRequest(Guid ProviderUserId, Guid? ProviderCompanyId, decimal Amount, string? Currency, string Description);
public sealed record FinancialInvoiceResponse(Guid Id, Guid ClientUserId, Guid ProviderUserId, Guid? ProviderCompanyId, decimal Amount, decimal PlatformFeeAmount, decimal TotalAmount, string Currency, string Description, string Status, DateTime CreatedAtUtc, DateTime? UpdatedAtUtc);
