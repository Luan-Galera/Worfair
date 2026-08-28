namespace Worfair.Modules.Identity.Application.Dtos;

public sealed record AuthResponseDto(
    Guid UserId,
    string Email,
    string FullName,
    Guid? TenantId,
    string Mode,
    IReadOnlyList<string> Roles,
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc);

public sealed record MembershipDto(Guid TenantId, string TenantName, int Status, DateTime JoinedAtUtc);

public sealed record MeDto(
    Guid UserId,
    string Email,
    string FullName,
    int Status,
    Guid? TenantId,
    string? Mode,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> EffectivePermissions,
    IReadOnlyList<string> AvailableModes,
    IReadOnlyList<MembershipDto> Memberships);
