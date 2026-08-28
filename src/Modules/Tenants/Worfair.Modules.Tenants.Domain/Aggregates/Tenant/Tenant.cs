namespace Worfair.Modules.Tenants.Domain.Aggregates.Tenant;

using System.Text.RegularExpressions;
using Worfair.BuildingBlocks.Domain.Auditing;
using Worfair.BuildingBlocks.Domain.Entities;
using Worfair.BuildingBlocks.Domain.Errors;
using Worfair.BuildingBlocks.Domain.ValueObjects;
using Worfair.Modules.Tenants.Domain.Events;

/// <summary>
/// Raiz de agregado GLOBAL (sem tenant_id — docs/database/02 R-03):
/// espaço contratual na plataforma; raiz do isolamento.
/// </summary>
public sealed class Tenant : AggregateRoot<Guid>, IAuditableEntity
{
    public const int NameMaxLength = 150;
    public const int SlugMaxLength = 80;
    public const int TimezoneMaxLength = 50;
    public const int LocaleMaxLength = 10;
    private static readonly Regex SlugPattern = new(
        @"^[a-z0-9]+(-[a-z0-9]+)*$",
        RegexOptions.Compiled,
        matchTimeout: TimeSpan.FromMilliseconds(250));

    private Tenant()
    {
        // EF Core
    }

    private Tenant(
        Guid id, string name, string slug, TenantTier tier, string? timezone, string locale, DateTime utcNow)
    {
        Id = id;
        Name = name;
        Slug = slug;
        Tier = tier;
        Status = TenantStatus.Active;
        Timezone = timezone;
        Locale = locale;
        CreatedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    public string Name { get; private set; } = default!;

    public string Slug { get; private set; } = default!;

    public TenantTier Tier { get; private set; }

    public TenantStatus Status { get; private set; }

    public string? Timezone { get; private set; }

    public string Locale { get; private set; } = "pt-BR";

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? UpdatedAtUtc { get; private set; }

    /// <summary>Cria o tenant em status Active e emite TenantProvisionedDomainEvent.</summary>
    public static Result<Tenant> Create(
        string? name, string? slug, TenantTier tier = TenantTier.Standard,
        string? timezone = null, string? locale = null, DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;

        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<Tenant>(TenantErrors.NameRequired);
        if (name.Trim().Length > NameMaxLength)
            return Result.Failure<Tenant>(TenantErrors.NameTooLong(NameMaxLength));

        var normalizedSlug = slug?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedSlug)
            || normalizedSlug.Length > SlugMaxLength
            || !SlugPattern.IsMatch(normalizedSlug))
            return Result.Failure<Tenant>(TenantErrors.SlugInvalid);

        if (!Enum.IsDefined(tier))
            return Result.Failure<Tenant>(TenantErrors.InvalidStatusTransition);

        var tenant = new Tenant(
            Guid.NewGuid(),
            name.Trim(),
            normalizedSlug,
            tier,
            string.IsNullOrWhiteSpace(timezone) ? null : timezone.Trim(),
            string.IsNullOrWhiteSpace(locale) ? "pt-BR" : locale!.Trim(),
            now);

        tenant.RaiseDomainEvent(new TenantProvisionedDomainEvent(tenant.Key, tenant.Name, tenant.Slug));

        return tenant;
    }

    /// <summary>Identificador tipado (raiz de isolamento — usado por todos os módulos).</summary>
    public TenantId Key => new(Id);

    public Result Suspend()
    {
        if (Status != TenantStatus.Active)
            return Result.Failure(TenantErrors.InvalidStatusTransition);

        Status = TenantStatus.Suspended;
        return Result.Success();
    }

    public Result Reactivate()
    {
        if (Status != TenantStatus.Suspended)
            return Result.Failure(TenantErrors.InvalidStatusTransition);

        Status = TenantStatus.Active;
        return Result.Success();
    }

    public Result Cancel()
    {
        if (Status is TenantStatus.Cancelled)
            return Result.Failure(TenantErrors.InvalidStatusTransition);

        Status = TenantStatus.Cancelled;
        return Result.Success();
    }
}
