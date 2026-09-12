namespace Worfair.Modules.Jobs.Domain.Aggregates.JobApplication;

using Worfair.BuildingBlocks.Domain.Abstractions;
using Worfair.BuildingBlocks.Domain.Errors;
using Worfair.Modules.Jobs.Domain.Abstractions;
using Worfair.Modules.Jobs.Domain.Aggregates.JobPosting;
using Worfair.Modules.Jobs.Domain.Errors;

public enum JobApplicationStatus
{
    Submitted = 1,
    InReview = 2,
    Accepted = 3,
    Rejected = 4,
    Withdrawn = 5
}

public sealed class JobApplication : TenantAggregateRoot<JobApplicationId>
{
    private JobApplication()
    {
    }

    private JobApplication(
        JobApplicationId id,
        JobPostingId jobPostingId,
        Guid applicantUserId,
        Guid? applicantCompanyId,
        string message,
        DateTime utcNow)
    {
        Id = id;
        JobPostingId = jobPostingId;
        ApplicantUserId = applicantUserId;
        ApplicantCompanyId = applicantCompanyId;
        Message = message.Trim();
        Status = JobApplicationStatus.Submitted;
        CreatedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    public JobPostingId JobPostingId { get; private set; }

    public Guid ApplicantUserId { get; private set; }

    public Guid? ApplicantCompanyId { get; private set; }

    public string Message { get; private set; } = default!;

    public JobApplicationStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? UpdatedAtUtc { get; private set; }

    public static Result<JobApplication> Create(
        JobPosting posting,
        Guid applicantUserId,
        Guid? applicantCompanyId,
        string? message,
        DateTime? utcNow = null)
    {
        if (posting.Status != JobPostingStatus.Published)
            return Result.Failure<JobApplication>(JobsErrors.JobPostingNotOpen);

        if (string.IsNullOrWhiteSpace(message))
            return Result.Failure<JobApplication>(JobsErrors.ApplicationMessageRequired);

        return new JobApplication(
            new JobApplicationId(Guid.NewGuid()),
            posting.Id,
            applicantUserId,
            applicantCompanyId,
            message,
            utcNow ?? DateTime.UtcNow);
    }
}

public readonly record struct JobApplicationId(Guid Value)
{
    public static implicit operator Guid(JobApplicationId id) => id.Value;
    public override string ToString() => Value.ToString();
}
