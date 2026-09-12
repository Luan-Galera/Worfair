namespace Worfair.Modules.Jobs.Application.Dtos;

public sealed record JobPostingDto(
    Guid Id,
    Guid? CompanyId,
    Guid? CreatedBy,
    string Title,
    string Description,
    string? Location,
    int Remote,
    int Status,
    DateTime? PublishedAtUtc,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    string? Category = null,
    string? CompanyName = null);

public sealed record ServiceProjectDto(
    Guid Id,
    Guid? CompanyId,
    Guid? CreatedBy,
    string Title,
    string Description,
    decimal BudgetMin,
    decimal? BudgetMax,
    string Currency,
    DateTime? Deadline,
    int Status,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    string? Category = null,
    string? CompanyName = null);

public sealed record JobApplicationDto(
    Guid Id,
    Guid JobPostingId,
    Guid ApplicantUserId,
    Guid? ApplicantCompanyId,
    string Message,
    int Status,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
