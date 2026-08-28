namespace Worfair.Modules.Recruitment.Domain.Aggregates.Candidate;

using Worfair.BuildingBlocks.Domain.Tenancy;

/// <summary>
/// Registro imutável de cada mudança de estágio (append-only — docs/database/03 §5).
/// </summary>
public sealed class CandidateStageHistoryEntry : ITenantEntity
{
    private CandidateStageHistoryEntry()
    {
        // EF Core
    }

    private CandidateStageHistoryEntry(Guid id, CandidateStatus fromStatus, CandidateStatus toStatus, Guid changedBy, DateTime changedAtUtc)
    {
        Id = id;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        ChangedBy = changedBy;
        ChangedAtUtc = changedAtUtc;
    }

    public Guid Id { get; private set; }

    public TenantId TenantId { get; private set; }

    public CandidateStatus FromStatus { get; private set; }

    public CandidateStatus ToStatus { get; private set; }

    public Guid ChangedBy { get; private set; }

    public DateTime ChangedAtUtc { get; private set; }

    public static CandidateStageHistoryEntry Create(CandidateStatus fromStatus, CandidateStatus toStatus, Guid changedBy, DateTime utcNow) =>
        new(Guid.NewGuid(), fromStatus, toStatus, changedBy, utcNow);

    void ITenantEntity.SetTenantId(TenantId tenantId)
    {
        if (TenantId != default && TenantId != tenantId)
            throw new InvalidOperationException("TenantId de um registro de histórico já nascido não é alterável.");

        TenantId = tenantId;
    }
}
