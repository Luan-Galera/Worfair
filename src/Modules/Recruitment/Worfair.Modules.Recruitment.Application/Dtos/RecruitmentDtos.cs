namespace Worfair.Modules.Recruitment.Application.Dtos;

public sealed record HiringTeamMemberDto(Guid RecruiterUserId, int Role, DateTime AddedAtUtc);

public sealed record StageHistoryDto(int FromStatus, int ToStatus, Guid ChangedBy, DateTime ChangedAtUtc);

public sealed record InterviewFeedbackDto(Guid InterviewerUserId, int Rating, string Notes, DateTime SubmittedAtUtc);

public sealed record JobRequisitionDto(
    Guid Id,
    Guid? CompanyId,
    string Title,
    string Description,
    decimal SalaryMin,
    decimal? SalaryMax,
    string SalaryCurrency,
    int Status,
    Guid? CreatedBy,
    DateTime CreatedAtUtc,
    DateTime? PublishedAtUtc,
    DateTime? ClosedAtUtc,
    string? CloseReason,
    IReadOnlyList<HiringTeamMemberDto> HiringTeam);

public sealed record CandidateDto(
    Guid Id,
    Guid? UserId,
    string FullName,
    string Email,
    string? Phone,
    int Source,
    int Status,
    string? ResumeUrl,
    DateTime CreatedAtUtc,
    IReadOnlyList<StageHistoryDto> History);

public sealed record InterviewDto(
    Guid Id,
    Guid JobRequisitionId,
    Guid CandidateId,
    DateTime ScheduledAtUtc,
    int DurationMinutes,
    int Type,
    int Status,
    DateTime? CompletedAtUtc,
    DateTime? CancelledAtUtc,
    IReadOnlyList<InterviewFeedbackDto> Feedbacks);
