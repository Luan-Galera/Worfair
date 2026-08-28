namespace Worfair.Modules.Recruitment.Application.JobRequisitions;

using Worfair.BuildingBlocks.Application.Cqrs;
using Worfair.BuildingBlocks.Application.Security;
using Worfair.Modules.Recruitment.Domain.ValueObjects;

public sealed class CreateJobRequisitionCommandHandler(
    IJobRequisitionRepository requisitions,
    IRecruitmentUnitOfWork unitOfWork,
    ITenantProvider tenantProvider,
    ICurrentUser currentUser)
    : ICommandHandler<CreateJobRequisitionCommand, Result<JobRequisitionDto>>
{
    public async Task<Result<JobRequisitionDto>> Handle(
        CreateJobRequisitionCommand command, CancellationToken cancellationToken)
    {
        if (tenantProvider.TenantId is null)
            return Result.Failure<JobRequisitionDto>(RecruitmentErrors.TenantRequired);

        var moneyResult = Money.Create(command.SalaryMin, command.Currency);
        if (moneyResult.IsFailure)
            return Result.Failure<JobRequisitionDto>(moneyResult.Error!);

        Money? maximum = null;
        if (command.SalaryMax.HasValue)
        {
            var maxResult = Money.Create(command.SalaryMax.Value, command.Currency);
            if (maxResult.IsFailure)
                return Result.Failure<JobRequisitionDto>(maxResult.Error!);

            maximum = maxResult.Value;
        }

        var rangeResult = SalaryRange.Create(moneyResult.Value, maximum);
        if (rangeResult.IsFailure)
            return Result.Failure<JobRequisitionDto>(rangeResult.Error!);

        var createResult = JobRequisition.Create(
            command.CompanyId,
            currentUser.UserId,
            command.Title,
            command.Description,
            rangeResult.Value);

        if (createResult.IsFailure)
            return Result.Failure<JobRequisitionDto>(createResult.Error!);

        var requisition = createResult.Value;
        await requisitions.AddAsync(requisition, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToDto(requisition);
    }

    internal static JobRequisitionDto ToDto(JobRequisition r) => new(
        r.Id.Value,
        r.CompanyId,
        r.Title.Value,
        r.Description,
        r.SalaryRange.Minimum.Amount,
        r.SalaryRange.Maximum?.Amount,
        r.SalaryRange.Minimum.Currency,
        (int)r.Status,
        r.CreatedBy,
        r.CreatedAtUtc,
        r.PublishedAtUtc,
        r.ClosedAtUtc,
        r.CloseReason,
        [.. r.HiringTeam.Select(m => new HiringTeamMemberDto(m.RecruiterUserId, (int)m.Role, m.AddedAtUtc))]);
}
