namespace Worfair.Modules.Recruitment.Application.JobRequisitions;

using Worfair.BuildingBlocks.Application.Cqrs;
using Worfair.BuildingBlocks.Application.Ports;
using Worfair.Modules.Recruitment.Domain.ValueObjects;

public sealed class AddHiringTeamMemberCommandHandler(
    IJobRequisitionRepository requisitions,
    IRecruitmentUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : ICommandHandler<AddHiringTeamMemberCommand>
{
    public async Task<Result> Handle(AddHiringTeamMemberCommand command, CancellationToken cancellationToken)
    {
        var requisition = await requisitions
            .GetByIdAsync(new JobRequisitionId(command.JobRequisitionId), cancellationToken)
            .ConfigureAwait(false);

        if (requisition is null)
            return Result.Failure(RecruitmentErrors.NotFound);

        var result = requisition.AddTeamMember(command.RecruiterUserId, (HiringRole)command.Role, clock.UtcNow);
        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return result;
    }
}

public sealed class PublishJobRequisitionCommandHandler(
    IJobRequisitionRepository requisitions,
    IRecruitmentUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : ICommandHandler<PublishJobRequisitionCommand>
{
    public async Task<Result> Handle(PublishJobRequisitionCommand command, CancellationToken cancellationToken)
    {
        var requisition = await requisitions
            .GetByIdAsync(new JobRequisitionId(command.JobRequisitionId), cancellationToken)
            .ConfigureAwait(false);

        if (requisition is null)
            return Result.Failure(RecruitmentErrors.NotFound);

        var result = requisition.Publish(clock.UtcNow);
        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false); // evento → outbox

        return result;
    }
}

public sealed class PauseJobRequisitionCommandHandler(
    IJobRequisitionRepository requisitions,
    IRecruitmentUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : ICommandHandler<PauseJobRequisitionCommand>
{
    public async Task<Result> Handle(PauseJobRequisitionCommand command, CancellationToken cancellationToken)
    {
        var requisition = await requisitions
            .GetByIdAsync(new JobRequisitionId(command.JobRequisitionId), cancellationToken)
            .ConfigureAwait(false);

        if (requisition is null)
            return Result.Failure(RecruitmentErrors.NotFound);

        var result = requisition.Pause(clock.UtcNow);
        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return result;
    }
}

public sealed class ResumeJobRequisitionCommandHandler(
    IJobRequisitionRepository requisitions,
    IRecruitmentUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : ICommandHandler<ResumeJobRequisitionCommand>
{
    public async Task<Result> Handle(ResumeJobRequisitionCommand command, CancellationToken cancellationToken)
    {
        var requisition = await requisitions
            .GetByIdAsync(new JobRequisitionId(command.JobRequisitionId), cancellationToken)
            .ConfigureAwait(false);

        if (requisition is null)
            return Result.Failure(RecruitmentErrors.NotFound);

        var result = requisition.Resume(clock.UtcNow);
        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return result;
    }
}

public sealed class CloseJobRequisitionCommandHandler(
    IJobRequisitionRepository requisitions,
    IRecruitmentUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : ICommandHandler<CloseJobRequisitionCommand>
{
    public async Task<Result> Handle(CloseJobRequisitionCommand command, CancellationToken cancellationToken)
    {
        var requisition = await requisitions
            .GetByIdAsync(new JobRequisitionId(command.JobRequisitionId), cancellationToken)
            .ConfigureAwait(false);

        if (requisition is null)
            return Result.Failure(RecruitmentErrors.NotFound);

        var result = requisition.Close(command.Reason, clock.UtcNow);
        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return result;
    }
}

public sealed class CancelJobRequisitionCommandHandler(
    IJobRequisitionRepository requisitions,
    IRecruitmentUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : ICommandHandler<CancelJobRequisitionCommand>
{
    public async Task<Result> Handle(CancelJobRequisitionCommand command, CancellationToken cancellationToken)
    {
        var requisition = await requisitions
            .GetByIdAsync(new JobRequisitionId(command.JobRequisitionId), cancellationToken)
            .ConfigureAwait(false);

        if (requisition is null)
            return Result.Failure(RecruitmentErrors.NotFound);

        var result = requisition.Cancel(command.Reason, clock.UtcNow);
        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return result;
    }
}

public sealed class ChangeSalaryRangeCommandHandler(
    IJobRequisitionRepository requisitions,
    IRecruitmentUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : ICommandHandler<ChangeSalaryRangeCommand>
{
    public async Task<Result> Handle(ChangeSalaryRangeCommand command, CancellationToken cancellationToken)
    {
        var requisition = await requisitions
            .GetByIdAsync(new JobRequisitionId(command.JobRequisitionId), cancellationToken)
            .ConfigureAwait(false);

        if (requisition is null)
            return Result.Failure(RecruitmentErrors.NotFound);

        var moneyResult = Money.Create(command.SalaryMin, command.Currency);
        if (moneyResult.IsFailure)
            return Result.Failure(moneyResult.Error!);

        Money? maximum = null;
        if (command.SalaryMax.HasValue)
        {
            var maxResult = Money.Create(command.SalaryMax.Value, command.Currency);
            if (maxResult.IsFailure)
                return Result.Failure(maxResult.Error!);

            maximum = maxResult.Value;
        }

        var rangeResult = SalaryRange.Create(moneyResult.Value, maximum);
        if (rangeResult.IsFailure)
            return Result.Failure(rangeResult.Error!);

        var result = requisition.ChangeSalaryRange(rangeResult.Value, clock.UtcNow);
        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return result;
    }
}
