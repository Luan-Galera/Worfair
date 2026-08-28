namespace Worfair.Modules.Recruitment.Domain.Aggregates.Interview;

using Worfair.BuildingBlocks.Domain.Auditing;
using Worfair.BuildingBlocks.Domain.Errors;
using Worfair.BuildingBlocks.Domain.Tenancy;
using Worfair.Modules.Recruitment.Domain.Abstractions;
using Worfair.Modules.Recruitment.Domain.Errors;

/// <summary>
/// Entrevista vinculada a requisição + candidato (tenant-owned).
/// Invariantes (docs/architecture/03 §3.2): duração válida; agendamento no
/// futuro; feedback obrigatório para concluir; um feedback por entrevistador.
/// </summary>
public sealed class Interview : TenantAggregateRoot<InterviewId>, IAuditableEntity
{
    public const int MinDurationMinutes = 15;
    public const int MaxDurationMinutes = 480;

    private readonly List<InterviewFeedback> _feedbacks = [];

    private Interview()
    {
        // EF Core
    }

    private Interview(
        InterviewId id,
        Guid jobRequisitionId,
        Guid candidateId,
        DateTime scheduledAtUtc,
        int durationMinutes,
        InterviewType type,
        DateTime utcNow)
    {
        Id = id;
        JobRequisitionId = jobRequisitionId;
        CandidateId = candidateId;
        ScheduledAtUtc = scheduledAtUtc;
        DurationMinutes = durationMinutes;
        Type = type;
        Status = InterviewStatus.Scheduled;
        CreatedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    public Guid JobRequisitionId { get; private set; }

    public Guid CandidateId { get; private set; }

    public DateTime ScheduledAtUtc { get; private set; }

    public int DurationMinutes { get; private set; }

    public InterviewType Type { get; private set; }

    public InterviewStatus Status { get; private set; }

    public DateTime? CompletedAtUtc { get; private set; }

    public DateTime? CancelledAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? UpdatedAtUtc { get; private set; }

    public IReadOnlyList<InterviewFeedback> Feedbacks => _feedbacks.AsReadOnly();

    private void Touch(DateTime utcNow) => UpdatedAtUtc = utcNow;

    /// <summary>Nasce em Scheduled.</summary>
    public static Result<Interview> Schedule(
        Guid jobRequisitionId, Guid candidateId, DateTime scheduledAtUtc,
        int durationMinutes, InterviewType type, DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;

        if (jobRequisitionId == Guid.Empty)
            return Result.Failure<Interview>(InterviewErrors.RequisitionRequired);

        if (candidateId == Guid.Empty)
            return Result.Failure<Interview>(InterviewErrors.CandidateRequired);

        if (durationMinutes is < MinDurationMinutes or > MaxDurationMinutes)
            return Result.Failure<Interview>(InterviewErrors.InvalidDuration);

        if (scheduledAtUtc <= now)
            return Result.Failure<Interview>(InterviewErrors.ScheduledInPast);

        return new Interview(
            new InterviewId(Guid.NewGuid()),
            jobRequisitionId,
            candidateId,
            scheduledAtUtc,
            durationMinutes,
            type,
            now);
    }

    /// <summary>
    /// Registra o feedback do entrevistador. A validação "entrevistador pertence
    /// ao HiringTeam" é feita pelo handler (consulta cross-aggregate).
    /// </summary>
    public Result AddFeedback(Guid interviewerUserId, int rating, string? notes, DateTime? utcNow = null)
    {
        if (Status != InterviewStatus.Scheduled)
            return Result.Failure(InterviewErrors.InvalidStatusTransition);

        if (_feedbacks.Any(f => f.InterviewerUserId == interviewerUserId))
            return Result.Failure(InterviewErrors.FeedbackAlreadySubmitted);

        var createResult = InterviewFeedback.Create(interviewerUserId, rating, notes, utcNow);
        if (createResult.IsFailure)
            return Result.Failure(createResult.Error!);

        _feedbacks.Add(createResult.Value);
        Touch(utcNow ?? DateTime.UtcNow);
        return Result.Success();
    }

    public Result Complete(DateTime? utcNow = null)
    {
        if (Status != InterviewStatus.Scheduled)
            return Result.Failure(InterviewErrors.InvalidStatusTransition);

        if (_feedbacks.Count == 0)
            return Result.Failure(InterviewErrors.FeedbackRequiredToComplete);

        var now = utcNow ?? DateTime.UtcNow;
        Status = InterviewStatus.Completed;
        CompletedAtUtc = now;
        Touch(now);
        return Result.Success();
    }

    public Result Cancel(DateTime? utcNow = null)
    {
        if (Status != InterviewStatus.Scheduled)
            return Result.Failure(InterviewErrors.InvalidStatusTransition);

        var now = utcNow ?? DateTime.UtcNow;
        Status = InterviewStatus.Cancelled;
        CancelledAtUtc = now;
        Touch(now);
        return Result.Success();
    }
}
