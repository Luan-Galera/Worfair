namespace Worfair.Api.Features.Communication;

using Worfair.BuildingBlocks.Domain.Tenancy;
using Worfair.BuildingBlocks.Domain.ValueObjects;

public enum DisputeStatus { Open = 1, UnderMediation = 2, Resolved = 3, Rejected = 4 }

public sealed class ConversationMessage : ITenantEntity
{
    private ConversationMessage() { }
    public Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public Guid? InvoiceId { get; private set; }
    public Guid SenderUserId { get; private set; }
    public Guid RecipientUserId { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public void SetTenantId(TenantId tenantId) => TenantId = tenantId;
    public static ConversationMessage Create(Guid? invoiceId, Guid senderUserId, Guid recipientUserId, string body) => new()
    {
        Id = Guid.NewGuid(), InvoiceId = invoiceId, SenderUserId = senderUserId,
        RecipientUserId = recipientUserId, Body = body.Trim(), CreatedAtUtc = DateTime.UtcNow
    };
}

public sealed class PaymentDispute : ITenantEntity
{
    private PaymentDispute() { }
    public Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public Guid InvoiceId { get; private set; }
    public Guid OpenedByUserId { get; private set; }
    public Guid? SuperAdminUserId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public DisputeStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ResolvedAtUtc { get; private set; }
    public void SetTenantId(TenantId tenantId) => TenantId = tenantId;
    public void AssignMediator(Guid userId) { SuperAdminUserId = userId; Status = DisputeStatus.UnderMediation; }
    public void Resolve(bool accepted) { Status = accepted ? DisputeStatus.Resolved : DisputeStatus.Rejected; ResolvedAtUtc = DateTime.UtcNow; }
    public static PaymentDispute Create(Guid invoiceId, Guid openedByUserId, string reason) => new()
    {
        Id = Guid.NewGuid(), InvoiceId = invoiceId, OpenedByUserId = openedByUserId,
        Reason = reason.Trim(), Status = DisputeStatus.Open, CreatedAtUtc = DateTime.UtcNow
    };
}

public sealed record ConversationMessageRequest(Guid RecipientUserId, Guid? InvoiceId, string Body);
public sealed record ConversationMessageResponse(Guid Id, Guid? InvoiceId, Guid SenderUserId, Guid RecipientUserId, string Body, DateTime CreatedAtUtc);
public sealed record PaymentDisputeRequest(Guid InvoiceId, string Reason);
public sealed record PaymentDisputeResponse(Guid Id, Guid InvoiceId, Guid OpenedByUserId, Guid? SuperAdminUserId, string Reason, string Status, DateTime CreatedAtUtc, DateTime? ResolvedAtUtc);
