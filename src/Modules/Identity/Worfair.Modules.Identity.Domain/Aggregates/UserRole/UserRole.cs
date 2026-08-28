namespace Worfair.Modules.Identity.Domain.Aggregates.UserRole;

using Worfair.BuildingBlocks.Domain.Errors;
using Worfair.BuildingBlocks.Domain.ValueObjects;
using Worfair.Modules.Identity.Domain.Aggregates.Role;
using Worfair.Modules.Identity.Domain.Errors;

/// <summary>
/// Vínculo usuário × role com escopo MISTO (R-04/R-05, docs/database/03 §4):
/// tenant_id NULL ⇒ role GLOBAL (SUPER_ADMIN); caso contrário role de tenant
/// (exige membership ativa). PK composta (user_id, tenant_id, role_id).
/// </summary>
public sealed class UserRole
{
    private UserRole()
    {
        // EF Core
    }

    private UserRole(Guid userId, TenantId? tenantId, Guid roleId, bool isGlobal, Guid? grantedBy, DateTime grantedAtUtc)
    {
        // PK técnica: o PostgreSQL não admite NULL em PK composta; a chave lógica
        // (user_id, tenant_id, role_id) é garantida por UNIQUE (+ parcial p/ globais).
        Id = Guid.NewGuid();
        UserId = userId;
        TenantId = tenantId;
        RoleIdValue = roleId;
        IsGlobal = isGlobal;
        GrantedBy = grantedBy;
        GrantedAtUtc = grantedAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    /// <summary>NULL apenas para roles globais (SUPER_ADMIN).</summary>
    public TenantId? TenantId { get; private set; }

    public Guid RoleIdValue { get; private set; }

    /// <summary>Espelho da role — CHECK do banco: (tenant_id IS NULL) = is_global.</summary>
    public bool IsGlobal { get; private set; }

    public Guid? GrantedBy { get; private set; }

    public DateTime GrantedAtUtc { get; private set; }

    /// <summary>
    /// Fábrica que garante a coerência de escopo ANTES do banco (R-04):
    /// role global ⇔ sem tenant; role de tenant ⇔ com tenant.
    /// </summary>
    public static Result<UserRole> Grant(
        Guid userId, Role role, TenantId? tenantId, Guid? grantedBy, DateTime? utcNow = null)
    {
        if ((tenantId is null) != role.IsGlobal)
            return Result.Failure<UserRole>(AuthErrors.GlobalRoleRequiresPlatformScope);

        return new UserRole(
            userId,
            tenantId,
            role.Id,
            role.IsGlobal,
            grantedBy,
            utcNow ?? DateTime.UtcNow);
    }

    /// <summary>Chave lógica para deduplicação (user, escopo, role).</summary>
    public bool Matches(Guid userId, TenantId? tenantId, Guid roleId) =>
        UserId == userId
        && RoleIdValue == roleId
        && (tenantId is null
            ? TenantId is null
            : TenantId == tenantId.Value);
}
