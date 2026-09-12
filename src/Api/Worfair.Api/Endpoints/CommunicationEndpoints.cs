namespace Worfair.Api.Endpoints;

using Microsoft.EntityFrameworkCore;
using Worfair.Api.Authorization;
using Worfair.Api.Features.Communication;
using Worfair.Api.Features.Notifications;
using Worfair.Api.Infrastructure;
using Worfair.BuildingBlocks.Application.Security;
using Worfair.BuildingBlocks.Domain.ValueObjects;
using Worfair.Modules.Identity.Infrastructure.Persistence;

public static class CommunicationEndpoints
{
    public static IEndpointRouteBuilder MapCommunicationEndpoints(this IEndpointRouteBuilder app)
    {
        var messages = app.MapGroup("/api/messages").WithTags("Messages");

        messages.MapGet("/invoices/{invoiceId:guid}", async (Guid invoiceId, FinancialDbContext db, IdentityDbContext identity, Worfair.Modules.Tenants.Infrastructure.Persistence.TenancyDbContext tenancy, ICurrentUser user, CancellationToken ct) =>
        {
            if (user.UserId is not { } userId) return Results.Unauthorized();
            var invoice = await db.Invoices.FirstOrDefaultAsync(x => x.Id == invoiceId &&
                (x.ClientUserId == userId || x.ProviderUserId == userId), ct);
            var isSuperAdmin = await IsSuperAdmin(identity, userId, ct);
            if (invoice is null && !isSuperAdmin) return Results.NotFound();

            // Mediação: o super admin enxerga a conversa inteira (leitura elevada,
            // pois o RLS esconderia as linhas no contexto global).
            if (isSuperAdmin && invoice is null)
            {
                var tenants = await FinancialElevatedRead.ListActiveTenantIdsAsync(tenancy, ct)
                    .ConfigureAwait(false);
                var found = await FinancialElevatedRead.FindInvoiceTenantAsync(
                    db, tenants, invoiceId, ct).ConfigureAwait(false);
                if (found is null) return Results.NotFound();
                var thread = await FinancialElevatedRead.QueryAcrossTenantsAsync(
                    db, [found.Value],
                    fdb => fdb.Messages.Where(x => x.InvoiceId == invoiceId)
                        .OrderBy(x => x.CreatedAtUtc).Select(x => new ConversationMessageResponse(
                            x.Id, x.InvoiceId, x.SenderUserId, x.RecipientUserId, x.Body, x.CreatedAtUtc))
                        .ToListAsync(ct), ct).ConfigureAwait(false);
                return Results.Ok(thread);
            }

            var result = await db.Messages.Where(x => x.InvoiceId == invoiceId &&
                    (isSuperAdmin || x.SenderUserId == userId || x.RecipientUserId == userId))
                .OrderBy(x => x.CreatedAtUtc).Select(x => new ConversationMessageResponse(
                    x.Id, x.InvoiceId, x.SenderUserId, x.RecipientUserId, x.Body, x.CreatedAtUtc))
                .ToListAsync(ct);
            return Results.Ok(result);
        }).RequireAuthorization(SecurityPolicies.SupportRead).WithName("ListInvoiceMessages");

        messages.MapPost(string.Empty, async (ConversationMessageRequest request, FinancialDbContext db, IdentityDbContext identity, Worfair.Modules.Tenants.Infrastructure.Persistence.TenancyDbContext tenancy, ICurrentUser user, CancellationToken ct) =>
        {
            if (user.UserId is not { } senderId) return Results.Unauthorized();
            if (senderId == request.RecipientUserId || string.IsNullOrWhiteSpace(request.Body)) return Results.BadRequest();

            // Mediação: o super admin pode falar na fatura em disputa (qualquer tenant).
            if (await IsSuperAdmin(identity, senderId, ct))
            {
                if (request.InvoiceId is not { } adminInvoiceId) return Results.BadRequest();
                var tenants = await FinancialElevatedRead.ListActiveTenantIdsAsync(tenancy, ct)
                    .ConfigureAwait(false);
                var tenant = await FinancialElevatedRead.FindInvoiceTenantAsync(
                    db, tenants, adminInvoiceId, ct).ConfigureAwait(false);
                if (tenant is null) return Results.NotFound();

                var adminMessage = ConversationMessage.Create(
                    adminInvoiceId, senderId, request.RecipientUserId, request.Body);
                adminMessage.SetTenantId(new TenantId(tenant.Value));

                await using var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
                await db.Database.ExecuteSqlRawAsync(
                    "SELECT set_config('app.tenant_id', {0}, true)",
                    [tenant.Value.ToString()], ct).ConfigureAwait(false);
                await db.Messages.AddAsync(adminMessage, ct);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct).ConfigureAwait(false);

                return Results.Ok(new ConversationMessageResponse(adminMessage.Id, adminMessage.InvoiceId,
                    adminMessage.SenderUserId, adminMessage.RecipientUserId, adminMessage.Body,
                    adminMessage.CreatedAtUtc));
            }

            if (request.InvoiceId is { } invoiceId)
            {
                var invoice = await db.Invoices.FirstOrDefaultAsync(x => x.Id == invoiceId &&
                    (x.ClientUserId == senderId || x.ProviderUserId == senderId) &&
                    (x.ClientUserId == request.RecipientUserId || x.ProviderUserId == request.RecipientUserId), ct);
                if (invoice is null) return Results.NotFound();
            }
            var message = ConversationMessage.Create(request.InvoiceId, senderId, request.RecipientUserId, request.Body);
            await db.Messages.AddAsync(message, ct);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new ConversationMessageResponse(message.Id, message.InvoiceId, message.SenderUserId,
                message.RecipientUserId, message.Body, message.CreatedAtUtc));
        }).RequireAuthorization(SecurityPolicies.MessageSend).WithName("SendMessage");

        var disputes = app.MapGroup("/api/financial/disputes").WithTags("Financial · Disputes");
        disputes.MapPost(string.Empty, async (PaymentDisputeRequest request, FinancialDbContext db,
            IdentityDbContext identity, ICurrentUser user, CancellationToken ct) =>
        {
            if (user.UserId is not { } openedBy) return Results.Unauthorized();
            var invoice = await db.Invoices.FirstOrDefaultAsync(x => x.Id == request.InvoiceId &&
                (x.ClientUserId == openedBy || x.ProviderUserId == openedBy), ct);
            if (invoice is null || string.IsNullOrWhiteSpace(request.Reason)) return Results.BadRequest();

            var superAdminId = await identity.UserRoles.Where(x => x.TenantId == null)
                .Join(identity.Roles.Where(x => x.Code == "SUPER_ADMIN"), x => x.RoleIdValue, x => x.Id, (x, _) => x.UserId)
                .SingleOrDefaultAsync(ct);
            if (superAdminId == Guid.Empty) return Results.Problem("Conta super admin única não encontrada.", statusCode: 503);

            var dispute = PaymentDispute.Create(invoice.Id, openedBy, request.Reason);
            dispute.AssignMediator(superAdminId);
            db.Disputes.Add(dispute);
            db.Notifications.Add(UserNotification.Create(superAdminId, "Nova disputa de pagamento",
                $"A disputa {dispute.Id} requer mediação. Consulte as mensagens da fatura.", "financial.dispute", NotificationPriority.High));
            await db.SaveChangesAsync(ct);
            return Results.Ok(ToResponse(dispute));
        }).RequireAuthorization(SecurityPolicies.DisputeOpen).WithName("OpenPaymentDispute");

        disputes.MapPost("/{disputeId:guid}/resolve", async (Guid disputeId, bool accepted,
            FinancialDbContext db, IdentityDbContext identity,
            Worfair.Modules.Tenants.Infrastructure.Persistence.TenancyDbContext tenancy,
            ICurrentUser user, CancellationToken ct) =>
        {
            if (user.UserId is not { } mediator || !await IsSuperAdmin(identity, mediator, ct)) return Results.Forbid();
            // Escrita elevada: localiza o tenant da disputa e resolve dentro dele
            // (o interceptor barraria Modified cross-tenant no contexto global).
            var tenants = await FinancialElevatedRead.ListActiveTenantIdsAsync(tenancy, ct)
                .ConfigureAwait(false);
            foreach (var tenantId in tenants)
            {
                await using var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
                await db.Database.ExecuteSqlRawAsync(
                    "SELECT set_config('app.tenant_id', {0}, true)",
                    [tenantId.ToString()], ct).ConfigureAwait(false);
                var dispute = await db.Disputes
                    .FirstOrDefaultAsync(x => x.Id == disputeId, ct).ConfigureAwait(false);
                if (dispute is null)
                {
                    await tx.RollbackAsync(ct).ConfigureAwait(false);
                    continue;
                }
                dispute.Resolve(accepted);
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                await tx.CommitAsync(ct).ConfigureAwait(false);
                return Results.Ok(ToResponse(dispute));
            }
            return Results.NotFound();
        }).RequireAuthorization(SecurityPolicies.DisputeAdmin).WithName("ResolvePaymentDispute");

        // Gestão de conflitos (lista/detalhe): EXCLUSIVA do super admin.
        // Usuário comum usa só mensagens; a abertura (denúncia) segue liberada
        // às partes da fatura e o acompanhamento chega por notificação.
        // Leitura elevada: no contexto global o RLS esconderia tudo.
        disputes.MapGet(string.Empty, async (FinancialDbContext db, IdentityDbContext identity,
            Worfair.Modules.Tenants.Infrastructure.Persistence.TenancyDbContext tenancy,
            ICurrentUser user, CancellationToken ct) =>
        {
            if (user.UserId is not { } userId) return Results.Unauthorized();
            if (!await IsSuperAdmin(identity, userId, ct)) return Results.Forbid();
            var tenants = await FinancialElevatedRead.ListActiveTenantIdsAsync(tenancy, ct)
                .ConfigureAwait(false);
            var all = await FinancialElevatedRead.QueryAcrossTenantsAsync(
                db, tenants,
                fdb => fdb.Disputes.OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct), ct).ConfigureAwait(false);
            return Results.Ok(all.Select(ToResponse).OrderByDescending(x => x.CreatedAtUtc).ToList());
        }).RequireAuthorization(SecurityPolicies.DisputeAdmin).WithName("ListPaymentDisputes")
            .WithSummary("Lista disputas (super admin).");

        disputes.MapGet("/{disputeId:guid}", async (Guid disputeId, FinancialDbContext db,
            IdentityDbContext identity,
            Worfair.Modules.Tenants.Infrastructure.Persistence.TenancyDbContext tenancy,
            ICurrentUser user, CancellationToken ct) =>
        {
            if (user.UserId is not { } userId) return Results.Unauthorized();
            if (!await IsSuperAdmin(identity, userId, ct)) return Results.Forbid();
            var tenants = await FinancialElevatedRead.ListActiveTenantIdsAsync(tenancy, ct)
                .ConfigureAwait(false);
            var found = await FinancialElevatedRead.QueryAcrossTenantsAsync(
                db, tenants,
                fdb => fdb.Disputes.Where(x => x.Id == disputeId).ToListAsync(ct), ct).ConfigureAwait(false);
            if (found.Count == 0) return Results.NotFound();
            return Results.Ok(ToResponse(found[0]));
        }).RequireAuthorization(SecurityPolicies.DisputeAdmin).WithName("GetPaymentDisputeById");

        return app;
    }

    private static async Task<bool> IsSuperAdmin(IdentityDbContext identity, Guid userId, CancellationToken ct) =>
        await identity.UserRoles.Where(x => x.UserId == userId && x.TenantId == null)
            .Join(identity.Roles.Where(x => x.Code == "SUPER_ADMIN"), x => x.RoleIdValue, x => x.Id, (x, _) => x)
            .AnyAsync(ct);

    private static PaymentDisputeResponse ToResponse(PaymentDispute dispute) => new(
        dispute.Id, dispute.InvoiceId, dispute.OpenedByUserId, dispute.SuperAdminUserId,
        dispute.Reason, dispute.Status.ToString(), dispute.CreatedAtUtc, dispute.ResolvedAtUtc);
}
