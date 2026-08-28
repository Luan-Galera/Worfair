namespace Worfair.Modules.Identity.Domain.Aggregates.Role;

using Worfair.BuildingBlocks.Domain.Entities;
using Worfair.BuildingBlocks.Domain.Errors;
using Worfair.Modules.Identity.Domain.Errors;

/// <summary>Permissão GLOBAL (catálogo — R-03). Código estável ex.: recruitment.requisition.publish.</summary>
public sealed class Permission : Entity<Guid>
{
    public const int CodeMaxLength = 100;

    private Permission()
    {
        // EF Core
    }

    private Permission(Guid id, string code)
    {
        Id = id;
        Code = code;
    }

    public string Code { get; private set; } = default!;

    public static Result<Permission> Create(string? code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length > CodeMaxLength)
            return Result.Failure<Permission>(AuthErrors.RoleNotFound);

        return new Permission(Guid.NewGuid(), code.Trim().ToLowerInvariant());
    }

    public PermissionId Key => new(Id);
}
