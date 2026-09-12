namespace Worfair.Modules.Tenants.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using Worfair.BuildingBlocks.Domain.ValueObjects;
using Worfair.Modules.Tenants.Domain.Abstractions;
using Worfair.Modules.Tenants.Domain.Aggregates.Company;
using Worfair.Modules.Tenants.Domain.Aggregates.Membership;
using Worfair.Modules.Tenants.Domain.Aggregates.Settings;
using Worfair.Modules.Tenants.Domain.Aggregates.Tenant;
using Worfair.Modules.Tenants.Domain.ValueObjects;

public sealed class TenantRepository(TenancyDbContext db) : ITenantRepository
{
    public Task<Tenant?> GetByIdAsync(TenantId id, CancellationToken cancellationToken = default) =>
        db.Tenants.FirstOrDefaultAsync(t => t.Id == id.Value, cancellationToken);

    public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default) =>
        db.Tenants.FirstOrDefaultAsync(t => t.Slug == slug, cancellationToken);

    public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken = default) =>
        db.Tenants.AnyAsync(t => t.Slug == slug, cancellationToken);

    public async Task<IReadOnlyList<Tenant>> ListAllActiveAsync(CancellationToken cancellationToken = default) =>
        await db.Tenants
            .AsNoTracking()
            .Where(t => t.Status == TenantStatus.Active)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default) =>
        await db.Tenants.AddAsync(tenant, cancellationToken).ConfigureAwait(false);
}

public sealed class CompanyRepository(TenancyDbContext db) : ICompanyRepository
{
    public Task<Company?> GetByTenantAndIdAsync(TenantId tenantId, CompanyId id, CancellationToken cancellationToken = default) =>
        db.Companies.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Company>> ListByTenantAsync(TenantId tenantId, CancellationToken cancellationToken = default) =>
        await db.Companies
            .AsNoTracking()
            .OrderBy(c => c.LegalName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    // R-01/R-06: a checagem é por tenant via Global Query Filter (UNIQUE (tenant_id, document)
    // no banco garante a última linha de defesa).
    public Task<bool> DocumentExistsAsync(Document document, CancellationToken cancellationToken = default) =>
        db.Companies
            .AnyAsync(c => c.Document == document, cancellationToken);

    public async Task AddAsync(Company company, CancellationToken cancellationToken = default) =>
        await db.Companies.AddAsync(company, cancellationToken).ConfigureAwait(false);
}

public sealed class TenantMembershipRepository(TenancyDbContext db) : ITenantMembershipRepository
{
    public Task<TenantMembership?> FindAsync(TenantId tenantId, Guid userId, CancellationToken cancellationToken = default) =>
        db.TenantMemberships.FirstOrDefaultAsync(m => m.TenantId == tenantId.Value && m.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<TenantMembership>> ListByTenantAsync(TenantId tenantId, CancellationToken cancellationToken = default) =>
        await db.TenantMemberships
            .AsNoTracking()
            .OrderBy(m => m.JoinedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AddAsync(TenantMembership membership, CancellationToken cancellationToken = default) =>
        await db.TenantMemberships.AddAsync(membership, cancellationToken).ConfigureAwait(false);
}

public sealed class TenantSettingsRepository(TenancyDbContext db) : ITenantSettingsRepository
{
    public Task<TenantSettings?> GetForTenantAsync(TenantId tenantId, CancellationToken cancellationToken = default) =>
        db.TenantSettings.FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);

    public async Task AddAsync(TenantSettings settings, CancellationToken cancellationToken = default) =>
        await db.TenantSettings.AddAsync(settings, cancellationToken).ConfigureAwait(false);
}
