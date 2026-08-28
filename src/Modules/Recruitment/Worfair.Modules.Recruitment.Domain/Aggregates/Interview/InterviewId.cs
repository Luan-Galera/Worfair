namespace Worfair.Modules.Recruitment.Domain.Aggregates.Interview;

using Worfair.BuildingBlocks.Domain.Tenancy;
using Worfair.BuildingBlocks.Domain.ValueObjects;

/// <summary>Identificador tipado da entrevista.</summary>
public readonly record struct InterviewId(Guid Value)
{
    public override string ToString() => Value.ToString();
}

/// <summary>
/// Feedback do entrevistador — UNIQUE (interview_id, interviewer_user_id);
/// feedback é obrigatório para concluir (docs/architecture/03 §3.2).
/// </summary>
public sealed class InterviewFeedback : ITenantEntity
{
    public const int NotesMaxLength = 4000;

    private InterviewFeedback()
    {
        // EF Core
    }

    private InterviewFeedback(Guid id, Guid interviewerUserId, int rating, string notes, DateTime submittedAtUtc)
    {
        Id = id;
        InterviewerUserId = interviewerUserId;
        Rating = rating;
        Notes = notes;
        SubmittedAtUtc = submittedAtUtc;
    }

    public Guid Id { get; private set; }

    public TenantId TenantId { get; private set; }

    public Guid InterviewerUserId { get; private set; }

    /// <summary>Nota de 1 a 5 (CHECK no banco — docs/database/03 §5).</summary>
    public int Rating { get; private set; }

    public string Notes { get; private set; } = default!;

    public DateTime SubmittedAtUtc { get; private set; }

    public static Result<InterviewFeedback> Create(
        Guid interviewerUserId, int rating, string? notes, DateTime? utcNow = null)
    {
        if (interviewerUserId == Guid.Empty)
            return Result.Failure<InterviewFeedback>(InterviewErrors.NotFound);

        if (rating is < 1 or > 5)
            return Result.Failure<InterviewFeedback>(InterviewErrors.FeedbackRatingInvalid);

        if (string.IsNullOrWhiteSpace(notes))
            return Result.Failure<InterviewFeedback>(InterviewErrors.FeedbackNotesRequired);

        return new InterviewFeedback(
            Guid.NewGuid(), interviewerUserId, rating, notes.Trim(), utcNow ?? DateTime.UtcNow);
    }

    void ITenantEntity.SetTenantId(TenantId tenantId)
    {
        if (TenantId != default && TenantId != tenantId)
            throw new InvalidOperationException("TenantId de um feedback já nascido não é alterável.");

        TenantId = tenantId;
    }
}
