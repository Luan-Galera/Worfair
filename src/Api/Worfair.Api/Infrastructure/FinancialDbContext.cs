namespace Worfair.Api.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Worfair.BuildingBlocks.Domain.Tenancy;
using Worfair.BuildingBlocks.Domain.ValueObjects;
using Worfair.BuildingBlocks.Infrastructure.Persistence.Outbox;
using Worfair.BuildingBlocks.Infrastructure.Persistence.Tenant;
using Worfair.Api.Features.Financial;
using Worfair.Api.Features.Notifications;
using Worfair.Api.Features.Communication;

public sealed class FinancialDbContext(DbContextOptions<FinancialDbContext> options, ITenantProvider tenantProvider)
    : DbContext(options), ITenantFilteredDbContext
{
    public DbSet<FinancialInvoice> Invoices => Set<FinancialInvoice>();
    public DbSet<UserNotification> Notifications => Set<UserNotification>();
    public DbSet<ConversationMessage> Messages => Set<ConversationMessage>();
    public DbSet<PaymentDispute> Disputes => Set<PaymentDispute>();
    public DbSet<AsaasPayment> AsaasPayments => Set<AsaasPayment>();
    public DbSet<AsaasWebhookEvent> AsaasWebhookEvents => Set<AsaasWebhookEvent>();
    public TenantId? CurrentTenantId => tenantProvider.TenantId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<FinancialInvoice>(builder =>
        {
            builder.ToTable("invoices", "financial");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasConversion(x => x.Value, x => new TenantId(x));
            builder.Property(x => x.ClientUserId).HasColumnName("client_user_id").IsRequired();
            builder.Property(x => x.ProviderUserId).HasColumnName("provider_user_id").IsRequired();
            builder.Property(x => x.ProviderCompanyId).HasColumnName("provider_company_id");
            builder.Property(x => x.Amount).HasColumnName("amount").HasColumnType("numeric(12,2)");
            builder.Property(x => x.PlatformFeeAmount).HasColumnName("platform_fee_amount").HasColumnType("numeric(12,2)");
            builder.Property(x => x.TotalAmount).HasColumnName("total_amount").HasColumnType("numeric(12,2)");
            builder.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
            builder.Property(x => x.Description).HasColumnName("description").IsRequired();
            builder.Property(x => x.Status).HasColumnName("status").HasConversion<int>();
            builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at");
            builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at");
            builder.HasIndex(x => new { x.TenantId, x.Status });
        });
        modelBuilder.Entity<ConversationMessage>(builder =>
        {
            builder.ToTable("messages", "financial"); builder.HasKey(x => x.Id);
            builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasConversion(x => x.Value, x => new TenantId(x));
            builder.Property(x => x.InvoiceId).HasColumnName("invoice_id");
            builder.Property(x => x.SenderUserId).HasColumnName("sender_user_id");
            builder.Property(x => x.RecipientUserId).HasColumnName("recipient_user_id");
            builder.Property(x => x.Body).HasColumnName("body").IsRequired();
            builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at");
            builder.HasIndex(x => new { x.TenantId, x.InvoiceId, x.CreatedAtUtc });
        });
        modelBuilder.Entity<PaymentDispute>(builder =>
        {
            builder.ToTable("payment_disputes", "financial"); builder.HasKey(x => x.Id);
            builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasConversion(x => x.Value, x => new TenantId(x));
            builder.Property(x => x.InvoiceId).HasColumnName("invoice_id");
            builder.Property(x => x.OpenedByUserId).HasColumnName("opened_by_user_id");
            builder.Property(x => x.SuperAdminUserId).HasColumnName("super_admin_user_id");
            builder.Property(x => x.Reason).HasColumnName("reason").IsRequired();
            builder.Property(x => x.Status).HasColumnName("status").HasConversion<int>();
            builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at");
            builder.Property(x => x.ResolvedAtUtc).HasColumnName("resolved_at");
            builder.HasIndex(x => new { x.TenantId, x.InvoiceId });
        });
        modelBuilder.Entity<UserNotification>(builder =>
        {
            builder.ToTable("notifications", "financial");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasConversion(x => x.Value, x => new TenantId(x));
            builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
            builder.Property(x => x.Message).HasColumnName("message").IsRequired();
            builder.Property(x => x.Category).HasColumnName("category").HasMaxLength(80).IsRequired();
            builder.Property(x => x.Priority).HasColumnName("priority").HasConversion<int>();
            builder.Property(x => x.IsRead).HasColumnName("is_read");
            builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at");
            builder.HasIndex(x => new { x.TenantId, x.UserId, x.IsRead });
        });
        modelBuilder.Entity<AsaasPayment>(builder =>
        {
            builder.ToTable("asaas_payments", "financial");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasConversion(x => x.Value, x => new TenantId(x));
            builder.Property(x => x.InvoiceId).HasColumnName("invoice_id").IsRequired();
            builder.Property(x => x.AsaasPaymentId).HasColumnName("asaas_payment_id").HasMaxLength(60).IsRequired();
            builder.Property(x => x.AsaasCustomerId).HasColumnName("asaas_customer_id").HasMaxLength(60);
            builder.Property(x => x.BillingType).HasColumnName("billing_type").HasMaxLength(20).IsRequired();
            builder.Property(x => x.ChargedValue).HasColumnName("charged_value").HasColumnType("numeric(12,2)");
            builder.Property(x => x.CheckoutUrl).HasColumnName("checkout_url").HasMaxLength(500);
            builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(30).IsRequired();
            builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at");
            builder.Property(x => x.LastSyncedAtUtc).HasColumnName("last_synced_at");
            builder.HasIndex(x => x.InvoiceId).IsUnique();
            builder.HasIndex(x => x.AsaasPaymentId).IsUnique();
        });
        modelBuilder.Entity<AsaasWebhookEvent>(builder =>
        {
            builder.ToTable("asaas_webhook_events", "financial");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasConversion(x => x.Value, x => new TenantId(x));
            builder.Property(x => x.Event).HasColumnName("event").HasMaxLength(60).IsRequired();
            builder.Property(x => x.AsaasPaymentId).HasColumnName("asaas_payment_id").HasMaxLength(60).IsRequired();
            builder.Property(x => x.PaymentStatus).HasColumnName("payment_status").HasMaxLength(30).IsRequired();
            builder.Property(x => x.InvoiceId).HasColumnName("invoice_id");
            builder.Property(x => x.Payload).HasColumnName("payload").HasColumnType("text").IsRequired();
            builder.Property(x => x.ReceivedAtUtc).HasColumnName("received_at");
            builder.HasIndex(x => new { x.Event, x.AsaasPaymentId, x.PaymentStatus }).IsUnique();
        });
        modelBuilder.AddOutboxMessages("financial");
        modelBuilder.ApplyTenantFiltersFromProvider(this);
    }
}

public sealed class FinancialUnitOfWork(FinancialDbContext db) : Worfair.BuildingBlocks.Domain.Abstractions.IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}
