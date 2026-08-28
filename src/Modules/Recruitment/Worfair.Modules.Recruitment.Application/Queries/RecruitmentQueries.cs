namespace Worfair.Modules.Recruitment.Application.Queries;

using Worfair.BuildingBlocks.Application.Cqrs;
using Worfair.Modules.Recruitment.Application.Candidates;
using Worfair.Modules.Recruitment.Application.Dtos;
using Worfair.Modules.Recruitment.Application.Interviews;
using Worfair.Modules.Recruitment.Application.JobRequisitions;

public sealed record GetJobRequisitionByIdQuery(Guid JobRequisitionId) : IQuery<Result<JobRequisitionDto>>;

public sealed class GetJobRequisitionByIdQueryHandler(IJobRequisitionRepository requisitions)
    : IQueryHandler<GetJobRequisitionByIdQuery, Result<JobRequisitionDto>>
{
    public async Task<Result<JobRequisitionDto>> Handle(
        GetJobRequisitionByIdQuery query, CancellationToken cancellationToken)
    {
        var requisition = await requisitions
            .GetByIdAsync(new JobRequisitionId(query.JobRequisitionId), cancellationToken)
            .ConfigureAwait(false);

        return requisition is null
            ? Result.Failure<JobRequisitionDto>(RecruitmentErrors.NotFound)
            : CreateJobRequisitionCommandHandler.ToDto(requisition);
    }
}

public sealed record ListJobRequisitionsQuery : IQuery<Result<IReadOnlyList<JobRequisitionDto>>>;

public sealed class ListJobRequisitionsQueryHandler(IJobRequisitionRepository requisitions)
    : IQueryHandler<ListJobRequisitionsQuery, Result<IReadOnlyList<JobRequisitionDto>>>
{
    public async Task<Result<IReadOnlyList<JobRequisitionDto>>> Handle(
        ListJobRequisitionsQuery query, CancellationToken cancellationToken)
    {
        var list = await requisitions.ListByTenantAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success<IReadOnlyList<JobRequisitionDto>>(
            [.. list.Select(CreateJobRequisitionCommandHandler.ToDto).OrderByDescending(r => r.CreatedAtUtc)]);
    }
}

public sealed record GetCandidateByIdQuery(Guid CandidateId) : IQuery<Result<CandidateDto>>;

public sealed class GetCandidateByIdQueryHandler(ICandidateRepository candidates)
    : IQueryHandler<GetCandidateByIdQuery, Result<CandidateDto>>
{
    public async Task<Result<CandidateDto>> Handle(
        GetCandidateByIdQuery query, CancellationToken cancellationToken)
    {
        var candidate = await candidates
            .GetByIdAsync(new CandidateId(query.CandidateId), cancellationToken)
            .ConfigureAwait(false);

        return candidate is null
            ? Result.Failure<CandidateDto>(CandidateErrors.NotFound)
            : CreateCandidateCommandHandler.ToDto(candidate);
    }
}

public sealed record ListCandidatesQuery : IQuery<Result<IReadOnlyList<CandidateDto>>>;

public sealed class ListCandidatesQueryHandler(ICandidateRepository candidates)
    : IQueryHandler<ListCandidatesQuery, Result<IReadOnlyList<CandidateDto>>>
{
    public async Task<Result<IReadOnlyList<CandidateDto>>> Handle(
        ListCandidatesQuery query, CancellationToken cancellationToken)
    {
        var list = await candidates.ListByTenantAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success<IReadOnlyList<CandidateDto>>(
            [.. list.Select(CreateCandidateCommandHandler.ToDto).OrderByDescending(c => c.CreatedAtUtc)]);
    }
}

public sealed record ListInterviewsByCandidateQuery(Guid CandidateId) : IQuery<Result<IReadOnlyList<InterviewDto>>>;

public sealed class ListInterviewsByCandidateQueryHandler(
    ICandidateRepository candidates,
    IInterviewRepository interviews)
    : IQueryHandler<ListInterviewsByCandidateQuery, Result<IReadOnlyList<InterviewDto>>>
{
    public async Task<Result<IReadOnlyList<InterviewDto>>> Handle(
        ListInterviewsByCandidateQuery query, CancellationToken cancellationToken)
    {
        // 404 indistinguível se o candidato (ou suas entrevistas) for de outro tenant.
        var candidate = await candidates
            .GetByIdAsync(new CandidateId(query.CandidateId), cancellationToken)
            .ConfigureAwait(false);
        if (candidate is null)
            return Result.Failure<IReadOnlyList<InterviewDto>>(CandidateErrors.NotFound);

        var list = await interviews.ListByCandidateAsync(query.CandidateId, cancellationToken).ConfigureAwait(false);

        return Result.Success<IReadOnlyList<InterviewDto>>(
            [.. list.Select(ScheduleInterviewCommandHandler.ToDto).OrderBy(i => i.ScheduledAtUtc)]);
    }
}
