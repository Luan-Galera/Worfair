namespace Worfair.Modules.Tenants.Application.Dtos;

public sealed record TenantDto(
    Guid Id,
    string Name,
    string Slug,
    int Tier,
    int Status,
    string? Timezone,
    string Locale,
    DateTime CreatedAtUtc);

public sealed record CompanyDto(
    Guid Id,
    string LegalName,
    string? TradeName,
    string Document,
    string? Email,
    string? Phone,
    int Status,
    Guid? OwnerUserId = null);

public sealed record MemberDto(
    Guid UserId,
    int Status,
    DateTime JoinedAtUtc,
    string? Email = null,
    string? FullName = null,
    IReadOnlyList<string>? RoleCodes = null);
