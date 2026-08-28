namespace Worfair.Modules.Recruitment.Application.Interviews;

using Worfair.BuildingBlocks.Application.Cqrs;
using Worfair.BuildingBlocks.Application.Ports;
using Worfair.BuildingBlocks.Application.Security;

public sealed class ScheduleInterviewCommandHandler(
    IJobRequisitionRepository requisitions,
    ICandidateRepository candidates,
    IInterviewRepository interviews,
    IRecruitmentUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : ICommandHandler<ScheduleInterviewCommand, Result<InterviewDto>>
{
    public async Task<Result<InterviewDto>> Handle(
        ScheduleInterviewCommand command, CancellationToken cancellationToken)
    {
        // Invariante cross-aggregate: candidato e requisição devem existir no tenant.
        var requisitionExists = await requisitions
            .GetByIdAsync(new JobRequisitionId(command.JobRequisitionId), cancellationToken)
            .ConfigureAwait(false) is not null;
        if (!requisitionExists)
            return Result.Failure<InterviewDto>(RecruitmentErrors.NotFound);

        var candidate = await candidates
            .GetByIdAsync(new CandidateId(command.CandidateId), cancellationToken)
            .ConfigureAwait(false);
        if (candidate is null)
            return Result.Failure<InterviewDto>(CandidateErrors.NotFound);

        var scheduleResult = Interview.Schedule(
            command.JobRequisitionId, command.CandidateId, command.ScheduledAtUtc,
            command.DurationMinutes, (InterviewType)command.Type, clock.UtcNow);

        if (scheduleResult.IsFailure)
            return Result.Failure<InterviewDto>(scheduleResult.Error!);

        var interview = scheduleResult.Value;
        await interviews.AddAsync(interview, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToDto(interview);
    }

    internal static InterviewDto ToDto(Interview i) => new(
        i.Id.Value,
        i.JobRequisitionId,
        i.CandidateId,
        i.ScheduledAtUtc,
        i.DurationMinutes,
        (int)i.Type,
        (int)i.Status,
        i.CompletedAtUtc,
        i.CancelledAtUtc,
        [.. i.Feedbacks.Select(f => new InterviewFeedbackDto(f.InterviewerUserId, f.Rating, f.Notes, f.SubmittedAtUtc))]);
}

public sealed class AddInterviewFeedbackCommandHandler(
    IInterviewRepository interviews,
    IRecruitmentUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    ICurrentUser currentUser)
    : ICommandHandler<AddInterviewFeedbackCommand>
{
    public async Task<Result> Handle(AddInterviewFeedbackCommand command, CancellationToken cancellationToken)
    {
        var interview = await interviews
            .GetByIdAsync(new InterviewId(command.InterviewId), cancellationToken)
            .ConfigureAwait(false);

        if (interview is null)
            return Result.Failure(InterviewErrors.NotFound);

        var result = interview.AddFeedback(currentUser.UserId ?? Guid.Empty, command.Rating, command.Notes, clock.UtcNow);
        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return result;
    }
}

public sealed class CompleteInterviewCommandHandler(
    IInterviewRepository interviews,
    IRecruitmentUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : ICommandHandler<CompleteInterviewCommand>
{
    public async Task<Result> Handle(CompleteInterviewCommand command, CancellationToken cancellationToken)
    {
        var interview = await interviews
            .GetByIdAsync(new InterviewId(command.InterviewId), cancellationToken)
            .ConfigureAwait(false);

        if (interview is null)
            return Result.Failure(InterviewErrors.NotFound);

        var result = interview.Complete(clock.UtcNow);
        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return result;
    }
}

public sealed class CancelInterviewCommandHandler(
    IInterviewRepository interviews,
    IRecruitmentUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : ICommandHandler<CancelInterviewCommand>
{
    public async Task<Result> Handle(CancelInterviewCommand command, CancellationToken cancellationToken)
    {
        var interview = await interviews
            .GetByIdAsync(new InterviewId(command.InterviewId), cancellationToken)
            .ConfigureAwait(false);

        if (interview is null)
            return Result.Failure(InterviewErrors.NotFound);

        var result = interview.Cancel(clock.UtcNow);
        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return result;
    }
}
