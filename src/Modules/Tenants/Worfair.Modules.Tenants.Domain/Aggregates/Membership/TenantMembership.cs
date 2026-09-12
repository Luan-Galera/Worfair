namespace Worfair.Modules.Tenants.Domain.Aggregates.Membership;

using Worfair.BuildingBlocks.Domain.Errors;
using Worfair.BuildingBlocks.Domain.Tenancy;
using Worfair.BuildingBlocks.Domain.ValueObjects;
using Worfair.Modules.Tenants.Domain.Errors;

/// <summary>
/// Vínculo usuário × tenant (tenant-owned; PK composta (tenant_id, user_id)).
/// Base para user_roles: role só existe com membership (R-05, docs/database/02).
/// </summary>
public sealed class TenantMembership : ITenantEntity
{
    private TenantMembership()
    {
        // EF Core
    }

    private TenantMembership(TenantId tenantId, Guid userId, MembershipStatus status, DateTime joinedAtUtc)
    {
        TenantId = tenantId;
        UserId = userId;
        Status = status;
        JoinedAtUtc = joinedAtUtc;
    }

    public TenantId TenantId { get; private set; }

    public Guid UserId { get; private set; }

    public MembershipStatus Status { get; private set; }

    public DateTime JoinedAtUtc { get; private set; }

    public static Result<TenantMembership> Add(
        Guid userId, TenantId? tenantId = null, DateTime? utcNow = null)
    {
        if (userId == Guid.Empty)
            return Result.Failure<TenantMembership>(TenantErrors.NotFound);

        return new TenantMembership(
            tenantId ?? TenantId.Empty,
            userId,
            MembershipStatus.Active,
            utcNow ?? DateTime.UtcNow);
    }

    /// <summary>Convida o usuário: entra como Invited e precisa ser ativado.</summary>
    public static Result<TenantMembership> Invite(Guid userId, DateTime? utcNow = null)
    {
        if (userId == Guid.Empty)
            return Result.Failure<TenantMembership>(TenantErrors.NotFound);

        return new TenantMembership(TenantId.Empty, userId, MembershipStatus.Invited, utcNow ?? DateTime.UtcNow);
    }

    public Result Activate()
    {
        if (Status is not (MembershipStatus.Invited or MembershipStatus.Disabled))
            return Result.Failure(MembershipErrors.InvalidStatusTransition);

        Status = MembershipStatus.Active;
        return Result.Success();
    }

    public Result Disable()
    {
        if (Status is not (MembershipStatus.Active or MembershipStatus.Invited))
            return Result.Failure(MembershipErrors.InvalidStatusTransition);

        Status = MembershipStatus.Disabled;
        return Result.Success();
    }

    void ITenantEntity.SetTenantId(TenantId tenantId)
    {
        if (TenantId != default && TenantId != tenantId)
            throw new InvalidOperationException("TenantId de uma membership já nascida não é alterável.");

        TenantId = tenantId;
    }
}
