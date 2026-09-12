namespace Worfair.Modules.Jobs.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using Worfair.Modules.Jobs.Domain.Abstractions;
using Worfair.Modules.Jobs.Domain.Aggregates.JobPosting;
using Worfair.Modules.Jobs.Domain.Aggregates.JobApplication;
using Worfair.Modules.Jobs.Domain.Aggregates.Proposal;
using Worfair.Modules.Jobs.Domain.Aggregates.ServiceProject;

public sealed class JobPostingRepository(JobsDbContext db) : IJobPostingRepository
{
    public Task<JobPosting?> GetByIdAsync(JobPostingId id, CancellationToken cancellationToken = default) =>
        db.JobPostings.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<JobPosting>> ListByTenantAsync(CancellationToken cancellationToken = default) =>
        await db.JobPostings.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task AddAsync(JobPosting posting, CancellationToken cancellationToken = default) =>
        await db.JobPostings.AddAsync(posting, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<JobPosting>> ListPublishedAcrossTenantsAsync(
        ShowcasePostingFilter filter, CancellationToken cancellationToken = default)
    {
        var query = db.JobPostings.AsNoTracking().IgnoreQueryFilters()
            .Where(x => x.Status == JobPostingStatus.Published);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = $"%{ShowcaseLike.Escape(filter.Search.Trim())}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.Title, term) || EF.Functions.ILike(x.Description, term));
        }
        if (filter.CompanyId is { } companyId)
            query = query.Where(x => x.CompanyId == companyId);
        if (!string.IsNullOrWhiteSpace(filter.Category))
            query = query.Where(x => x.Category == filter.Category!.Trim());
        if (filter.Remote is >= 0 and <= 2)
            query = query.Where(x => x.Remote == (JobPostingRemoteType)filter.Remote.Value);

        return await query.OrderByDescending(x => x.PublishedAtUtc)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<JobPosting?> GetByIdAcrossTenantsAsync(JobPostingId id, CancellationToken cancellationToken = default) =>
        db.JobPostings.AsNoTracking().IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
}

public sealed class ServiceProjectRepository(JobsDbContext db) : IServiceProjectRepository
{
    public Task<ServiceProject?> GetByIdAsync(ServiceProjectId id, CancellationToken cancellationToken = default) =>
        db.ServiceProjects.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ServiceProject>> ListByTenantAsync(CancellationToken cancellationToken = default) =>
        await db.ServiceProjects.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task AddAsync(ServiceProject project, CancellationToken cancellationToken = default) =>
        await db.ServiceProjects.AddAsync(project, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<ServiceProject>> ListOpenAcrossTenantsAsync(
        ShowcaseProjectFilter filter, CancellationToken cancellationToken = default)
    {
        var query = db.ServiceProjects.AsNoTracking().IgnoreQueryFilters()
            .Where(x => x.Status == ServiceProjectStatus.Open);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = $"%{ShowcaseLike.Escape(filter.Search.Trim())}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.Title, term) || EF.Functions.ILike(x.Description, term));
        }
        if (filter.CompanyId is { } companyId)
            query = query.Where(x => x.CompanyId == companyId);
        if (!string.IsNullOrWhiteSpace(filter.Category))
            query = query.Where(x => x.Category == filter.Category!.Trim());
        if (filter.MinBudget is { } minBudget)
            query = query.Where(x => (x.BudgetMax ?? x.BudgetMin) >= minBudget);
        if (filter.MaxBudget is { } maxBudget)
            query = query.Where(x => x.BudgetMin <= maxBudget);

        return await query.OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<ServiceProject?> GetByIdAcrossTenantsAsync(ServiceProjectId id, CancellationToken cancellationToken = default) =>
        db.ServiceProjects.AsNoTracking().IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
}

public sealed class JobApplicationRepository(JobsDbContext db) : IJobApplicationRepository
{
    public Task<JobApplication?> GetByIdAsync(JobApplicationId id, CancellationToken cancellationToken = default) =>
        db.JobApplications.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> ExistsAsync(JobPostingId jobPostingId, Guid applicantUserId, CancellationToken cancellationToken = default) =>
        db.JobApplications.AnyAsync(x => x.JobPostingId == jobPostingId && x.ApplicantUserId == applicantUserId, cancellationToken);

    public async Task<IReadOnlyList<JobApplication>> ListByApplicantAsync(Guid applicantUserId, CancellationToken cancellationToken = default) =>
        await db.JobApplications.AsNoTracking().Where(x => x.ApplicantUserId == applicantUserId)
            .OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task AddAsync(JobApplication application, CancellationToken cancellationToken = default) =>
        await db.JobApplications.AddAsync(application, cancellationToken).ConfigureAwait(false);
}

public sealed class ProposalRepository(JobsDbContext db) : IProposalRepository
{
    public Task<Proposal?> GetByIdAsync(ProposalId id, CancellationToken cancellationToken = default) =>
        db.Proposals.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> ExistsAsync(Guid? jobPostingId, Guid? serviceProjectId, Guid providerUserId, CancellationToken cancellationToken = default) =>
        db.Proposals.AnyAsync(x => x.JobPostingId == jobPostingId && x.ServiceProjectId == serviceProjectId && x.ProviderUserId == providerUserId, cancellationToken);

    public async Task<IReadOnlyList<Proposal>> ListAsync(CancellationToken cancellationToken = default) =>
        await db.Proposals.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<Proposal>> ListByProviderAsync(Guid providerUserId, CancellationToken cancellationToken = default) =>
        await db.Proposals.AsNoTracking().Where(x => x.ProviderUserId == providerUserId)
            .OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task AddAsync(Proposal proposal, CancellationToken cancellationToken = default) =>
        await db.Proposals.AddAsync(proposal, cancellationToken).ConfigureAwait(false);
}

internal static class ShowcaseLike
{
    internal static string Escape(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
