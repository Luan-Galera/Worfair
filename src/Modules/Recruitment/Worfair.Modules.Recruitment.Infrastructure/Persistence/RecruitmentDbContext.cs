namespace Worfair.Modules.Recruitment.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Worfair.BuildingBlocks.Domain.Tenancy;
using Worfair.BuildingBlocks.Infrastructure.Persistence.Outbox;
using Worfair.BuildingBlocks.Infrastructure.Persistence.Tenant;

/// <summary>
/// DbContext do módulo Recruitment — schema SQL "recruitment" (docs/database/03 §5).
/// Todas as tabelas são tenant-owned (RLS FORCE) + outbox_messages do módulo.
/// </summary>
public sealed class RecruitmentDbContext(DbContextOptions<RecruitmentDbContext> options, ITenantProvider tenantProvider)
    : DbContext(options), ITenantFilteredDbContext
{
    public DbSet<JobRequisition> JobRequisitions => Set<JobRequisition>();

    public DbSet<Candidate> Candidates => Set<Candidate>();

    public DbSet<Interview> Interviews => Set<Interview>();

    public TenantId? CurrentTenantId => tenantProvider.TenantId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RecruitmentDbContext).Assembly);
        modelBuilder.AddOutboxMessages("recruitment");
        modelBuilder.ApplyTenantFiltersFromProvider(this);
    }
}
