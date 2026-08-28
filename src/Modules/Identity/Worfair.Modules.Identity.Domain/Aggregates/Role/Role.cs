namespace Worfair.Modules.Identity.Domain.Aggregates.Role;

using Worfair.BuildingBlocks.Domain.Entities;
using Worfair.BuildingBlocks.Domain.Errors;
using Worfair.Modules.Identity.Domain.Errors;
using Worfair.Modules.Identity.Domain.ValueObjects;

/// <summary>Role GLOBAL (catálogo — R-03). is_global=true apenas para SUPER_ADMIN.</summary>
public sealed class Role : Entity<Guid>
{
    public const int CodeMaxLength = 50;
    public const int NameMaxLength = 120;

    private Role()
    {
        // EF Core
    }

    private Role(Guid id, string code, string name, bool isGlobal, string? description)
    {
        Id = id;
        Code = code;
        Name = name;
        IsGlobal = isGlobal;
        Description = description;
    }

    public string Code { get; private set; } = default!;

    public string Name { get; private set; } = default!;

    public bool IsGlobal { get; private set; }

    public string? Description { get; private set; }

    public static Result<Role> Create(string? code, string? name, bool isGlobal, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > CodeMaxLength)
            return Result.Failure<Role>(AuthErrors.RoleNotFound);
        if (string.IsNullOrWhiteSpace(name) || name.Length > NameMaxLength)
            return Result.Failure<Role>(AuthErrors.FullNameRequired);

        // Invariante do catálogo: somente SUPER_ADMIN é global.
        if (isGlobal && !code.Equals(RoleCodes.SuperAdmin, StringComparison.Ordinal))
            return Result.Failure<Role>(AuthErrors.GlobalRoleRequiresPlatformScope);

        return new Role(Guid.NewGuid(), code.Trim().ToUpperInvariant(), name.Trim(), isGlobal, description);
    }

    public RoleId Key => new(Id);

    public RolePermission Grant(PermissionId permissionId) => new(Id, permissionId.Value);
}

/// <summary>Vínculo role ↔ permissão (PK composta, tabela global).</summary>
public sealed class RolePermission
{
    internal RolePermission(Guid roleId, Guid permissionId)
    {
        RoleId = roleId;
        PermissionId = permissionId;
    }

    public Guid RoleId { get; private set; }

    public Guid PermissionId { get; private set; }
}
