namespace Worfair.Modules.Recruitment.Application.Candidates;

using Worfair.BuildingBlocks.Application.Cqrs;
using Worfair.BuildingBlocks.Application.Ports;
using Worfair.BuildingBlocks.Application.Security;

public sealed class CreateCandidateCommandHandler(
    ICandidateRepository candidates,
    IRecruitmentUnitOfWork unitOfWork,
    ITenantProvider tenantProvider,
    IDateTimeProvider clock)
    : ICommandHandler<CreateCandidateCommand, Result<CandidateDto>>
{
    public async Task<Result<CandidateDto>> Handle(
        CreateCandidateCommand command, CancellationToken cancellationToken)
    {
        if (tenantProvider.TenantId is null)
            return Result.Failure<CandidateDto>(CandidateErrors.TenantRequired);

        var source = (CandidateSource)command.Source;
        var createResult = Candidate.Create(
            command.FullName, command.Email, command.Phone, source, command.ResumeUrl, utcNow: clock.UtcNow);

        if (createResult.IsFailure)
            return Result.Failure<CandidateDto>(createResult.Error!);

        var candidate = createResult.Value;
        if (await candidates.EmailExistsAsync(candidate.Email, cancellationToken).ConfigureAwait(false))
            return Result.Failure<CandidateDto>(CandidateErrors.ContactEmailDuplicated);

        await candidates.AddAsync(candidate, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToDto(candidate);
    }

    internal static CandidateDto ToDto(Candidate c) => new(
        c.Id.Value,
        c.UserId,
        c.FullName,
        c.Email.Value,
        c.Phone,
        (int)c.Source,
        (int)c.Status,
        c.ResumeUrl,
        c.CreatedAtUtc,
        [.. c.History.Select(h => new StageHistoryDto((int)h.FromStatus, (int)h.ToStatus, h.ChangedBy, h.ChangedAtUtc))]);
}

public sealed class AdvanceCandidateCommandHandler(
    ICandidateRepository candidates,
    IInterviewRepository interviews,
    IRecruitmentUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    ICurrentUser currentUser)
    : ICommandHandler<AdvanceCandidateCommand, Result<CandidateDto>>
{
    public async Task<Result<CandidateDto>> Handle(
        AdvanceCandidateCommand command, CancellationToken cancellationToken)
    {
        var candidate = await candidates
            .GetByIdAsync(new CandidateId(command.CandidateId), cancellationToken)
            .ConfigureAwait(false);

        if (candidate is null)
            return Result.Failure<CandidateDto>(CandidateErrors.NotFound);

        var target = (CandidateStatus)command.TargetStage;
        var hasScheduledInterview = target == CandidateStatus.Interviewing &&
            await interviews.HasScheduledForCandidateAsync(candidate.Id.Value, cancellationToken).ConfigureAwait(false);

        var result = candidate.Advance(target, hasScheduledInterview, currentUser.UserId ?? Guid.Empty, clock.UtcNow);
        if (result.IsFailure)
            return Result.Failure<CandidateDto>(result.Error!);

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false); // CandidateHired → outbox

        return CreateCandidateCommandHandler.ToDto(candidate);
    }
}

public sealed class RejectCandidateCommandHandler(
    ICandidateRepository candidates,
    IRecruitmentUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    ICurrentUser currentUser)
    : ICommandHandler<RejectCandidateCommand>
{
    public async Task<Result> Handle(RejectCandidateCommand command, CancellationToken cancellationToken)
    {
        var candidate = await candidates
            .GetByIdAsync(new CandidateId(command.CandidateId), cancellationToken)
            .ConfigureAwait(false);

        if (candidate is null)
            return Result.Failure(CandidateErrors.NotFound);

        var result = candidate.Reject(currentUser.UserId ?? Guid.Empty, clock.UtcNow);
        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return result;
    }
}

public sealed class HireCandidateCommandHandler(
    ICandidateRepository candidates,
    IRecruitmentUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    ICurrentUser currentUser)
    : ICommandHandler<HireCandidateCommand>
{
    public async Task<Result> Handle(HireCandidateCommand command, CancellationToken cancellationToken)
    {
        var candidate = await candidates
            .GetByIdAsync(new CandidateId(command.CandidateId), cancellationToken)
            .ConfigureAwait(false);

        if (candidate is null)
            return Result.Failure(CandidateErrors.NotFound);

        var result = candidate.Advance(
            CandidateStatus.Hired, hasScheduledInterview: false, currentUser.UserId ?? Guid.Empty, clock.UtcNow);
        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false); // evento → outbox

        return result;
    }
}
