namespace Worfair.Modules.Recruitment.Domain.Aggregates.JobRequisition;

using Worfair.BuildingBlocks.Domain.Auditing;
using Worfair.BuildingBlocks.Domain.Errors;
using Worfair.BuildingBlocks.Domain.Tenancy;
using Worfair.BuildingBlocks.Domain.ValueObjects;
using Worfair.Modules.Recruitment.Domain.Abstractions;
using Worfair.Modules.Recruitment.Domain.Enums;
using Worfair.Modules.Recruitment.Domain.Errors;
using Worfair.Modules.Recruitment.Domain.ValueObjects;

/// <summary>
/// Requisição de vaga — operação INTERNA do processo seletivo (ATS).
/// Diferente de JobPosting (vitrine do módulo Jobs). Tenant-owned.
/// Invariantes (docs/architecture/03 §3.2 e 05 §5):
/// não publica sem time de contratação; Minimum ≤ Maximum na mesma moeda;
/// faixa salarial imutável após Closed/Cancelled; recrutador único no time;
/// time travado fora de rascunho.
/// </summary>
public sealed class JobRequisition : TenantAggregateRoot<JobRequisitionId>, IAuditableEntity
{
    private readonly List<HiringTeamMember> _hiringTeam = [];

    private JobRequisition()
    {
        // EF Core
    }

    private JobRequisition(
        JobRequisitionId id,
        Guid? companyId,
        Guid? createdBy,
        JobTitle title,
        string description,
        SalaryRange salaryRange,
        DateTime utcNow)
    {
        Id = id;
        CompanyId = companyId;
        CreatedBy = createdBy;
        Title = title;
        Description = description;
        SalaryRange = salaryRange;
        Status = JobRequisitionStatus.Draft;
        CreatedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    public Guid? CompanyId { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public JobTitle Title { get; private set; } = default!;

    public string Description { get; private set; } = default!;

    public SalaryRange SalaryRange { get; private set; } = default!;

    public JobRequisitionStatus Status { get; private set; }

    public DateTime? PublishedAtUtc { get; private set; }

    public DateTime? ClosedAtUtc { get; private set; }

    public string? CloseReason { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? UpdatedAtUtc { get; private set; }

    public IReadOnlyList<HiringTeamMember> HiringTeam => _hiringTeam.AsReadOnly();

    private void Touch(DateTime utcNow) => UpdatedAtUtc = utcNow;

    /// <summary>Nasce em Draft; o tenant é informado pelo interceptor em SaveChanges.</summary>
    public static Result<JobRequisition> Create(
        Guid? companyId, Guid? createdBy, string? title, string? description,
        SalaryRange salaryRange, DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;

        if (string.IsNullOrWhiteSpace(description))
            return Result.Failure<JobRequisition>(RecruitmentErrors.DescriptionRequired);

        var titleResult = JobTitle.Create(title);
        if (titleResult.IsFailure)
            return Result.Failure<JobRequisition>(titleResult.Error!);

        var requisition = new JobRequisition(
            new JobRequisitionId(Guid.NewGuid()),
            companyId,
            createdBy,
            titleResult.Value,
            description.Trim(),
            salaryRange,
            now);

        requisition.RaiseDomainEvent(
            new Events.JobRequisitionCreatedDomainEvent(requisition.Id, requisition.Title.Value));

        return requisition;
    }

    public Result AddTeamMember(Guid recruiterUserId, HiringRole role, DateTime? utcNow = null)
    {
        if (Status != JobRequisitionStatus.Draft)
            return Result.Failure(RecruitmentErrors.TeamLockedAfterPublish);

        if (_hiringTeam.Any(m => m.RecruiterUserId == recruiterUserId))
            return Result.Failure(RecruitmentErrors.DuplicateTeamMember);

        _hiringTeam.Add(HiringTeamMember.Create(recruiterUserId, role, utcNow ?? DateTime.UtcNow));
        Touch(utcNow ?? DateTime.UtcNow);
        return Result.Success();
    }

    public Result Publish(DateTime? utcNow = null)
    {
        if (Status != JobRequisitionStatus.Draft)
            return Result.Failure(RecruitmentErrors.InvalidStatusTransition);

        if (_hiringTeam.Count == 0)
            return Result.Failure(RecruitmentErrors.CannotPublishWithoutTeam);

        Status = JobRequisitionStatus.Published;
        PublishedAtUtc = utcNow ?? DateTime.UtcNow;
        Touch(PublishedAtUtc.Value);

        RaiseDomainEvent(new Events.JobRequisitionPublishedDomainEvent(Id, Title.Value));
        return Result.Success();
    }

    public Result Pause(DateTime? utcNow = null)
    {
        if (Status != JobRequisitionStatus.Published)
            return Result.Failure(RecruitmentErrors.InvalidStatusTransition);

        Status = JobRequisitionStatus.Paused;
        Touch(utcNow ?? DateTime.UtcNow);
        return Result.Success();
    }

    public Result Resume(DateTime? utcNow = null)
    {
        if (Status != JobRequisitionStatus.Paused)
            return Result.Failure(RecruitmentErrors.InvalidStatusTransition);

        Status = JobRequisitionStatus.Published;
        Touch(utcNow ?? DateTime.UtcNow);
        return Result.Success();
    }

    public Result ChangeSalaryRange(SalaryRange newSalaryRange, DateTime? utcNow = null)
    {
        if (Status is JobRequisitionStatus.Closed or JobRequisitionStatus.Cancelled)
            return Result.Failure(RecruitmentErrors.CannotChangeClosed);

        SalaryRange = newSalaryRange;
        Touch(utcNow ?? DateTime.UtcNow);
        return Result.Success();
    }

    public Result Close(string? reason, DateTime? utcNow = null)
    {
        if (Status is JobRequisitionStatus.Closed or JobRequisitionStatus.Cancelled)
            return Result.Failure(RecruitmentErrors.InvalidStatusTransition);

        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure(RecruitmentErrors.CloseReasonRequired);

        var now = utcNow ?? DateTime.UtcNow;
        Status = JobRequisitionStatus.Closed;
        ClosedAtUtc = now;
        CloseReason = reason.Trim();
        Touch(now);

        RaiseDomainEvent(new Events.JobRequisitionClosedDomainEvent(Id, CloseReason));
        return Result.Success();
    }

    public Result Cancel(string? reason, DateTime? utcNow = null)
    {
        if (Status is JobRequisitionStatus.Closed or JobRequisitionStatus.Cancelled)
            return Result.Failure(RecruitmentErrors.InvalidStatusTransition);

        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure(RecruitmentErrors.CloseReasonRequired);

        var now = utcNow ?? DateTime.UtcNow;
        Status = JobRequisitionStatus.Cancelled;
        ClosedAtUtc = now;
        CloseReason = reason.Trim();
        Touch(now);

        RaiseDomainEvent(new Events.JobRequisitionCancelledDomainEvent(Id, CloseReason));
        return Result.Success();
    }
}
