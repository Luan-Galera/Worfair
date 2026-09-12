namespace Worfair.Modules.Jobs.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Worfair.BuildingBlocks.Domain.Tenancy;
using Worfair.BuildingBlocks.Domain.ValueObjects;
using Worfair.BuildingBlocks.Infrastructure.Persistence.Outbox;
using Worfair.BuildingBlocks.Infrastructure.Persistence.Tenant;
using Worfair.Modules.Jobs.Domain.Aggregates.JobPosting;
using Worfair.Modules.Jobs.Domain.Aggregates.JobApplication;
using Worfair.Modules.Jobs.Domain.Aggregates.Proposal;
using Worfair.Modules.Jobs.Domain.Aggregates.ServiceProject;

public sealed class JobsDbContext(DbContextOptions<JobsDbContext> options, ITenantProvider tenantProvider)
    : DbContext(options), ITenantFilteredDbContext
{
    public DbSet<JobPosting> JobPostings => Set<JobPosting>();
    public DbSet<JobApplication> JobApplications => Set<JobApplication>();
    public DbSet<Proposal> Proposals => Set<Proposal>();
    public DbSet<ServiceProject> ServiceProjects => Set<ServiceProject>();

    public TenantId? CurrentTenantId => tenantProvider.TenantId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(JobsDbContext).Assembly);
        modelBuilder.AddOutboxMessages("jobs");
        modelBuilder.ApplyTenantFiltersFromProvider(this);
    }
}
