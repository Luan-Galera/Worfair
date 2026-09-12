namespace Worfair.BuildingBlocks.Infrastructure.Persistence.Tenant;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Worfair.BuildingBlocks.Domain.Tenancy;

/// <summary>
/// Proteção de ESCRITA (docs/architecture/04 §3.4): preenche tenant em Added,
/// bloqueia Modified/Deleted cross-tenant.
/// </summary>
public sealed class TenantSaveChangesInterceptor(ITenantProvider tenantProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        ApplyTenantRules(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyTenantRules(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyTenantRules(DbContext? context)
    {
        if (context is null)
            return;

        var tenantId = tenantProvider.TenantId;

        foreach (var entry in context.ChangeTracker.Entries<Worfair.BuildingBlocks.Domain.Tenancy.ITenantEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    if (tenantId is not null)
                    {
                        if (entry.Entity.TenantId == default || entry.Entity.TenantId != tenantId.Value)
                            entry.Entity.SetTenantId(tenantId.Value);
                        break;
                    }

                    // Bootstrap global: uma operação de provisionamento pode criar entidades do
                    // novo tenant explicitamente sem estar em contexto de tenant vigente.
                    if (entry.Entity.TenantId == default)
                        throw new TenantRequiredException("Entidade tenant-owned exige um tenant ativo no contexto.");
                    break;

                case EntityState.Modified:
                    var modifiedOriginal =
                        entry.OriginalValues.GetValue<Worfair.BuildingBlocks.Domain.ValueObjects.TenantId>(nameof(Worfair.BuildingBlocks.Domain.Tenancy.ITenantEntity.TenantId));
                    if (tenantId is null || modifiedOriginal != tenantId.Value || entry.Entity.TenantId != tenantId.Value)
                        throw new TenantMismatchException("Tentativa de alterar (ou mover) dados de outro tenant.");
                    break;

                case EntityState.Deleted:
                    var deletedOriginal =
                        entry.OriginalValues.GetValue<Worfair.BuildingBlocks.Domain.ValueObjects.TenantId>(nameof(Worfair.BuildingBlocks.Domain.Tenancy.ITenantEntity.TenantId));
                    if (tenantId is null || deletedOriginal != tenantId.Value)
                        throw new TenantMismatchException("Tentativa de excluir dados de outro tenant.");
                    break;
            }
        }
    }
}
