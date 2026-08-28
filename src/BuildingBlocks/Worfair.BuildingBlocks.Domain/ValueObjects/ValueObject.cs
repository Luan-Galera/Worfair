namespace Worfair.BuildingBlocks.Domain.ValueObjects;

/// <summary>
/// Base de Value Object com igualdade estrutural (docs/architecture/05 §1).
/// </summary>
public abstract class ValueObject
{
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public override bool Equals(object? obj)
    {
        if (obj is null || obj.GetType() != GetType())
            return false;

        var left = GetEqualityComponents().GetEnumerator();
        var right = ((ValueObject)obj).GetEqualityComponents().GetEnumerator();

        while (left.MoveNext() && right.MoveNext())
        {
            if (!Equals(left.Current, right.Current))
                return false;
        }

        return !left.MoveNext() && !right.MoveNext();
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var component in GetEqualityComponents())
            hash.Add(component);
        return hash.ToHashCode();
    }

    public static bool operator ==(ValueObject? left, ValueObject? right) => Equals(left, right);

    public static bool operator !=(ValueObject? left, ValueObject? right) => !Equals(left, right);
}
