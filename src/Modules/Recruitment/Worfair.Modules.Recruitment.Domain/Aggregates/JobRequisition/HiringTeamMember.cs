namespace Worfair.Modules.Recruitment.Domain.Aggregates.JobRequisition;

using Worfair.BuildingBlocks.Domain.Tenancy;

/// <summary>
/// Membro do time de contratação (entidade filha — docs/database/03 §5).
/// PK (job_requisition_id, recruiter_user_id); um mesmo recrutador não se repete.
/// </summary>
public sealed class HiringTeamMember : ITenantEntity
{
    private HiringTeamMember()
    {
        // EF Core
    }

    private HiringTeamMember(Guid recruiterUserId, HiringRole role, DateTime addedAtUtc)
    {
        RecruiterUserId = recruiterUserId;
        Role = role;
        AddedAtUtc = addedAtUtc;
    }

    public TenantId TenantId { get; private set; }

    public Guid RecruiterUserId { get; private set; }

    public HiringRole Role { get; private set; }

    public DateTime AddedAtUtc { get; private set; }

    public static HiringTeamMember Create(Guid recruiterUserId, HiringRole role, DateTime utcNow) =>
        new(recruiterUserId, role, utcNow);

    void ITenantEntity.SetTenantId(TenantId tenantId)
    {
        if (TenantId != default && TenantId != tenantId)
            throw new InvalidOperationException("TenantId de um membro do time já nascido não é alterável.");

        TenantId = tenantId;
    }
}
