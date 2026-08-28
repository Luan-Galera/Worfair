namespace Worfair.BuildingBlocks.Domain.Entities;

/// <summary>
/// Base de toda entidade persistida: igualdade por identidade (Id).
/// </summary>
public abstract class Entity
{
    public override bool Equals(object? obj) =>
        obj is Entity other && GetType() == other.GetType() && IdEquals(other);

    public override int GetHashCode() => GetIdHashCode();

    protected abstract bool IdEquals(Entity other);
    protected abstract int GetIdHashCode();
}

/// <summary>
/// Entidade com identificador tipado (docs/architecture/05 §1).
/// </summary>
public abstract class Entity<TId> : Entity
    where TId : notnull
{
    public TId Id { get; protected set; } = default!;

    protected override bool IdEquals(Entity other) =>
        other is Entity<TId> typed && EqualityComparer<TId>.Default.Equals(Id, typed.Id);

    protected override int GetIdHashCode() =>
        typeof(TId).GetHashCode() ^ EqualityComparer<TId>.Default.GetHashCode(Id);
}
