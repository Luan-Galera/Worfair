namespace Worfair.Modules.Identity.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Worfair.Modules.Identity.Domain.Aggregates.RefreshToken;
using Worfair.Modules.Identity.Domain.Aggregates.Role;
using Worfair.Modules.Identity.Domain.Aggregates.User;
using Worfair.Modules.Identity.Domain.Aggregates.UserRole;

/// <summary>Mapeamento snake_case explícito (R-09). CHECKs/FKs compostas/RLS via migrations.</summary>
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", "identity");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("id");

        builder.Property(u => u.Email)
            .HasColumnName("email")
            .HasMaxLength(Email.MaxLength)
            .HasConversion(e => e.Value, value => Email.Create(value).Value)
            .IsRequired();

        // Índice funcional UNIQUE (lower(email)) via migration SQL (uq_users_email_lower).

        builder.Property(u => u.PasswordHash)
            .HasColumnName("password_hash")
            .IsRequired();

        builder.Property(u => u.FullName)
            .HasColumnName("full_name")
            .HasMaxLength(User.FullNameMaxLength)
            .IsRequired();

        builder.Property(u => u.Status)
            .HasColumnName("status")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(u => u.EmailVerifiedAtUtc).HasColumnName("email_verified_at");
        builder.Property(u => u.LastLoginAtUtc).HasColumnName("last_login_at");
        builder.Property(u => u.CreatedAtUtc).HasColumnName("created_at");
        builder.Property(u => u.UpdatedAtUtc).HasColumnName("updated_at");

        builder.Ignore(u => u.DomainEvents);
    }
}

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles", "identity");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");

        builder.Property(r => r.Code)
            .HasColumnName("code")
            .HasMaxLength(Role.CodeMaxLength)
            .IsRequired();

        builder.HasIndex(r => r.Code).IsUnique().HasDatabaseName("uq_roles_code");
        builder.HasIndex(r => new { r.Id, r.IsGlobal }).IsUnique().HasDatabaseName("uq_roles_id_is_global");

        builder.Property(r => r.Name)
            .HasColumnName("name")
            .HasMaxLength(Role.NameMaxLength)
            .IsRequired();

        builder.Property(r => r.IsGlobal)
            .HasColumnName("is_global")
            .IsRequired();

        builder.Property(r => r.Description).HasColumnName("description").HasColumnType("text");
    }
}

public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("permissions", "identity");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");

        builder.Property(p => p.Code)
            .HasColumnName("code")
            .HasMaxLength(Permission.CodeMaxLength)
            .IsRequired();

        builder.HasIndex(p => p.Code).IsUnique().HasDatabaseName("uq_permissions_code");
    }
}

public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("role_permissions", "identity");

        builder.HasKey(rp => new { rp.RoleId, rp.PermissionId });

        builder.Property(rp => rp.RoleId).HasColumnName("role_id").IsRequired();
        builder.Property(rp => rp.PermissionId).HasColumnName("permission_id").IsRequired();
    }
}

public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("user_roles", "identity");

        // PK técnica (surrogate); chave lógica via índice único abaixo.
        builder.HasKey(ur => ur.Id);
        builder.Property(ur => ur.Id).HasColumnName("id");

        builder.Property(ur => ur.UserId).HasColumnName("user_id").IsRequired();

        builder.Property(ur => ur.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(id => id.HasValue ? id.Value.Value : (Guid?)null,
                           value => value.HasValue ? new TenantId(value.Value) : null)
            .IsRequired(false);

        builder.Property(ur => ur.RoleIdValue).HasColumnName("role_id").IsRequired();

        builder.Property(ur => ur.IsGlobal).HasColumnName("is_global").IsRequired();
        builder.Property(ur => ur.GrantedBy).HasColumnName("granted_by");
        builder.Property(ur => ur.GrantedAtUtc).HasColumnName("granted_at");

        // R-05: múltiplas roles por usuário no mesmo tenant (linhas distintas por role).
        builder.HasIndex(ur => new { ur.UserId, ur.TenantId, ur.RoleIdValue })
            .HasDatabaseName("uq_user_roles_user_tenant_role");
        // Índice parcial p/ linhas GLOBAIS (tenant_id IS NULL) via migration SQL.

        builder.HasIndex(ur => new { ur.TenantId, ur.RoleIdValue }).HasDatabaseName("ix_user_roles_tenant_role");
        builder.HasIndex(ur => ur.UserId).HasDatabaseName("ix_user_roles_user");
    }
}

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens", "identity");

        builder.HasKey(rt => rt.Id);
        builder.Property(rt => rt.Id).HasColumnName("id");

        builder.Property(rt => rt.UserId).HasColumnName("user_id").IsRequired();

        builder.Property(rt => rt.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(id => id.HasValue ? id.Value.Value : (Guid?)null,
                           value => value.HasValue ? new TenantId(value.Value) : null)
            .IsRequired(false);

        builder.Property(rt => rt.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(RefreshToken.TokenHashMaxLength)
            .IsRequired();

        builder.HasIndex(rt => rt.TokenHash).IsUnique().HasDatabaseName("uq_refresh_tokens_token_hash");

        builder.Property(rt => rt.ExpiresAtUtc).HasColumnName("expires_at").IsRequired();
        builder.Property(rt => rt.RevokedAtUtc).HasColumnName("revoked_at");
        builder.Property(rt => rt.ReplacedByTokenHash).HasColumnName("replaced_by_token_hash");
        builder.Property(rt => rt.CreatedAtUtc).HasColumnName("created_at");

        builder.HasIndex(rt => new { rt.TenantId, rt.UserId }).HasDatabaseName("ix_refresh_tokens_tenant_user");
    }
}
