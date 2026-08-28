namespace Worfair.BuildingBlocks.Infrastructure.Persistence.Tenant;

using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using Worfair.BuildingBlocks.Domain.Tenancy;
using Worfair.BuildingBlocks.Domain.ValueObjects;

/// <summary>
/// DbContext com filtros dinâmicos por tenant — chave de modelo inclui o tenant
/// (docs/architecture/04 §3.3).
/// </summary>
public interface ITenantFilteredDbContext
{
    TenantId? CurrentTenantId { get; }
}

public static class TenantQueryFilterExtensions
{
    /// <summary>
    /// Aplica Global Query Filters a TODA entidade ITenantEntity, por convenção.
    /// </summary>
    public static void ApplyTenantQueryFilters(this ModelBuilder modelBuilder, TenantId tenantId)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
                continue;

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var property = Expression.Property(parameter, nameof(ITenantEntity.TenantId));
            var constant = Expression.Constant(tenantId, typeof(TenantId));
            var body = Expression.Equal(property, constant);
            var filter = Expression.Lambda(body, parameter);

            entityType.SetQueryFilter(filter);
        }
    }

    /// <summary>Atalho usado no OnModelCreating dos contextos de módulo.</summary>
    public static void ApplyTenantFiltersFromProvider(
        this ModelBuilder modelBuilder, ITenantFilteredDbContext context)
    {
        if (context.CurrentTenantId is { } tenantId)
            modelBuilder.ApplyTenantQueryFilters(tenantId);
    }
}
