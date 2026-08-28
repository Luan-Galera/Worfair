namespace Worfair.Modules.Tenants.Domain.Aggregates.Company;

using Worfair.BuildingBlocks.Domain.Auditing;
using Worfair.BuildingBlocks.Domain.Errors;
using Worfair.BuildingBlocks.Domain.Tenancy;
using Worfair.BuildingBlocks.Domain.ValueObjects;
using Worfair.Modules.Tenants.Domain.Errors;
using Worfair.Modules.Tenants.Domain.ValueObjects;

/// <summary>
/// Pessoa jurídica operacional dentro do tenant (tenant-owned, DB-05).
/// Um tenant pode ter 1:N empresas; registros de negócio referenciam company_id
/// opcionalmente (pessoa física sem empresa).
/// </summary>
public sealed class Company : TenantEntity<CompanyId>, IAuditableEntity
{
    public const int LegalNameMaxLength = 200;
    public const int TradeNameMaxLength = 200;
    public const int EmailMaxLength = 320;
    public const int PhoneMaxLength = 30;

    private Company()
    {
        // EF Core
    }

    private Company(
        CompanyId id, TenantId tenantId, string legalName, string? tradeName,
        Document document, string? email, string? phone, DateTime utcNow)
    {
        Id = id;
        TenantId = tenantId;
        LegalName = legalName;
        TradeName = tradeName;
        Document = document;
        Email = email;
        Phone = phone;
        Status = CompanyStatus.Active;
        CreatedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    public string LegalName { get; private set; } = default!;

    public string? TradeName { get; private set; }

    public Document Document { get; private set; } = default!;

    public string? Email { get; private set; }

    public string? Phone { get; private set; }

    public CompanyStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? UpdatedAtUtc { get; private set; }

    /// <summary>O tenant é informado pelo interceptor em SaveChanges (nunca pelo input).</summary>
    public static Result<Company> Create(
        string? legalName, string? tradeName, Document document,
        string? email = null, string? phone = null, DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;

        if (string.IsNullOrWhiteSpace(legalName))
            return Result.Failure<Company>(CompanyErrors.LegalNameRequired);
        if (legalName.Trim().Length > LegalNameMaxLength)
            return Result.Failure<Company>(CompanyErrors.FieldTooLong("Razão social", LegalNameMaxLength));
        if (tradeName is { Length: > TradeNameMaxLength })
            return Result.Failure<Company>(CompanyErrors.FieldTooLong("Nome fantasia", TradeNameMaxLength));
        if (!string.IsNullOrWhiteSpace(email) && email.Trim().Length > EmailMaxLength)
            return Result.Failure<Company>(CompanyErrors.FieldTooLong("E-mail", EmailMaxLength));
        if (!string.IsNullOrWhiteSpace(phone) && phone.Trim().Length > PhoneMaxLength)
            return Result.Failure<Company>(CompanyErrors.FieldTooLong("Telefone", PhoneMaxLength));

        return new Company(
            new CompanyId(Guid.NewGuid()),
            TenantId.Empty,
            legalName.Trim(),
            string.IsNullOrWhiteSpace(tradeName) ? null : tradeName.Trim(),
            document,
            string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
            string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
            now);
    }

    public Result Deactivate()
    {
        if (Status != CompanyStatus.Active)
            return Result.Failure(CompanyErrors.InvalidStatusTransition);

        Status = CompanyStatus.Inactive;
        return Result.Success();
    }

    public Result Activate()
    {
        if (Status != CompanyStatus.Inactive)
            return Result.Failure(CompanyErrors.InvalidStatusTransition);

        Status = CompanyStatus.Active;
        return Result.Success();
    }
}
