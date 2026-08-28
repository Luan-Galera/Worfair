namespace Worfair.Modules.Tenants.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Worfair.BuildingBlocks.Domain.Tenancy;
using Worfair.BuildingBlocks.Infrastructure.Persistence.Outbox;
using Worfair.BuildingBlocks.Infrastructure.Persistence.Tenant;
using Worfair.Modules.Tenants.Domain.Aggregates.Company;
using Worfair.Modules.Tenants.Domain.Aggregates.Membership;
using Worfair.Modules.Tenants.Domain.Aggregates.Settings;
using Worfair.Modules.Tenants.Domain.Aggregates.Tenant;

/// <summary>
/// DbContext do módulo Tenants — schema SQL "tenancy" (R-01/R-09).
/// Tabelas globais: tenants. Tenant-owned: companies, tenant_memberships,
/// tenant_settings (+ outbox_messages do módulo).
/// </summary>
public sealed class TenancyDbContext(DbContextOptions<TenancyDbContext> options, ITenantProvider tenantProvider)
    : DbContext(options), ITenantFilteredDbContext
{
    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<Company> Companies => Set<Company>();

    public DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();

    public DbSet<TenantSettings> TenantSettings => Set<TenantSettings>();

    public TenantId? CurrentTenantId => tenantProvider.TenantId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TenancyDbContext).Assembly);
        modelBuilder.AddOutboxMessages("tenancy");

        // Filtros dinâmicos por tenant (leitura) — docs/architecture/04 §3.3.
        modelBuilder.ApplyTenantFiltersFromProvider(this);
    }
}
