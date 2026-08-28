namespace Worfair.Modules.Recruitment.Domain.Aggregates.Interview;

/// <summary>Status da entrevista (docs/database/03 §5).</summary>
public enum InterviewStatus
{
    Scheduled = 1,
    Completed = 2,
    Cancelled = 3
}
