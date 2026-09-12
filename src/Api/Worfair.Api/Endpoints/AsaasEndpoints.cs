namespace Worfair.Api.Endpoints;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Worfair.Api.Authorization;
using Worfair.Api.Features.Financial;
using Worfair.Api.Features.Notifications;
using Worfair.Api.Infrastructure;
using Worfair.BuildingBlocks.Application.Security;
using Worfair.BuildingBlocks.Domain.ValueObjects;
using Worfair.Modules.Identity.Infrastructure.Persistence;

/// <summary>
/// Cobrança Asaas (sandbox) + webhook idempotente. Sem ApiKey, a criação de
/// cobrança responde 503 e as faturas seguem locais (comportamento atual).
/// </summary>
public static class AsaasEndpoints
{
    private static readonly HashSet<string> PaidStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "RECEIVED", "CONFIRMED" };

    private static readonly HashSet<string> BillingTypes =
        new(StringComparer.OrdinalIgnoreCase) { "PIX", "BOLETO", "CREDIT_CARD", "DEBIT_CARD" };

    public static IEndpointRouteBuilder MapAsaasEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/financial").WithTags("Financial · Asaas");

        group.MapPost("/invoices/{invoiceId:guid}/charge",
            async (Guid invoiceId, AsaasChargeRequest request, FinancialDbContext db,
                IdentityDbContext identity, IOptions<AsaasOptions> asaasOptions,
                IAsaasGateway gateway, ICurrentUser user, CancellationToken ct) =>
            {
                if (user.UserId is not { } clientId) return Results.Unauthorized();
                if (!asaasOptions.Value.IsConfigured)
                    return Results.Problem(
                        "Asaas não configurado: defina ASAAS_API_KEY (sandbox) no backend.",
                        statusCode: 503);

                var invoice = await db.Invoices.FirstOrDefaultAsync(
                    x => x.Id == invoiceId && x.ClientUserId == clientId, ct);
                if (invoice is null) return Results.NotFound(new { message = "Invoice not found." });
                if (invoice.Status != FinancialStatus.Issued)
                    return Results.Conflict(new { message = "Somente faturas em aberto podem ser cobradas." });

                var existing = await db.AsaasPayments
                    .FirstOrDefaultAsync(x => x.InvoiceId == invoiceId, ct);
                if (existing is not null)
                    return Results.Ok(ToChargeResponse(invoiceId, existing, splitApplied: false));

                var billingType = string.IsNullOrWhiteSpace(request.BillingType)
                    ? "PIX" : request.BillingType.Trim().ToUpperInvariant();
                if (!BillingTypes.Contains(billingType))
                    return Results.BadRequest(new { message = "BillingType inválido (PIX, BOLETO, CREDIT_CARD)." });

                var digits = new string((request.CpfCnpj ?? "").Where(char.IsDigit).ToArray());
                if (digits.Length is not (11 or 14))
                    return Results.BadRequest(new { message = "Informe o CPF (11) ou CNPJ (14) do pagador." });

                var identityUser = await identity.Users
                    .FirstOrDefaultAsync(u => u.Id == clientId, ct);
                var customerName = string.IsNullOrWhiteSpace(request.CustomerName)
                    ? identityUser?.FullName ?? "Cliente Worfair"
                    : request.CustomerName.Trim();
                var customerEmail = identityUser?.Email.Value ?? "pagador@worfair.local";

                var dueDays = request.DueDateDays is > 0 and <= 30 ? request.DueDateDays.Value : 3;
                var tenantId = new TenantId(invoice.TenantId.Value);

                try
                {
                    var customer = await gateway.CreateCustomerAsync(
                        customerName, customerEmail, digits, ct);

                    IReadOnlyList<AsaasSplitRequest>? split = null;
                    var splitApplied = false;
                    if (!string.IsNullOrWhiteSpace(asaasOptions.Value.SplitWalletId))
                    {
                        split = [new AsaasSplitRequest(
                            asaasOptions.Value.SplitWalletId!.Trim(),
                            FinancialInvoice.PlatformFeeRate * 100)];
                        splitApplied = true;
                    }

                    var payment = await gateway.CreatePaymentAsync(new AsaasPaymentRequest(
                        customer.Id,
                        billingType,
                        invoice.TotalAmount,
                        DateOnly.FromDateTime(DateTime.UtcNow.AddDays(dueDays)).ToString("yyyy-MM-dd"),
                        $"Worfair: {invoice.Description}"[..Math.Min(140, $"Worfair: {invoice.Description}".Length)],
                        $"worfair:{tenantId.Value}:{invoice.Id}",
                        split), ct);

                    var record = AsaasPayment.Create(invoice.Id, payment.Id, customer.Id,
                        billingType, invoice.TotalAmount, payment.InvoiceUrl, payment.Status);
                    await db.AsaasPayments.AddAsync(record, ct);
                    await db.SaveChangesAsync(ct);

                    return Results.Ok(ToChargeResponse(invoiceId, record, splitApplied));
                }
                catch (InvalidOperationException ex)
                {
                    return Results.Problem(ex.Message, statusCode: 502);
                }
            })
            .WithName("ChargeInvoiceViaAsaas")
            .RequireAuthorization(SecurityPolicies.InvoiceIssue)
            .WithSummary("Cria cobrança Asaas (total com 15%) com split opcional da taxa.");

        group.MapGet("/invoices/{invoiceId:guid}/payment",
            async (Guid invoiceId, FinancialDbContext db, IOptions<AsaasOptions> asaasOptions,
                IAsaasGateway gateway, ICurrentUser user, CancellationToken ct) =>
            {
                if (user.UserId is not { } userId) return Results.Unauthorized();
                var invoice = await db.Invoices.FirstOrDefaultAsync(
                    x => x.Id == invoiceId
                        && (x.ClientUserId == userId || x.ProviderUserId == userId), ct);
                if (invoice is null) return Results.NotFound();

                var record = await db.AsaasPayments
                    .FirstOrDefaultAsync(x => x.InvoiceId == invoiceId, ct);
                if (record is null) return Results.NotFound(new { message = "Sem cobrança Asaas." });

                if (asaasOptions.Value.IsConfigured)
                {
                    try
                    {
                        var live = await gateway.GetPaymentAsync(record.AsaasPaymentId, ct);
                        record.SyncStatus(live.Status);
                        await db.SaveChangesAsync(ct);
                    }
                    catch (InvalidOperationException)
                    {
                        // Mantém o último status conhecido.
                    }
                }

                return Results.Ok(ToChargeResponse(invoiceId, record, splitApplied: false));
            })
            .WithName("GetInvoiceAsaasPayment")
            .RequireAuthorization(SecurityPolicies.ProposalRead)
            .WithSummary("Status da cobrança Asaas da fatura (sincroniza quando configurado).");

        // Webhook do Asaas: SEM JWT (chamado pelo Asaas) — autentica pelo token
        // configurado no painel (header asaas-access-token).
        app.MapPost("/api/financial/asaas/webhook",
            async (AsaasWebhookPayload payload, HttpContext http, FinancialDbContext db,
                IOptions<AsaasOptions> asaasOptions, CancellationToken ct) =>
            {
                var configured = asaasOptions.Value.WebhookToken;
                var received = http.Request.Headers["asaas-access-token"].FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(configured))
                {
                    if (string.IsNullOrWhiteSpace(received) || received != configured)
                        return Results.Unauthorized();
                }
                else
                {
                    Console.WriteLine("[Asaas] Webhook sem ASAAS_WEBHOOK_TOKEN configurado — aceito (DEV).");
                }

                if (payload?.Payment is null || string.IsNullOrWhiteSpace(payload.Event))
                    return Results.BadRequest();

                var (tenantId, invoiceId) = ParseReference(payload.Payment.ExternalReference);
                if (tenantId is null || invoiceId is null)
                    return Results.Ok(new { received = true, applied = false });

                // Escrita elevada (R-04): o webhook chega sem contexto; fixa o
                // tenant da fatura no escopo da TRANSAÇÃO e torna o processamento
                // idempotente pela chave (event, payment, status).
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                await db.Database.ExecuteSqlRawAsync(
                    "SELECT set_config('app.tenant_id', {0}, true)",
                    [tenantId.Value.ToString()], ct);

                var raw = JsonSerializer.Serialize(payload);
                var already = await db.AsaasWebhookEvents.AnyAsync(x =>
                    x.Event == payload.Event.Trim().ToUpperInvariant()
                    && x.AsaasPaymentId == payload.Payment.Id
                    && x.PaymentStatus == payload.Payment.Status.Trim().ToUpperInvariant(), ct);
                if (already)
                {
                    await tx.RollbackAsync(ct);
                    return Results.Ok(new { received = true, duplicate = true });
                }

                var invoice = await db.Invoices
                    .FirstOrDefaultAsync(x => x.Id == invoiceId.Value, ct);
                if (invoice is not null)
                {
                    var stored = await db.AsaasPayments
                        .FirstOrDefaultAsync(x => x.InvoiceId == invoiceId.Value, ct);
                    stored?.SyncStatus(payload.Payment.Status);

                    if (PaidStatuses.Contains(payload.Payment.Status))
                    {
                        invoice.Settle();
                        db.Notifications.Add(UserNotification.Create(invoice.ClientUserId,
                            "Pagamento confirmado",
                            $"A fatura {invoice.Id} foi confirmada no Asaas.",
                            "financial.payment", NotificationPriority.Normal));
                        db.Notifications.Add(UserNotification.Create(invoice.ProviderUserId,
                            "Pagamento recebido",
                            $"A fatura {invoice.Id} foi paga. Valor a receber: {invoice.Amount:0.00} {invoice.Currency}.",
                            "financial.payment", NotificationPriority.Normal));
                    }
                    else
                    {
                        db.Notifications.Add(UserNotification.Create(invoice.ClientUserId,
                            $"Cobrança {payload.Payment.Status}",
                            $"A fatura {invoice.Id} está com status {payload.Payment.Status} no Asaas.",
                            "financial.payment", NotificationPriority.Low));
                    }
                }

                db.AsaasWebhookEvents.Add(AsaasWebhookEvent.Create(
                    payload.Event, payload.Payment.Id, payload.Payment.Status, invoiceId, raw));
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                return Results.Ok(new { received = true, applied = invoice is not null });
            })
            .AllowAnonymous()
            .WithName("AsaasWebhook")
            .WithSummary("Webhook do Asaas (idempotente; liquida a fatura em RECEIVED/CONFIRMED).")
            .WithTags("Financial · Asaas");

        return app;
    }

    private static AsaasChargeResponse ToChargeResponse(
        Guid invoiceId, AsaasPayment record, bool splitApplied) => new(
        invoiceId, record.AsaasPaymentId, record.Status, record.ChargedValue,
        record.CheckoutUrl, splitApplied);

    private static (Guid? TenantId, Guid? InvoiceId) ParseReference(string? reference)
    {
        // Formato emitido na cobrança: worfair:{tenantId}:{invoiceId}
        var parts = (reference ?? "").Split(':');
        if (parts.Length == 3 && parts[0] == "worfair"
            && Guid.TryParse(parts[1], out var tenantId)
            && Guid.TryParse(parts[2], out var invoiceId))
            return (tenantId, invoiceId);
        return (null, null);
    }
}
