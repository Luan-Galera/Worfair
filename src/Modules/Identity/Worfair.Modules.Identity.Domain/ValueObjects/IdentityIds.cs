namespace Worfair.Modules.Identity.Domain.ValueObjects;

using Worfair.BuildingBlocks.Domain.ValueObjects;

public readonly record struct UserId(Guid Value)
{
    public override string ToString() => Value.ToString();
}

public readonly record struct RoleId(Guid Value)
{
    public override string ToString() => Value.ToString();
}

public readonly record struct PermissionId(Guid Value)
{
    public override string ToString() => Value.ToString();
}

public readonly record struct RefreshTokenId(Guid Value)
{
    public override string ToString() => Value.ToString();
}
