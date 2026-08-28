namespace Worfair.BuildingBlocks.Domain.Tenancy;

/// <summary>
/// Resolve o tenant corrente. Alimentado UMA única vez pelo claim do JWT validado
/// (SEC-02) — nunca por header/body/query (docs/security/02).
/// </summary>
public interface ITenantProvider
{
    /// <summary>Null = contexto global (SUPER_ADMIN) ou request anônimo.</summary>
    TenantId? TenantId { get; }

    bool IsMultitenantRequest => TenantId is not null;

    void SetTenant(TenantId? tenantId);
}
