namespace Worfair.Modules.Jobs.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Worfair.BuildingBlocks.Domain.ValueObjects;
using Worfair.Modules.Jobs.Domain.Aggregates.JobPosting;
using Worfair.Modules.Jobs.Domain.Aggregates.JobApplication;
using Worfair.Modules.Jobs.Domain.Aggregates.Proposal;
using Worfair.Modules.Jobs.Domain.Aggregates.ServiceProject;

public sealed class JobPostingConfiguration : IEntityTypeConfiguration<JobPosting>
{
    public void Configure(EntityTypeBuilder<JobPosting> builder)
    {
        builder.ToTable("job_postings", "jobs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new JobPostingId(value));
        builder.Property(x => x.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.CompanyName).HasColumnName("company_name").HasMaxLength(120);
        builder.Property(x => x.Category).HasColumnName("category").HasMaxLength(60);
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").IsRequired();
        builder.Property(x => x.Location).HasColumnName("location").HasMaxLength(150);
        builder.Property(x => x.Remote).HasColumnName("remote").HasConversion<int>().IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(x => x.PublishedAtUtc).HasColumnName("published_at");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at");
        builder.HasIndex(x => new { x.TenantId, x.Status }).HasDatabaseName("ix_job_postings_tenant_status");
        builder.HasIndex(x => new { x.TenantId, x.PublishedAtUtc }).HasDatabaseName("ix_job_postings_tenant_published");
        builder.HasIndex(x => new { x.TenantId, x.Category }).HasDatabaseName("ix_job_postings_tenant_category");
        builder.Ignore(x => x.DomainEvents);
    }
}

public sealed class ServiceProjectConfiguration : IEntityTypeConfiguration<ServiceProject>
{
    public void Configure(EntityTypeBuilder<ServiceProject> builder)
    {
        builder.ToTable("service_projects", "jobs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new ServiceProjectId(value));
        builder.Property(x => x.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.CompanyName).HasColumnName("company_name").HasMaxLength(120);
        builder.Property(x => x.Category).HasColumnName("category").HasMaxLength(60);
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").IsRequired();
        builder.Property(x => x.BudgetMin).HasColumnName("budget_min").HasColumnType("numeric(12,2)");
        builder.Property(x => x.BudgetMax).HasColumnName("budget_max").HasColumnType("numeric(12,2)");
        builder.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        builder.Property(x => x.Deadline).HasColumnName("deadline");
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at");
        builder.HasIndex(x => new { x.TenantId, x.Status }).HasDatabaseName("ix_service_projects_tenant_status");
        builder.HasIndex(x => new { x.TenantId, x.Category }).HasDatabaseName("ix_service_projects_tenant_category");
        builder.Ignore(x => x.DomainEvents);
    }
}

public sealed class JobApplicationConfiguration : IEntityTypeConfiguration<JobApplication>
{
    public void Configure(EntityTypeBuilder<JobApplication> builder)
    {
        builder.ToTable("job_applications", "jobs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id")
            .HasConversion(id => id.Value, value => new JobApplicationId(value));
        builder.Property(x => x.TenantId).HasColumnName("tenant_id")
            .HasConversion(id => id.Value, value => new TenantId(value)).IsRequired();
        builder.Property(x => x.JobPostingId).HasColumnName("job_posting_id")
            .HasConversion(id => id.Value, value => new JobPostingId(value)).IsRequired();
        builder.Property(x => x.ApplicantUserId).HasColumnName("applicant_user_id").IsRequired();
        builder.Property(x => x.ApplicantCompanyId).HasColumnName("applicant_company_id");
        builder.Property(x => x.Message).HasColumnName("message").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at");
        builder.HasIndex(x => new { x.TenantId, x.JobPostingId, x.ApplicantUserId })
            .IsUnique().HasDatabaseName("ux_job_applications_tenant_posting_applicant");
        builder.HasIndex(x => new { x.TenantId, x.ApplicantUserId })
            .HasDatabaseName("ix_job_applications_tenant_applicant");
        builder.Ignore(x => x.DomainEvents);
    }
}

public sealed class ProposalConfiguration : IEntityTypeConfiguration<Proposal>
{
    public void Configure(EntityTypeBuilder<Proposal> builder)
    {
        builder.ToTable("proposals", "jobs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id")
            .HasConversion(id => id.Value, value => new ProposalId(value));
        builder.Property(x => x.TenantId).HasColumnName("tenant_id")
            .HasConversion(id => id.Value, value => new TenantId(value)).IsRequired();
        builder.Property(x => x.JobPostingId).HasColumnName("job_posting_id");
        builder.Property(x => x.ServiceProjectId).HasColumnName("service_project_id");
        builder.Property(x => x.ProviderUserId).HasColumnName("provider_user_id").IsRequired();
        builder.Property(x => x.ProviderCompanyId).HasColumnName("provider_company_id");
        builder.Property(x => x.Message).HasColumnName("message").IsRequired();
        builder.Property(x => x.Amount).HasColumnName("amount").HasColumnType("numeric(12,2)");
        builder.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at");
        builder.HasIndex(x => new { x.TenantId, x.ProviderUserId })
            .HasDatabaseName("ix_proposals_tenant_provider");
        builder.HasIndex(x => new { x.TenantId, x.JobPostingId, x.ProviderUserId })
            .IsUnique().HasDatabaseName("ux_proposals_tenant_posting_provider");
        builder.HasIndex(x => new { x.TenantId, x.ServiceProjectId, x.ProviderUserId })
            .IsUnique().HasDatabaseName("ux_proposals_tenant_project_provider");
        builder.Ignore(x => x.DomainEvents);
    }
}
