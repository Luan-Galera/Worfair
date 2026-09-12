namespace Worfair.Modules.Jobs.Domain.Aggregates.JobPosting;

using Worfair.BuildingBlocks.Domain.Abstractions;
using Worfair.BuildingBlocks.Domain.Tenancy;
using Worfair.BuildingBlocks.Domain.Errors;
using Worfair.Modules.Jobs.Domain.Abstractions;
using Worfair.Modules.Jobs.Domain.Errors;

public enum JobPostingStatus
{
    Draft = 1,
    Published = 2,
    Closed = 3,
    Archived = 4
}

public enum JobPostingRemoteType
{
    OnSite = 0,
    Hybrid = 1,
    Remote = 2
}

public sealed class JobPosting : TenantAggregateRoot<JobPostingId>
{
    private JobPosting()
    {
    }

    private JobPosting(
        JobPostingId id,
        Guid? companyId,
        Guid? createdBy,
        string title,
        string description,
        string? location,
        JobPostingRemoteType remote,
        DateTime utcNow,
        string? category = null,
        string? companyName = null)
    {
        Id = id;
        CompanyId = companyId;
        CompanyName = companyName;
        Category = category;
        CreatedBy = createdBy;
        Title = title.Trim();
        Description = description.Trim();
        Location = location?.Trim();
        Remote = remote;
        Status = JobPostingStatus.Draft;
        CreatedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    public Guid? CompanyId { get; private set; }

    public string? CompanyName { get; private set; }

    public string? Category { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public string Title { get; private set; } = default!;

    public string Description { get; private set; } = default!;

    public string? Location { get; private set; }

    public JobPostingRemoteType Remote { get; private set; }

    public JobPostingStatus Status { get; private set; }

    public DateTime? PublishedAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? UpdatedAtUtc { get; private set; }

    public static Result<JobPosting> Create(
        Guid? companyId,
        Guid? createdBy,
        string? title,
        string? description,
        string? location,
        JobPostingRemoteType remote,
        DateTime? utcNow = null,
        string? category = null,
        string? companyName = null)
    {
        var now = utcNow ?? DateTime.UtcNow;

        if (string.IsNullOrWhiteSpace(title))
            return Result.Failure<JobPosting>(JobsErrors.TitleRequired);

        if (string.IsNullOrWhiteSpace(description))
            return Result.Failure<JobPosting>(JobsErrors.DescriptionRequired);

        if (title.Trim().Length > 200)
            return Result.Failure<JobPosting>(JobsErrors.TitleTooLong);

        var normalizedCategory = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        if (normalizedCategory is { Length: > 60 })
            return Result.Failure<JobPosting>(JobsErrors.CategoryTooLong);

        var normalizedCompanyName = string.IsNullOrWhiteSpace(companyName) ? null : companyName.Trim();
        if (normalizedCompanyName is { Length: > 120 })
            return Result.Failure<JobPosting>(JobsErrors.CompanyNameTooLong);

        return new JobPosting(new JobPostingId(Guid.NewGuid()), companyId, createdBy, title, description, location, remote, now, normalizedCategory, normalizedCompanyName);
    }

    public Result Publish(DateTime? utcNow = null)
    {
        if (Status == JobPostingStatus.Published)
            return Result.Success();

        Status = JobPostingStatus.Published;
        PublishedAtUtc = utcNow ?? DateTime.UtcNow;
        UpdatedAtUtc = PublishedAtUtc;
        return Result.Success();
    }

    public Result Close(DateTime? utcNow = null)
    {
        if (Status is JobPostingStatus.Closed or JobPostingStatus.Archived)
            return Result.Failure(JobsErrors.InvalidStatusTransition);

        Status = JobPostingStatus.Closed;
        UpdatedAtUtc = utcNow ?? DateTime.UtcNow;
        return Result.Success();
    }
}

public readonly record struct JobPostingId(Guid Value)
{
    public static implicit operator Guid(JobPostingId id) => id.Value;
    public override string ToString() => Value.ToString();
}
