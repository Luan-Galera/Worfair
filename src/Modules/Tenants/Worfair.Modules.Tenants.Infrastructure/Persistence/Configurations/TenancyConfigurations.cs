namespace Worfair.Modules.Tenants.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Worfair.Modules.Tenants.Domain.Aggregates.Company;
using Worfair.Modules.Tenants.Domain.Aggregates.Membership;
using Worfair.Modules.Tenants.Domain.Aggregates.Settings;
using Worfair.Modules.Tenants.Domain.Aggregates.Tenant;

/// <summary>
/// Mapeamento snake_case explícito (R-09). CHECKs/RLS/índices funcionais ficam
/// nas migrations (migrationBuilder.Sql — docs/database/04).
/// </summary>
public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants", "tenancy");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");

        builder.Property(t => t.Name)
            .HasColumnName("name")
            .HasMaxLength(Tenant.NameMaxLength)
            .IsRequired();

        builder.Property(t => t.Slug)
            .HasColumnName("slug")
            .HasMaxLength(Tenant.SlugMaxLength)
            .IsRequired();

        builder.HasIndex(t => t.Slug).IsUnique().HasDatabaseName("uq_tenants_slug");

        builder.Property(t => t.Tier)
            .HasColumnName("tier")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(t => t.Status)
            .HasColumnName("status")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(t => t.Timezone)
            .HasColumnName("timezone")
            .HasMaxLength(Tenant.TimezoneMaxLength);

        builder.Property(t => t.Locale)
            .HasColumnName("locale")
            .HasMaxLength(Tenant.LocaleMaxLength)
            .IsRequired();

        builder.Property(t => t.CreatedAtUtc).HasColumnName("created_at");
        builder.Property(t => t.UpdatedAtUtc).HasColumnName("updated_at");

        builder.Ignore(t => t.DomainEvents);
    }
}

public sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("companies", "tenancy");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new CompanyId(value));

        builder.Property(c => c.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();

        builder.Property(c => c.LegalName)
            .HasColumnName("legal_name")
            .HasMaxLength(Company.LegalNameMaxLength)
            .IsRequired();

        builder.Property(c => c.TradeName)
            .HasColumnName("trade_name")
            .HasMaxLength(Company.TradeNameMaxLength);

        builder.Property(c => c.Document)
            .HasColumnName("document")
            .HasMaxLength(Document.MaxLength)
            .HasConversion(d => d.Value, value => Document.Create(value).Value)
            .IsRequired();

        // R-01: UNIQUE (tenant_id, document) + índice composto iniciando por tenant_id
        builder.HasIndex(c => new { c.TenantId, c.Document })
            .IsUnique()
            .HasDatabaseName("uq_companies_tenant_document");

        builder.Property(c => c.Email)
            .HasColumnName("email")
            .HasMaxLength(Company.EmailMaxLength);

        builder.Property(c => c.Phone)
            .HasColumnName("phone")
            .HasMaxLength(Company.PhoneMaxLength);

        builder.Property(c => c.Status)
            .HasColumnName("status")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(c => c.CreatedAtUtc).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAtUtc).HasColumnName("updated_at");
    }
}

public sealed class TenantMembershipConfiguration : IEntityTypeConfiguration<TenantMembership>
{
    public void Configure(EntityTypeBuilder<TenantMembership> builder)
    {
        builder.ToTable("tenant_memberships", "tenancy");

        builder.HasKey(m => new { m.TenantId, m.UserId });

        builder.Property(m => m.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();

        builder.Property(m => m.UserId).HasColumnName("user_id").IsRequired();

        builder.Property(m => m.Status)
            .HasColumnName("status")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(m => m.JoinedAtUtc).HasColumnName("joined_at");

        builder.HasIndex(m => m.UserId).HasDatabaseName("ix_tenant_memberships_user");
    }
}

public sealed class TenantSettingsConfiguration : IEntityTypeConfiguration<TenantSettings>
{
    public void Configure(EntityTypeBuilder<TenantSettings> builder)
    {
        builder.ToTable("tenant_settings", "tenancy");

        builder.HasKey(s => s.TenantId);

        builder.Property(s => s.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();

        builder.Property(s => s.HiringWorkflow).HasColumnName("hiring_workflow").HasColumnType("jsonb");
        builder.Property(s => s.Branding).HasColumnName("branding").HasColumnType("jsonb");
        builder.Property(s => s.FeatureFlags).HasColumnName("feature_flags").HasColumnType("jsonb");
    }
}
