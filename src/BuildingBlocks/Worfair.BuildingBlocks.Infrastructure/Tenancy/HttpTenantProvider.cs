namespace Worfair.BuildingBlocks.Infrastructure.Tenancy;

using Worfair.BuildingBlocks.Domain.Tenancy;
using Worfair.BuildingBlocks.Domain.ValueObjects;

/// <summary>
/// Implementação scoped: alimentada UMA vez pelo TenantContextMiddleware a
/// partir do claim do JWT validado (SEC-02). Nunca por header/body/query.
/// </summary>
public sealed class HttpTenantProvider : ITenantProvider
{
    public TenantId? TenantId { get; private set; }

    public void SetTenant(TenantId? tenantId) => TenantId = tenantId;
}
