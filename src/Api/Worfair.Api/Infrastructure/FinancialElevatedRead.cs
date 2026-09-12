namespace Worfair.Api.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Worfair.Modules.Tenants.Domain.Aggregates.Tenant;
using Worfair.Modules.Tenants.Infrastructure.Persistence;

/// <summary>
/// Leitura elevada para o super admin (R-04, mesmo padrão do TenancyReadContract):
/// o RLS filtra por app.tenant_id da sessão, então dados de tenants ficam
/// invisíveis no contexto global. Itera os tenants ativos fixando cada um no
/// escopo da transação (somente leitura, revertida ao final) e agrega em memória.
/// Uso exclusivo da mediação/relatórios do super admin.
/// </summary>
internal static class FinancialElevatedRead
{
    public static async Task<IReadOnlyList<Guid>> ListActiveTenantIdsAsync(
        TenancyDbContext tenancy, CancellationToken ct) =>
        await tenancy.Tenants.AsNoTracking()
            .Where(t => t.Status == TenantStatus.Active)
            .Select(t => t.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

    public static async Task<List<T>> QueryAcrossTenantsAsync<T>(
        FinancialDbContext db,
        IReadOnlyList<Guid> tenantIds,
        Func<FinancialDbContext, Task<List<T>>> query,
        CancellationToken ct)
    {
        var all = new List<T>();
        foreach (var tenantId in tenantIds)
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            await db.Database.ExecuteSqlRawAsync(
                "SELECT set_config('app.tenant_id', {0}, true)",
                [tenantId.ToString()], ct).ConfigureAwait(false);
            all.AddRange(await query(db).ConfigureAwait(false));
            await tx.RollbackAsync(ct).ConfigureAwait(false);
        }
        return all;
    }

    /// <summary>Localiza em qual tenant está a fatura (para detalhe/thread).</summary>
    public static async Task<Guid?> FindInvoiceTenantAsync(
        FinancialDbContext db,
        IReadOnlyList<Guid> tenantIds,
        Guid invoiceId,
        CancellationToken ct)
    {
        foreach (var tenantId in tenantIds)
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            await db.Database.ExecuteSqlRawAsync(
                "SELECT set_config('app.tenant_id', {0}, true)",
                [tenantId.ToString()], ct).ConfigureAwait(false);
            var found = await db.Invoices.AsNoTracking()
                .AnyAsync(x => x.Id == invoiceId, ct).ConfigureAwait(false);
            await tx.RollbackAsync(ct).ConfigureAwait(false);
            if (found)
                return tenantId;
        }
        return null;
    }
}
