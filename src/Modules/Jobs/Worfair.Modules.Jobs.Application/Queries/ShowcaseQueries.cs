namespace Worfair.Modules.Jobs.Application.Queries;

using Worfair.BuildingBlocks.Application.Cqrs;
using Worfair.BuildingBlocks.Domain.Errors;
using Worfair.Modules.Jobs.Domain.Abstractions;
using Worfair.Modules.Jobs.Domain.Aggregates.JobPosting;
using Worfair.Modules.Jobs.Domain.Aggregates.ServiceProject;
using Worfair.Modules.Jobs.Domain.Errors;

/// <summary>
/// Vitrine pública (descoberta cross-tenant): somente linhas PUBLICADAS/ABERTAS
/// atravessam tenants — o RLS no banco é a última linha de defesa e o endpoint
/// ainda barra rascunhos de outros tenants (ver JobsEndpoints).
/// </summary>
public sealed record ShowcaseJobPostingDto(
    Guid Id,
    Guid? CompanyId,
    Guid? CreatedBy,
    string Title,
    string Description,
    string? Location,
    int Remote,
    int Status,
    DateTime? PublishedAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    Guid TenantId,
    string? Category,
    string? CompanyName);

public sealed record ShowcaseServiceProjectDto(
    Guid Id,
    Guid? CompanyId,
    Guid? CreatedBy,
    string Title,
    string Description,
    decimal BudgetMin,
    decimal? BudgetMax,
    string Currency,
    DateTime? Deadline,
    int Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    Guid TenantId,
    string? Category,
    string? CompanyName);

public sealed record ListShowcasePostingsQuery(ShowcasePostingFilter Filter)
    : IQuery<Result<IReadOnlyList<ShowcaseJobPostingDto>>>;

public sealed class ListShowcasePostingsQueryHandler(IJobPostingRepository postings)
    : IQueryHandler<ListShowcasePostingsQuery, Result<IReadOnlyList<ShowcaseJobPostingDto>>>
{
    public async Task<Result<IReadOnlyList<ShowcaseJobPostingDto>>> Handle(
        ListShowcasePostingsQuery query, CancellationToken cancellationToken)
    {
        var list = await postings.ListPublishedAcrossTenantsAsync(query.Filter, cancellationToken).ConfigureAwait(false);
        return Result.Success<IReadOnlyList<ShowcaseJobPostingDto>>([.. list.Select(ToDto)]);
    }

    internal static ShowcaseJobPostingDto ToDto(JobPosting posting) => new(
        posting.Id.Value,
        posting.CompanyId,
        posting.CreatedBy,
        posting.Title,
        posting.Description,
        posting.Location,
        (int)posting.Remote,
        (int)posting.Status,
        posting.PublishedAtUtc,
        posting.CreatedAtUtc,
        posting.UpdatedAtUtc,
        posting.TenantId.Value,
        posting.Category,
        posting.CompanyName);
}

public sealed record GetShowcasePostingQuery(Guid JobPostingId) : IQuery<Result<ShowcaseJobPostingDto>>;

public sealed class GetShowcasePostingQueryHandler(IJobPostingRepository postings)
    : IQueryHandler<GetShowcasePostingQuery, Result<ShowcaseJobPostingDto>>
{
    public async Task<Result<ShowcaseJobPostingDto>> Handle(
        GetShowcasePostingQuery query, CancellationToken cancellationToken)
    {
        var posting = await postings
            .GetByIdAcrossTenantsAsync(new JobPostingId(query.JobPostingId), cancellationToken)
            .ConfigureAwait(false);

        return posting is null
            ? Result.Failure<ShowcaseJobPostingDto>(JobsErrors.NotFound)
            : Result.Success(ListShowcasePostingsQueryHandler.ToDto(posting));
    }
}

public sealed record ListShowcaseProjectsQuery(ShowcaseProjectFilter Filter)
    : IQuery<Result<IReadOnlyList<ShowcaseServiceProjectDto>>>;

public sealed class ListShowcaseProjectsQueryHandler(IServiceProjectRepository projects)
    : IQueryHandler<ListShowcaseProjectsQuery, Result<IReadOnlyList<ShowcaseServiceProjectDto>>>
{
    public async Task<Result<IReadOnlyList<ShowcaseServiceProjectDto>>> Handle(
        ListShowcaseProjectsQuery query, CancellationToken cancellationToken)
    {
        var list = await projects.ListOpenAcrossTenantsAsync(query.Filter, cancellationToken).ConfigureAwait(false);
        return Result.Success<IReadOnlyList<ShowcaseServiceProjectDto>>([.. list.Select(ToDto)]);
    }

    internal static ShowcaseServiceProjectDto ToDto(ServiceProject project) => new(
        project.Id.Value,
        project.CompanyId,
        project.CreatedBy,
        project.Title,
        project.Description,
        project.BudgetMin,
        project.BudgetMax,
        project.Currency,
        project.Deadline,
        (int)project.Status,
        project.CreatedAtUtc,
        project.UpdatedAtUtc,
        project.TenantId.Value,
        project.Category,
        project.CompanyName);
}

public sealed record GetShowcaseProjectQuery(Guid ServiceProjectId) : IQuery<Result<ShowcaseServiceProjectDto>>;

public sealed class GetShowcaseProjectQueryHandler(IServiceProjectRepository projects)
    : IQueryHandler<GetShowcaseProjectQuery, Result<ShowcaseServiceProjectDto>>
{
    public async Task<Result<ShowcaseServiceProjectDto>> Handle(
        GetShowcaseProjectQuery query, CancellationToken cancellationToken)
    {
        var project = await projects
            .GetByIdAcrossTenantsAsync(new ServiceProjectId(query.ServiceProjectId), cancellationToken)
            .ConfigureAwait(false);

        return project is null
            ? Result.Failure<ShowcaseServiceProjectDto>(JobsErrors.NotFound)
            : Result.Success(ListShowcaseProjectsQueryHandler.ToDto(project));
    }
}
