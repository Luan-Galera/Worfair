namespace Worfair.Modules.Identity.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Worfair.BuildingBlocks.Domain.Tenancy;
using Worfair.BuildingBlocks.Infrastructure.Persistence.Outbox;
using Worfair.Modules.Identity.Domain.Aggregates.RefreshToken;
using Worfair.Modules.Identity.Domain.Aggregates.Role;
using Worfair.Modules.Identity.Domain.Aggregates.User;
using Worfair.Modules.Identity.Domain.Aggregates.UserRole;

/// <summary>
/// DbContext do módulo Identity — schema "identity" (R-01/R-09).
/// Globais (R-03): users, roles, permissions, role_permissions.
/// Escopo misto (RLS especial): user_roles, refresh_tokens, audit.audit_logs.
/// </summary>
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options, ITenantProvider tenantProvider)
    : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<Worfair.Modules.Identity.Domain.Aggregates.Role.Permission> Permissions =>
        Set<Worfair.Modules.Identity.Domain.Aggregates.Role.Permission>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
        modelBuilder.AddOutboxMessages("identity");

        // Nenhum ITenantEntity neste módulo: isolamento por RLS nas tabelas de
        // escopo misto + leitura explícita por tenant nos repositórios.
        _ = tenantProvider;
    }
}
