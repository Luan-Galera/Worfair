namespace Worfair.Modules.Jobs.Application.Queries;

using Worfair.BuildingBlocks.Application.Cqrs;
using Worfair.BuildingBlocks.Domain.Errors;
using Worfair.Modules.Jobs.Application.Dtos;
using Worfair.Modules.Jobs.Domain.Abstractions;
using Worfair.Modules.Jobs.Domain.Aggregates.JobPosting;
using Worfair.Modules.Jobs.Domain.Aggregates.ServiceProject;
using Worfair.Modules.Jobs.Domain.Errors;

public sealed record GetJobPostingByIdQuery(Guid JobPostingId) : IQuery<Result<JobPostingDto>>;

public sealed class GetJobPostingByIdQueryHandler(IJobPostingRepository postings)
    : IQueryHandler<GetJobPostingByIdQuery, Result<JobPostingDto>>
{
    public async Task<Result<JobPostingDto>> Handle(
        GetJobPostingByIdQuery query, CancellationToken cancellationToken)
    {
        var posting = await postings
            .GetByIdAsync(new JobPostingId(query.JobPostingId), cancellationToken)
            .ConfigureAwait(false);

        return posting is null
            ? Result.Failure<JobPostingDto>(JobsErrors.NotFound)
            : Result.Success(ToDto(posting));
    }

    internal static JobPostingDto ToDto(JobPosting posting) => new(
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
        posting.Category,
        posting.CompanyName);
}

public sealed record ListPublishedJobPostingsQuery : IQuery<Result<IReadOnlyList<JobPostingDto>>>;

public sealed class ListPublishedJobPostingsQueryHandler(IJobPostingRepository postings)
    : IQueryHandler<ListPublishedJobPostingsQuery, Result<IReadOnlyList<JobPostingDto>>>
{
    public async Task<Result<IReadOnlyList<JobPostingDto>>> Handle(
        ListPublishedJobPostingsQuery query, CancellationToken cancellationToken)
    {
        var list = await postings.ListByTenantAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success<IReadOnlyList<JobPostingDto>>(
            [.. list
                .Where(posting => posting.Status == JobPostingStatus.Published)
                .Select(GetJobPostingByIdQueryHandler.ToDto)
                .OrderByDescending(posting => posting.PublishedAtUtc)]);
    }
}

public sealed record GetServiceProjectByIdQuery(Guid ServiceProjectId) : IQuery<Result<ServiceProjectDto>>;

public sealed class GetServiceProjectByIdQueryHandler(IServiceProjectRepository projects)
    : IQueryHandler<GetServiceProjectByIdQuery, Result<ServiceProjectDto>>
{
    public async Task<Result<ServiceProjectDto>> Handle(
        GetServiceProjectByIdQuery query, CancellationToken cancellationToken)
    {
        var project = await projects
            .GetByIdAsync(new ServiceProjectId(query.ServiceProjectId), cancellationToken)
            .ConfigureAwait(false);

        return project is null
            ? Result.Failure<ServiceProjectDto>(JobsErrors.NotFound)
            : Result.Success(ToDto(project));
    }

    internal static ServiceProjectDto ToDto(ServiceProject project) => new(
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
        project.Category,
        project.CompanyName);
}

public sealed record ListOpenServiceProjectsQuery : IQuery<Result<IReadOnlyList<ServiceProjectDto>>>;

public sealed class ListOpenServiceProjectsQueryHandler(IServiceProjectRepository projects)
    : IQueryHandler<ListOpenServiceProjectsQuery, Result<IReadOnlyList<ServiceProjectDto>>>
{
    public async Task<Result<IReadOnlyList<ServiceProjectDto>>> Handle(
        ListOpenServiceProjectsQuery query, CancellationToken cancellationToken)
    {
        var list = await projects.ListByTenantAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success<IReadOnlyList<ServiceProjectDto>>(
            [.. list
                .Where(project => project.Status == ServiceProjectStatus.Open)
                .Select(GetServiceProjectByIdQueryHandler.ToDto)
                .OrderByDescending(project => project.CreatedAtUtc)]);
    }
}
