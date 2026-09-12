namespace Worfair.Modules.Jobs.Application.JobPostings;

using FluentValidation;
using Worfair.BuildingBlocks.Application.Cqrs;
using Worfair.BuildingBlocks.Application.Security;
using Worfair.BuildingBlocks.Domain.Errors;
using Worfair.BuildingBlocks.Domain.Tenancy;
using Worfair.Modules.Jobs.Application.Abstractions;
using Worfair.Modules.Jobs.Application.Dtos;
using Worfair.Modules.Jobs.Domain.Abstractions;
using Worfair.Modules.Jobs.Domain.Aggregates.JobPosting;
using Worfair.Modules.Jobs.Domain.Errors;

public sealed record CreateJobPostingCommand(
    Guid? CompanyId,
    string Title,
    string Description,
    string? Location,
    int Remote,
    string? Category = null,
    string? CompanyName = null) : ICommand<Result<JobPostingDto>>;

public sealed class CreateJobPostingCommandValidator : AbstractValidator<CreateJobPostingCommand>
{
    public CreateJobPostingCommandValidator()
    {
        // Vaga de emprego é ato de EMPRESA: exige CompanyId (uma das empresas
        // do tenant, escolhida no picker). Trabalhos freelancer (ServiceProject)
        // seguem com empresa opcional — qualquer usuário pode publicar.
        RuleFor(x => x.CompanyId)
            .NotEmpty().WithErrorCode("Jobs.CompanyRequired");

        RuleFor(x => x.Title)
            .NotEmpty().WithErrorCode("Jobs.TitleRequired")
            .MaximumLength(200).WithErrorCode("Jobs.TitleTooLong");

        RuleFor(x => x.Description)
            .NotEmpty().WithErrorCode("Jobs.DescriptionRequired");

        RuleFor(x => x.Remote)
            .InclusiveBetween(0, 2).WithErrorCode("Jobs.InvalidRemoteType");

        RuleFor(x => x.Category)
            .MaximumLength(60).WithErrorCode("Jobs.CategoryTooLong");

        RuleFor(x => x.CompanyName)
            .MaximumLength(120).WithErrorCode("Jobs.CompanyNameTooLong");
    }
}

public sealed class CreateJobPostingCommandHandler(
    IJobPostingRepository postings,
    IJobsUnitOfWork unitOfWork,
    ITenantProvider tenantProvider,
    ICurrentUser currentUser)
    : ICommandHandler<CreateJobPostingCommand, Result<JobPostingDto>>
{
    public async Task<Result<JobPostingDto>> Handle(CreateJobPostingCommand command, CancellationToken cancellationToken)
    {
        if (tenantProvider.TenantId is null)
            return Result.Failure<JobPostingDto>(Worfair.Modules.Jobs.Domain.Errors.JobsErrors.NotFound);

        var remote = Enum.IsDefined(typeof(JobPostingRemoteType), command.Remote)
            ? (JobPostingRemoteType)command.Remote
            : JobPostingRemoteType.OnSite;

        var createResult = JobPosting.Create(
            command.CompanyId,
            currentUser.UserId,
            command.Title,
            command.Description,
            command.Location,
            remote,
            DateTime.UtcNow,
            command.Category,
            command.CompanyName);

        if (createResult.IsFailure)
            return Result.Failure<JobPostingDto>(createResult.Error!);

        var posting = createResult.Value;
        await postings.AddAsync(posting, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new JobPostingDto(
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
}
