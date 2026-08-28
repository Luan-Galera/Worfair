namespace Worfair.Modules.Tenants.Infrastructure.ReadContract;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Worfair.BuildingBlocks.Domain.ValueObjects;
using Worfair.Modules.Tenants.Contracts;
using Worfair.Modules.Tenants.Domain.Aggregates.Membership;
using Worfair.Modules.Tenants.Domain.Aggregates.Tenant;
using Worfair.Modules.Tenants.Infrastructure.Persistence;

/// <summary>
/// Implementação read-only do contrato Tenants (R-07).
///
/// Leitura ELEVADA (cross-tenant): usada exclusivamente pela autorização/switch
/// (docs/security/01 §4, docs/security/02 §4). Segue a consequência prática de
/// R-04 — "para inspeção de um tenant específico, define app.tenant_id
/// manualmente na sessão (auditável)": o contexto é definido com
/// set_config(..., true) (escopo da TRANSAÇÃO) e a transação é revertida —
/// leitura apenas, nunca escrita.
/// </summary>
public sealed class TenancyReadContract(TenancyDbContext db) : ITenancyReadContract
{
    public async Task<bool> IsTenantActiveAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        // tenants é tabela global (R-03): leitura direta.
        return await db.Tenants.AsNoTracking()
            .AnyAsync(t => t.Id == tenantId.Value && t.Status == TenantStatus.Active, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<string?> GetTenantNameAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        return await db.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantId.Value)
            .Select(t => t.Name)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> HasActiveMembershipAsync(
        Guid userId, TenantId tenantId, CancellationToken cancellationToken = default)
    {
        var found = await ReadMembershipElevatedAsync(tenantId, userId, cancellationToken).ConfigureAwait(false);
        return found is { Status: MembershipStatus.Active };
    }

    public async Task<IReadOnlyList<MembershipInfo>> ListActiveMembershipsAcrossTenantsAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var result = new List<MembershipInfo>();

        var activeTenants = await db.Tenants.AsNoTracking()
            .Where(t => t.Status == TenantStatus.Active)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var tenant in activeTenants)
        {
            var membership = await ReadMembershipElevatedAsync(tenant.Key, userId, cancellationToken)
                .ConfigureAwait(false);

            if (membership is { Status: MembershipStatus.Active })
                result.Add(new MembershipInfo(tenant.Id, tenant.Name, (int)membership.Status, membership.JoinedAtUtc));
        }

        return result;
    }

    /// <summary>
    /// Lê uma linha de tenant_memberships "como se" estivesse no tenant alvo,
    /// dentro de transação própria revertida ao final (RLS respeitado; sem bypass).
    /// </summary>
    private async Task<TenantMembership?> ReadMembershipElevatedAsync(
        TenantId tenantId, Guid userId, CancellationToken cancellationToken)
    {
        await using var transaction =
            await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await db.Database.ExecuteSqlRawAsync(
            "SELECT set_config('app.tenant_id', {0}, true)",
            [tenantId.Value.ToString()],
            cancellationToken).ConfigureAwait(false);

        var membership = await db.TenantMemberships
            .AsNoTracking()
            .IgnoreQueryFilters() // filtro EF relaxado DE PROPÓSITO; o banco aplica RLS do tenant alvo
            .FirstOrDefaultAsync(m => m.UserId == userId, cancellationToken)
            .ConfigureAwait(false);

        await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        return membership;
    }
}
