namespace Worfair.Api.Endpoints;

using Worfair.Api.Features.Financial;
using Worfair.Api.Authorization;
using Worfair.Api.Infrastructure;
using Worfair.BuildingBlocks.Application.Security;
using Worfair.BuildingBlocks.Domain.ValueObjects;
using Worfair.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public static class FinancialEndpoints
{
    public static IEndpointRouteBuilder MapFinancialEndpoints(this IEndpointRouteBuilder app)
    {
        var financial = app.MapGroup("/api/financial").WithTags("Financial");

        financial.MapGet("/invoices", async (FinancialDbContext db, ICurrentUser user, CancellationToken ct) =>
        {
            if (user.UserId is not { } userId) return Results.Unauthorized();
            var list = await db.Invoices.Where(x => x.ClientUserId == userId || x.ProviderUserId == userId)
                .OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);
            return Results.Ok(list.Select(ToResponse).ToList());
        })
            .WithName("ListInvoices")
            // Escopo genérico de tenant (sem exigir financial.invoice.issue): o
            // prestador também precisa ver as faturas onde é Provider — o filtro
            // por usuário na query mantém o isolamento. Criar/liquidar seguem
            // restritos ao modo Contratante abaixo.
            .RequireAuthorization(SecurityPolicies.ProposalRead);

        financial.MapGet("/balance", async (FinancialDbContext db, ICurrentUser user, CancellationToken ct) =>
        {
            if (user.UserId is not { } userId) return Results.Unauthorized();
            var mine = await db.Invoices
                .Where(x => x.ClientUserId == userId || x.ProviderUserId == userId)
                .ToListAsync(ct);
            return Results.Ok(ToBalance(userId, mine));
        })
            .WithName("GetFinancialBalance")
            .RequireAuthorization(SecurityPolicies.ProposalRead)
            .WithSummary("Saldo das faturas: a receber/a pagar/recebido/pago.");

        // Detalhe para mediação/suporte: parte da fatura ou super admin
        // (o global não passa nas policies com escopo de tenant; leitura elevada).
        financial.MapGet("/invoices/{invoiceId:guid}", async (Guid invoiceId,
            FinancialDbContext db, IdentityDbContext identity,
            Worfair.Modules.Tenants.Infrastructure.Persistence.TenancyDbContext tenancy,
            ICurrentUser user, CancellationToken ct) =>
        {
            if (user.UserId is not { } userId) return Results.Unauthorized();
            var invoice = await db.Invoices.FirstOrDefaultAsync(x => x.Id == invoiceId, ct);
            if (invoice is not null)
            {
                var isParty = invoice.ClientUserId == userId || invoice.ProviderUserId == userId;
                if (isParty || await IsSuperAdmin(identity, userId, ct).ConfigureAwait(false))
                    return Results.Ok(ToResponse(invoice));
                return Results.NotFound();
            }
            if (!await IsSuperAdmin(identity, userId, ct).ConfigureAwait(false))
                return Results.NotFound();
            var tenants = await FinancialElevatedRead.ListActiveTenantIdsAsync(tenancy, ct)
                .ConfigureAwait(false);
            var owner = await FinancialElevatedRead.FindInvoiceTenantAsync(
                db, tenants, invoiceId, ct).ConfigureAwait(false);
            if (owner is null) return Results.NotFound();
            var found = await FinancialElevatedRead.QueryAcrossTenantsAsync(
                db, [owner.Value],
                fdb => fdb.Invoices.Where(x => x.Id == invoiceId).ToListAsync(ct), ct).ConfigureAwait(false);
            return found.Count == 0 ? Results.NotFound() : Results.Ok(ToResponse(found[0]));
        })
            .WithName("GetInvoiceById")
            .RequireAuthorization(SecurityPolicies.SupportRead)
            .WithSummary("Detalhe da fatura (parte ou super admin).");

        financial.MapPost("/invoices", async (FinancialInvoiceRequest request, FinancialDbContext db, FinancialUnitOfWork unitOfWork, ICurrentUser user, CancellationToken ct) =>
        {
            if (user.UserId is not { } clientUserId) return Results.Unauthorized();
            if (request.Amount <= 0 || string.IsNullOrWhiteSpace(request.Description)) return Results.BadRequest();
            var invoice = FinancialInvoice.Create(clientUserId, request.ProviderUserId, request.ProviderCompanyId,
                request.Amount, request.Currency, request.Description);
            await db.Invoices.AddAsync(invoice, ct);
            await unitOfWork.SaveChangesAsync(ct);
            return Results.Ok(ToResponse(invoice));
        })
            .WithName("CreateInvoice")
            .RequireAuthorization(SecurityPolicies.InvoiceIssue);

        financial.MapPost("/invoices/{invoiceId:guid}/settle", async (Guid invoiceId, FinancialDbContext db, ICurrentUser user, CancellationToken ct) =>
        {
            if (user.UserId is not { } userId) return Results.Unauthorized();
            var invoice = await db.Invoices.FirstOrDefaultAsync(x => x.Id == invoiceId && x.ClientUserId == userId, ct);
            if (invoice is null) return Results.NotFound(new { message = "Invoice not found." });
            invoice.Settle();
            await db.SaveChangesAsync(ct);
            return Results.Ok(ToResponse(invoice));
        })
            .WithName("SettleInvoice")
            .RequireAuthorization(SecurityPolicies.InvoiceIssue);

        // ── Gestão de faturas do super admin (a plataforma centraliza o dinheiro).
        var admin = app.MapGroup("/api/financial/admin").WithTags("Financial · Admin");

        admin.MapGet("/invoices", async (FinancialDbContext db, IdentityDbContext identity,
            Worfair.Modules.Tenants.Infrastructure.Persistence.TenancyDbContext tenancy,
            ICurrentUser user, CancellationToken ct) =>
        {
            if (user.UserId is not { } userId) return Results.Unauthorized();
            if (!await IsSuperAdmin(identity, userId, ct).ConfigureAwait(false)) return Results.Forbid();
            var tenants = await FinancialElevatedRead.ListActiveTenantIdsAsync(tenancy, ct)
                .ConfigureAwait(false);
            var all = await FinancialElevatedRead.QueryAcrossTenantsAsync(
                db, tenants,
                fdb => fdb.Invoices.OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct), ct)
                .ConfigureAwait(false);
            return Results.Ok(all.Select(ToResponse).OrderByDescending(x => x.CreatedAtUtc).ToList());
        })
            .WithName("AdminListInvoices")
            .RequireAuthorization(SecurityPolicies.DisputeAdmin)
            .WithSummary("Todas as faturas (super admin).");

        admin.MapPost("/invoices", async (AdminInvoiceRequest request, FinancialDbContext db,
            IdentityDbContext identity,
            Worfair.Modules.Tenants.Infrastructure.Persistence.TenancyDbContext tenancy,
            ICurrentUser user, CancellationToken ct) =>
        {
            if (user.UserId is not { } userId) return Results.Unauthorized();
            if (!await IsSuperAdmin(identity, userId, ct).ConfigureAwait(false)) return Results.Forbid();
            if (request.Amount <= 0 || string.IsNullOrWhiteSpace(request.Description))
                return Results.BadRequest();
            var tenants = await FinancialElevatedRead.ListActiveTenantIdsAsync(tenancy, ct)
                .ConfigureAwait(false);
            if (!tenants.Contains(request.TenantId)) return Results.BadRequest();

            var invoice = FinancialInvoice.Create(request.ClientUserId, request.ProviderUserId,
                request.ProviderCompanyId, request.Amount, request.Currency, request.Description);
            invoice.SetTenantId(new TenantId(request.TenantId));

            await using var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            await db.Database.ExecuteSqlRawAsync(
                "SELECT set_config('app.tenant_id', {0}, true)",
                [request.TenantId.ToString()], ct).ConfigureAwait(false);
            await db.Invoices.AddAsync(invoice, ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct).ConfigureAwait(false);

            return Results.Ok(ToResponse(invoice));
        })
            .WithName("AdminCreateInvoice")
            .RequireAuthorization(SecurityPolicies.DisputeAdmin)
            .WithSummary("Cria fatura entre usuários num espaço (super admin).");

        admin.MapPost("/invoices/{invoiceId:guid}/settle", async (Guid invoiceId,
            FinancialDbContext db, IdentityDbContext identity,
            Worfair.Modules.Tenants.Infrastructure.Persistence.TenancyDbContext tenancy,
            ICurrentUser user, CancellationToken ct) =>
        {
            if (user.UserId is not { } userId) return Results.Unauthorized();
            if (!await IsSuperAdmin(identity, userId, ct).ConfigureAwait(false)) return Results.Forbid();
            var tenants = await FinancialElevatedRead.ListActiveTenantIdsAsync(tenancy, ct)
                .ConfigureAwait(false);
            foreach (var tenantId in tenants)
            {
                await using var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
                await db.Database.ExecuteSqlRawAsync(
                    "SELECT set_config('app.tenant_id', {0}, true)",
                    [tenantId.ToString()], ct).ConfigureAwait(false);
                var invoice = await db.Invoices
                    .FirstOrDefaultAsync(x => x.Id == invoiceId, ct).ConfigureAwait(false);
                if (invoice is null)
                {
                    await tx.RollbackAsync(ct).ConfigureAwait(false);
                    continue;
                }
                invoice.Settle();
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                await tx.CommitAsync(ct).ConfigureAwait(false);
                return Results.Ok(ToResponse(invoice));
            }
            return Results.NotFound();
        })
            .WithName("AdminSettleInvoice")
            .RequireAuthorization(SecurityPolicies.DisputeAdmin)
            .WithSummary("Liquida qualquer fatura (super admin).");

        // Receita da plataforma (super admin): quanto entrou de taxas.
        // Agregado cross-tenant com leitura elevada (RLS esconderia tudo).
        financial.MapGet("/revenue", async (FinancialDbContext db, IdentityDbContext identity,
            Worfair.Modules.Tenants.Infrastructure.Persistence.TenancyDbContext tenancy,
            ICurrentUser user, CancellationToken ct) =>
        {
            if (user.UserId is not { } userId) return Results.Unauthorized();
            if (!await IsSuperAdmin(identity, userId, ct).ConfigureAwait(false)) return Results.Forbid();
            var tenants = await FinancialElevatedRead.ListActiveTenantIdsAsync(tenancy, ct)
                .ConfigureAwait(false);
            var slices = await FinancialElevatedRead.QueryAcrossTenantsAsync(
                db, tenants,
                async fdb =>
                {
                    var invoices = await fdb.Invoices.AsNoTracking().ToListAsync(ct).ConfigureAwait(false);
                    var openDisputes = await fdb.Disputes.AsNoTracking()
                        .CountAsync(x => x.Status == Worfair.Api.Features.Communication.DisputeStatus.Open
                            || x.Status == Worfair.Api.Features.Communication.DisputeStatus.UnderMediation, ct)
                        .ConfigureAwait(false);
                    return new List<RevenueSlice>
                    {
                        new(
                            invoices.Where(x => x.Status == FinancialStatus.Paid).Sum(x => x.PlatformFeeAmount),
                            invoices.Where(x => x.Status == FinancialStatus.Issued).Sum(x => x.PlatformFeeAmount),
                            invoices.Where(x => x.Status == FinancialStatus.Paid).Sum(x => x.TotalAmount),
                            invoices.Count(x => x.Status == FinancialStatus.Paid),
                            invoices.Count(x => x.Status == FinancialStatus.Issued),
                            invoices.Count,
                            openDisputes)
                    };
                }, ct).ConfigureAwait(false);
            return Results.Ok(new FinancialRevenueResponse(
                slices.Sum(s => s.FeesReceived),
                slices.Sum(s => s.FeesPending),
                slices.Sum(s => s.VolumePaid),
                slices.Sum(s => s.PaidCount),
                slices.Sum(s => s.IssuedCount),
                slices.Sum(s => s.InvoicesCount),
                slices.Sum(s => s.OpenDisputes),
                "BRL"));
        })
            .WithName("GetPlatformRevenue")
            .RequireAuthorization(SecurityPolicies.DisputeAdmin)
            .WithSummary("Receita da plataforma em taxas (super admin).");

        return app;
    }

    private static FinancialInvoiceResponse ToResponse(FinancialInvoice invoice) => new(
        invoice.Id, invoice.ClientUserId, invoice.ProviderUserId, invoice.ProviderCompanyId,
        invoice.Amount, invoice.PlatformFeeAmount, invoice.TotalAmount, invoice.Currency, invoice.Description, invoice.Status.ToString(),
        invoice.CreatedAtUtc, invoice.UpdatedAtUtc);

    private static FinancialBalanceResponse ToBalance(Guid userId, List<FinancialInvoice> mine)
    {
        var toReceive = mine.Where(x => x.ProviderUserId == userId && x.Status == FinancialStatus.Issued).ToList();
        var toPay = mine.Where(x => x.ClientUserId == userId && x.Status == FinancialStatus.Issued).ToList();
        var received = mine.Where(x => x.ProviderUserId == userId && x.Status == FinancialStatus.Paid).ToList();
        var paid = mine.Where(x => x.ClientUserId == userId && x.Status == FinancialStatus.Paid).ToList();

        return new FinancialBalanceResponse(
            toReceive.Sum(x => x.Amount), toReceive.Sum(x => x.PlatformFeeAmount), toReceive.Count,
            toPay.Sum(x => x.TotalAmount), toPay.Sum(x => x.PlatformFeeAmount), toPay.Count,
            received.Sum(x => x.Amount), received.Count,
            paid.Sum(x => x.TotalAmount), paid.Count,
            mine.Count,
            "BRL");
    }

    private static async Task<bool> IsSuperAdmin(
        IdentityDbContext identity, Guid userId, CancellationToken ct) =>
        await identity.UserRoles.Where(x => x.UserId == userId && x.TenantId == null)
            .Join(identity.Roles.Where(x => x.Code == "SUPER_ADMIN"), x => x.RoleIdValue, x => x.Id, (x, _) => x)
            .AnyAsync(ct);
}

public sealed record FinancialBalanceResponse(
    decimal ToReceiveAmount,
    decimal ToReceiveFees,
    int ToReceiveCount,
    decimal ToPayTotal,
    decimal ToPayFees,
    int ToPayCount,
    decimal ReceivedAmount,
    int ReceivedCount,
    decimal PaidTotal,
    int PaidCount,
    int InvoicesCount,
    string Currency);
public sealed record FinancialRevenueResponse(
    decimal FeesReceived,
    decimal FeesPending,
    decimal VolumePaid,
    int PaidCount,
    int IssuedCount,
    int InvoicesCount,
    int OpenDisputes,
    string Currency);

public sealed record AdminInvoiceRequest(
    Guid TenantId,
    Guid ClientUserId,
    Guid ProviderUserId,
    Guid? ProviderCompanyId,
    decimal Amount,
    string? Currency,
    string Description);

internal sealed record RevenueSlice(    decimal FeesReceived,
    decimal FeesPending,
    decimal VolumePaid,
    int PaidCount,
    int IssuedCount,
    int InvoicesCount,
    int OpenDisputes);
