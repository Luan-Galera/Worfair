namespace Worfair.Modules.Jobs.Domain.Aggregates.Dispute;

using Worfair.BuildingBlocks.Domain.Abstractions;
using Worfair.BuildingBlocks.Domain.Errors;
using Worfair.Modules.Jobs.Domain.Abstractions;
using Worfair.Modules.Jobs.Domain.Errors;

public enum DisputeStatus
{
    Open = 1,
    UnderReview = 2,
    Resolved = 3,
    Closed = 4
}

public sealed class Dispute : TenantAggregateRoot<DisputeId>
{
    private Dispute() { }

    private Dispute(DisputeId id, Guid? jobPostingId, Guid? serviceProjectId, Guid openedByUserId, string reason, DateTime utcNow)
    {
        Id = id;
        JobPostingId = jobPostingId;
        ServiceProjectId = serviceProjectId;
        OpenedByUserId = openedByUserId;
        Reason = reason.Trim();
        Status = DisputeStatus.Open;
        CreatedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    public Guid? JobPostingId { get; private set; }
    public Guid? ServiceProjectId { get; private set; }
    public Guid OpenedByUserId { get; private set; }
    public string Reason { get; private set; } = default!;
    public DisputeStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public string? ResolutionNotes { get; private set; }

    public static Result<Dispute> Open(Guid? jobPostingId, Guid? serviceProjectId, Guid openedByUserId, string reason, DateTime? utcNow = null)
    {
        if (jobPostingId is null && serviceProjectId is null)
            return Result.Failure<Dispute>(JobsErrors.DisputeTargetRequired);
        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure<Dispute>(JobsErrors.DisputeReasonRequired);
        return new Dispute(new DisputeId(Guid.NewGuid()), jobPostingId, serviceProjectId, openedByUserId, reason, utcNow ?? DateTime.UtcNow);
    }

    public Result Resolve(string notes)
    {
        if (Status != DisputeStatus.Open && Status != DisputeStatus.UnderReview)
            return Result.Failure(JobsErrors.InvalidDisputeStatus);
        ResolutionNotes = notes.Trim();
        Status = DisputeStatus.Resolved;
        UpdatedAtUtc = DateTime.UtcNow;
        return Result.Success();
    }
}

public readonly record struct DisputeId(Guid Value)
{
    public static implicit operator Guid(DisputeId id) => id.Value;
    public override string ToString() => Value.ToString();
}
