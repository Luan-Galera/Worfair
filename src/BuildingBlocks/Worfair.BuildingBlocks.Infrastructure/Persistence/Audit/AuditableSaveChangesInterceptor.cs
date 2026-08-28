namespace Worfair.BuildingBlocks.Infrastructure.Persistence.Audit;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Worfair.BuildingBlocks.Domain.Auditing;
using Worfair.BuildingBlocks.Application.Ports;

/// <summary>Carimba created_at/updated_at em UTC (R-09).</summary>
public sealed class AuditableSaveChangesInterceptor(IDateTimeProvider clock) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
            return;

        foreach (var entry in context.ChangeTracker.Entries<IAuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    if (entry.Entity.CreatedAtUtc == default)
                        entry.Property(nameof(IAuditableEntity.CreatedAtUtc)).CurrentValue = clock.UtcNow;
                    entry.Property(nameof(IAuditableEntity.UpdatedAtUtc)).CurrentValue = clock.UtcNow;
                    break;

                case EntityState.Modified:
                    entry.Property(nameof(IAuditableEntity.UpdatedAtUtc)).CurrentValue = clock.UtcNow;
                    // created_at nunca muda
                    entry.Property(nameof(IAuditableEntity.CreatedAtUtc)).IsModified = false;
                    break;
            }
        }
    }
}
