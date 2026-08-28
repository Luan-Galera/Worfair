namespace Worfair.BuildingBlocks.Domain.Auditing;

/// <summary>
/// Entidades com timestamps controlados pela persistência (R-09: created_at/updated_at UTC).
/// </summary>
public interface IAuditableEntity
{
    DateTime CreatedAtUtc { get; }

    DateTime? UpdatedAtUtc { get; }
}

/// <summary>Implementação base para reduzir duplicação entre módulos.</summary>
public abstract class AuditableEntity : Entity<Guid>, IAuditableEntity
{
    public DateTime CreatedAtUtc { get; protected set; }

    public DateTime? UpdatedAtUtc { get; protected set; }

    public void Touch(DateTime utcNow) => UpdatedAtUtc = utcNow;

    protected void StampCreated(DateTime utcNow)
    {
        if (Id == Guid.Empty)
            Id = Guid.NewGuid();
        CreatedAtUtc = utcNow;
    }
}
