namespace Worfair.BuildingBlocks.Domain.Tenancy;

/// <summary>
/// Marca entidades tenant-owned: exigem coluna tenant_id + RLS (docs/database/02 R-01/R-02).
/// Tabelas globais (tenants, users, roles, permissions, role_permissions) NÃO implementam.
/// </summary>
public interface ITenantEntity
{
    TenantId TenantId { get; }

    /// <summary>Preenchido exclusivamente pelo TenantSaveChangesInterceptor — nunca por handlers.</summary>
    void SetTenantId(TenantId tenantId);
}

/// <summary>
/// Base para agregados/entidades tenant-owned com Id tipado.
/// </summary>
public abstract class TenantEntity<TId> : Entity<TId>, ITenantEntity
    where TId : notnull
{
    public TenantId TenantId { get; protected set; }

    protected TenantEntity()
        => TenantId = default;

    public void SetTenantId(TenantId tenantId)
    {
        if (TenantId != default && TenantId != tenantId)
            throw new InvalidOperationException("TenantId de uma entidade já nascida não é alterável.");

        TenantId = tenantId;
    }
}
