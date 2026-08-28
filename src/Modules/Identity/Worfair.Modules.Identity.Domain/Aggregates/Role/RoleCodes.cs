namespace Worfair.Modules.Identity.Domain.Aggregates.Role;

/// <summary>Catálogo oficial v1.2 (DB-07) — seed idempotente na migration.</summary>
public static class RoleCodes
{
    public const string SuperAdmin = "SUPER_ADMIN";
    public const string Owner = "OWNER";
    public const string Client = "CLIENT";
    public const string Recruiter = "RECRUITER";
    public const string HiringManager = "HIRING_MANAGER";
    public const string Provider = "PROVIDER";

    public static readonly IReadOnlyList<string> All =
        [SuperAdmin, Owner, Client, Recruiter, HiringManager, Provider];
}
