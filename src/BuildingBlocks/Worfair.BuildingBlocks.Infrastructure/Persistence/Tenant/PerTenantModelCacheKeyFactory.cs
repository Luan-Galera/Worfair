namespace Worfair.BuildingBlocks.Infrastructure.Persistence.Tenant;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Worfair.BuildingBlocks.Domain.ValueObjects;

/// <summary>
/// Cacheia um modelo EF POR TENANT (não por sessão) quando o contexto aplica
/// filtros dinâmicos (docs/architecture/04 §3.3/§5).
/// </summary>
public sealed class PerTenantModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime)
        => context is ITenantFilteredDbContext { CurrentTenantId: { } tenantId }
            ? (context.GetType(), tenantId, designTime)
            : (context.GetType(), designTime);
}
