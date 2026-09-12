namespace Worfair.Modules.Jobs.Domain.Abstractions;

using Worfair.Modules.Jobs.Domain.Aggregates.JobPosting;
using Worfair.Modules.Jobs.Domain.Aggregates.JobApplication;
using Worfair.Modules.Jobs.Domain.Aggregates.Proposal;
using Worfair.Modules.Jobs.Domain.Aggregates.ServiceProject;

public interface IJobPostingRepository
{
    Task<JobPosting?> GetByIdAsync(JobPostingId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JobPosting>> ListByTenantAsync(CancellationToken cancellationToken = default);
    Task AddAsync(JobPosting posting, CancellationToken cancellationToken = default);

    /// <summary>Vitrine pública: publicadas em TODOS os tenants (RLS filtra no banco).</summary>
    Task<IReadOnlyList<JobPosting>> ListPublishedAcrossTenantsAsync(
        ShowcasePostingFilter filter, CancellationToken cancellationToken = default);
    Task<JobPosting?> GetByIdAcrossTenantsAsync(JobPostingId id, CancellationToken cancellationToken = default);
}

public interface IServiceProjectRepository
{
    Task<ServiceProject?> GetByIdAsync(ServiceProjectId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ServiceProject>> ListByTenantAsync(CancellationToken cancellationToken = default);
    Task AddAsync(ServiceProject project, CancellationToken cancellationToken = default);

    /// <summary>Vitrine pública: abertos em TODOS os tenants (RLS filtra no banco).</summary>
    Task<IReadOnlyList<ServiceProject>> ListOpenAcrossTenantsAsync(
        ShowcaseProjectFilter filter, CancellationToken cancellationToken = default);
    Task<ServiceProject?> GetByIdAcrossTenantsAsync(ServiceProjectId id, CancellationToken cancellationToken = default);
}

/// <summary>Filtros da vitrine de vagas (todos opcionais).</summary>
public sealed record ShowcasePostingFilter(
    string? Search,
    Guid? CompanyId,
    string? Category,
    int? Remote);

/// <summary>Filtros da vitrine de trabalhos freelancer (todos opcionais).</summary>
public sealed record ShowcaseProjectFilter(
    string? Search,
    Guid? CompanyId,
    string? Category,
    decimal? MinBudget,
    decimal? MaxBudget);

public interface IJobApplicationRepository
{
    Task<JobApplication?> GetByIdAsync(JobApplicationId id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(JobPostingId jobPostingId, Guid applicantUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JobApplication>> ListByApplicantAsync(Guid applicantUserId, CancellationToken cancellationToken = default);
    Task AddAsync(JobApplication application, CancellationToken cancellationToken = default);
}

public interface IProposalRepository
{
    Task<Proposal?> GetByIdAsync(ProposalId id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid? jobPostingId, Guid? serviceProjectId, Guid providerUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Proposal>> ListAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Proposal>> ListByProviderAsync(Guid providerUserId, CancellationToken cancellationToken = default);
    Task AddAsync(Proposal proposal, CancellationToken cancellationToken = default);
}
