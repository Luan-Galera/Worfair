namespace Worfair.Modules.Jobs.Application.JobApplications;

using FluentValidation;
using Worfair.BuildingBlocks.Application.Cqrs;
using Worfair.BuildingBlocks.Application.Security;
using Worfair.BuildingBlocks.Domain.Errors;
using Worfair.BuildingBlocks.Domain.Tenancy;
using Worfair.Modules.Jobs.Application.Abstractions;
using Worfair.Modules.Jobs.Application.Dtos;
using Worfair.Modules.Jobs.Domain.Abstractions;
using Worfair.Modules.Jobs.Domain.Aggregates.JobApplication;
using Worfair.Modules.Jobs.Domain.Aggregates.JobPosting;
using Worfair.Modules.Jobs.Domain.Errors;

public sealed record ApplyToJobCommand(Guid JobPostingId, Guid? ApplicantCompanyId, string Message)
    : ICommand<Result<JobApplicationDto>>;

public sealed class ApplyToJobCommandValidator : AbstractValidator<ApplyToJobCommand>
{
    public ApplyToJobCommandValidator()
    {
        RuleFor(x => x.Message).NotEmpty().WithErrorCode("Jobs.ApplicationMessageRequired");
    }
}

public sealed class ApplyToJobCommandHandler(
    IJobPostingRepository postings,
    IJobApplicationRepository applications,
    IJobsUnitOfWork unitOfWork,
    ITenantProvider tenantProvider,
    ICurrentUser currentUser)
    : ICommandHandler<ApplyToJobCommand, Result<JobApplicationDto>>
{
    public async Task<Result<JobApplicationDto>> Handle(
        ApplyToJobCommand command, CancellationToken cancellationToken)
    {
        if (tenantProvider.TenantId is null || currentUser.UserId is not { } userId)
            return Result.Failure<JobApplicationDto>(JobsErrors.NotFound);

        var posting = await postings.GetByIdAsync(
            new JobPostingId(command.JobPostingId), cancellationToken).ConfigureAwait(false);
        if (posting is null)
            return Result.Failure<JobApplicationDto>(JobsErrors.NotFound);

        if (await applications.ExistsAsync(posting.Id, userId, cancellationToken).ConfigureAwait(false))
            return Result.Failure<JobApplicationDto>(JobsErrors.ApplicationAlreadyExists);

        var result = JobApplication.Create(
            posting, userId, command.ApplicantCompanyId, command.Message, DateTime.UtcNow);
        if (result.IsFailure)
            return Result.Failure<JobApplicationDto>(result.Error!);

        await applications.AddAsync(result.Value, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToDto(result.Value);
    }

    internal static JobApplicationDto ToDto(JobApplication application) => new(
        application.Id.Value,
        application.JobPostingId.Value,
        application.ApplicantUserId,
        application.ApplicantCompanyId,
        application.Message,
        (int)application.Status,
        application.CreatedAtUtc,
        application.UpdatedAtUtc);
}
