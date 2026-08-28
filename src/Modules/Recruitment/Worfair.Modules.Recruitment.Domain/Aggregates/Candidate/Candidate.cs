namespace Worfair.Modules.Recruitment.Domain.Aggregates.Candidate;

using Worfair.BuildingBlocks.Domain.Auditing;
using Worfair.BuildingBlocks.Domain.Errors;
using Worfair.BuildingBlocks.Domain.Tenancy;
using Worfair.BuildingBlocks.Domain.ValueObjects;
using Worfair.Modules.Recruitment.Domain.Abstractions;
using Worfair.Modules.Recruitment.Domain.Errors;
using Worfair.Modules.Recruitment.Domain.ValueObjects;

/// <summary>
/// Candidato do processo seletivo (tenant-owned). ContactEmail é único POR
/// TENANT (UNIQUE (tenant_id, lower(email)) no banco); transições seguem a
/// máquina de estados; avanço para Interviewing exige entrevista agendada
/// (docs/architecture/03 §3.2).
/// </summary>
public sealed class Candidate : TenantAggregateRoot<CandidateId>, IAuditableEntity
{
    private readonly List<CandidateStageHistoryEntry> _history = [];

    private Candidate()
    {
        // EF Core
    }

    private Candidate(
        CandidateId id,
        Guid? userId,
        string fullName,
        ContactEmail email,
        string? phone,
        CandidateSource source,
        CandidateStatus status,
        string? resumeUrl,
        DateTime utcNow)
    {
        Id = id;
        UserId = userId;
        FullName = fullName;
        Email = email;
        Phone = phone;
        Source = source;
        Status = status;
        ResumeUrl = resumeUrl;
        CreatedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    public Guid? UserId { get; private set; }

    public string FullName { get; private set; } = default!;

    public ContactEmail Email { get; private set; } = default!;

    public string? Phone { get; private set; }

    public CandidateSource Source { get; private set; }

    public CandidateStatus Status { get; private set; }

    public string? ResumeUrl { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? UpdatedAtUtc { get; private set; }

    public IReadOnlyList<CandidateStageHistoryEntry> History => _history.AsReadOnly();

    private void Touch(DateTime utcNow) => UpdatedAtUtc = utcNow;

    /// <summary>Nasce em Sourced (prospecção) ou Applied (candidatura direta).</summary>
    public static Result<Candidate> Create(
        string? fullName, string? email, string? phone,
        CandidateSource source, string? resumeUrl = null,
        Guid? userId = null, DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;

        if (string.IsNullOrWhiteSpace(fullName))
            return Result.Failure<Candidate>(CandidateErrors.FullNameRequired);
        if (fullName.Trim().Length > FullNameMaxLength)
            return Result.Failure<Candidate>(CandidateErrors.FieldTooLong("Nome", FullNameMaxLength));
        if (!string.IsNullOrWhiteSpace(phone) && phone.Trim().Length > PhoneMaxLength)
            return Result.Failure<Candidate>(CandidateErrors.FieldTooLong("Telefone", PhoneMaxLength));

        var emailResult = ContactEmail.Create(email);
        if (emailResult.IsFailure)
            return Result.Failure<Candidate>(emailResult.Error!);

        var status = source == CandidateSource.Applied ? CandidateStatus.Applied : CandidateStatus.Sourced;
        var candidate = new Candidate(
            new CandidateId(Guid.NewGuid()),
            userId,
            fullName.Trim(),
            emailResult.Value,
            string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
            source,
            status,
            resumeUrl,
            now);

        if (status == CandidateStatus.Applied)
            candidate.RaiseDomainEvent(new Events.CandidateAppliedDomainEvent(candidate.Id));

        return candidate;
    }

    public const int FullNameMaxLength = 200;
    public const int PhoneMaxLength = 30;

    /// <summary>
    /// Avança exatamente um estágio da máquina. Quando o destino é Interviewing,
    /// o handler informa se existe entrevista agendada para o candidato.
    /// </summary>
    public Result Advance(CandidateStatus target, bool hasScheduledInterview, Guid changedBy, DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;

        if (!AllowedTransitions.TryGetValue(Status, out var allowed) || !allowed.Contains(target))
            return Result.Failure(CandidateErrors.InvalidStatusTransition);

        if (target == CandidateStatus.Interviewing && !hasScheduledInterview)
            return Result.Failure(CandidateErrors.InterviewRequiredForInterviewing);

        var previous = Status;
        AppendHistory(previous, target, changedBy, now);
        Status = target;
        Touch(now);

        RaiseDomainEvent(new Events.CandidateAdvancedDomainEvent(Id.Value, (int)previous, (int)target));

        if (target == CandidateStatus.Hired)
            RaiseDomainEvent(new Events.CandidateHiredDomainEvent(Id, TenantId));

        return Result.Success();
    }

    public Result Reject(Guid changedBy, DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;

        if (!AllowedTransitions.ContainsKey(Status))
            return Result.Failure(CandidateErrors.InvalidStatusTransition);

        var previous = Status;
        AppendHistory(previous, CandidateStatus.Rejected, changedBy, now);
        Status = CandidateStatus.Rejected;
        Touch(now);

        RaiseDomainEvent(new Events.CandidateAdvancedDomainEvent(Id.Value, (int)previous, (int)CandidateStatus.Rejected));
        return Result.Success();
    }

    private static readonly IReadOnlyDictionary<CandidateStatus, CandidateStatus[]> AllowedTransitions =
        new Dictionary<CandidateStatus, CandidateStatus[]>
        {
            [CandidateStatus.Sourced] = [CandidateStatus.Applied],
            [CandidateStatus.Applied] = [CandidateStatus.Screened],
            [CandidateStatus.Screened] = [CandidateStatus.Interviewing],
            [CandidateStatus.Interviewing] = [CandidateStatus.Offered],
            [CandidateStatus.Offered] = [CandidateStatus.Hired]
        };

    private void AppendHistory(CandidateStatus from, CandidateStatus to, Guid changedBy, DateTime utcNow) =>
        _history.Add(CandidateStageHistoryEntry.Create(from, to, changedBy, utcNow));
}
