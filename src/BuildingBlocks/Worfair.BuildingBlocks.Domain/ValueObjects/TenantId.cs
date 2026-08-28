namespace Worfair.BuildingBlocks.Domain.ValueObjects;

/// <summary>
/// Identificador do tenant — usado por TODOS os módulos (docs/architecture/02 §2).
/// </summary>
public readonly record struct TenantId(Guid Value) : IComparable<TenantId>
{
    public static TenantId New() => new(Guid.NewGuid());

    public static readonly TenantId Empty = new(Guid.Empty);

    public bool IsEmpty => Value == Guid.Empty;

    public int CompareTo(TenantId other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString();

    public static implicit operator Guid(TenantId id) => id.Value;
}
