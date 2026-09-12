namespace Worfair.Modules.Jobs.Application.JobPostings;

using Worfair.BuildingBlocks.Application.Cqrs;
using Worfair.BuildingBlocks.Domain.Errors;
using Worfair.Modules.Jobs.Application.Abstractions;
using Worfair.Modules.Jobs.Application.Dtos;
using Worfair.Modules.Jobs.Domain.Abstractions;
using Worfair.Modules.Jobs.Domain.Aggregates.JobPosting;
using Worfair.Modules.Jobs.Domain.Errors;

public sealed record PublishJobPostingCommand(Guid JobPostingId) : ICommand<Result<JobPostingDto>>;

public sealed class PublishJobPostingCommandHandler(
    IJobPostingRepository postings,
    IJobsUnitOfWork unitOfWork)
    : ICommandHandler<PublishJobPostingCommand, Result<JobPostingDto>>
{
    public async Task<Result<JobPostingDto>> Handle(PublishJobPostingCommand command, CancellationToken cancellationToken)
    {
        var posting = await postings.GetByIdAsync(new JobPostingId(command.JobPostingId), cancellationToken).ConfigureAwait(false);
        if (posting is null)
            return Result.Failure<JobPostingDto>(JobsErrors.NotFound);

        var result = posting.Publish(DateTime.UtcNow);
        if (result.IsFailure)
            return Result.Failure<JobPostingDto>(result.Error!);

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
