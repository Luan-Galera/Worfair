namespace Worfair.Modules.Jobs.Domain.Aggregates.Proposal;

using Worfair.BuildingBlocks.Domain.Abstractions;
using Worfair.BuildingBlocks.Domain.Errors;
using Worfair.Modules.Jobs.Domain.Abstractions;
using Worfair.Modules.Jobs.Domain.Aggregates.JobPosting;
using Worfair.Modules.Jobs.Domain.Aggregates.ServiceProject;
using Worfair.Modules.Jobs.Domain.Errors;

public enum ProposalStatus
{
    Submitted = 1,
    Accepted = 2,
    Rejected = 3,
    Withdrawn = 4
}

public sealed class Proposal : TenantAggregateRoot<ProposalId>
{
    private Proposal()
    {
    }

    private Proposal(
        ProposalId id,
        Guid? jobPostingId,
        Guid? serviceProjectId,
        Guid providerUserId,
        Guid? providerCompanyId,
        string message,
        decimal amount,
        string currency,
        DateTime utcNow)
    {
        Id = id;
        JobPostingId = jobPostingId;
        ServiceProjectId = serviceProjectId;
        ProviderUserId = providerUserId;
        ProviderCompanyId = providerCompanyId;
        Message = message.Trim();
        Amount = amount;
        Currency = currency.Trim().ToUpperInvariant();
        Status = ProposalStatus.Submitted;
        CreatedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    public Guid? JobPostingId { get; private set; }
    public Guid? ServiceProjectId { get; private set; }
    public Guid ProviderUserId { get; private set; }
    public Guid? ProviderCompanyId { get; private set; }
    public string Message { get; private set; } = default!;
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = default!;
    public ProposalStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    public static Result<Proposal> Create(
        Guid? jobPostingId,
        Guid? serviceProjectId,
        Guid providerUserId,
        Guid? providerCompanyId,
        string? message,
        decimal amount,
        string? currency,
        DateTime? utcNow = null)
    {
        if (jobPostingId is null && serviceProjectId is null)
            return Result.Failure<Proposal>(JobsErrors.ProposalTargetRequired);
        if (jobPostingId is not null && serviceProjectId is not null)
            return Result.Failure<Proposal>(JobsErrors.ProposalSingleTargetRequired);
        if (string.IsNullOrWhiteSpace(message))
            return Result.Failure<Proposal>(JobsErrors.ProposalMessageRequired);
        if (amount <= 0)
            return Result.Failure<Proposal>(JobsErrors.InvalidProposalAmount);

        var normalizedCurrency = (currency ?? "BRL").Trim();
        if (normalizedCurrency.Length != 3)
            return Result.Failure<Proposal>(JobsErrors.InvalidCurrency);

        return new Proposal(new ProposalId(Guid.NewGuid()), jobPostingId, serviceProjectId,
            providerUserId, providerCompanyId, message, amount, normalizedCurrency, utcNow ?? DateTime.UtcNow);
    }

    public Result Decide(bool accept)
    {
        if (Status != ProposalStatus.Submitted)
            return Result.Failure(JobsErrors.InvalidProposalStatus);

        Status = accept ? ProposalStatus.Accepted : ProposalStatus.Rejected;
        UpdatedAtUtc = DateTime.UtcNow;
        return Result.Success();
    }
}

public readonly record struct ProposalId(Guid Value)
{
    public static implicit operator Guid(ProposalId id) => id.Value;
    public override string ToString() => Value.ToString();
}
