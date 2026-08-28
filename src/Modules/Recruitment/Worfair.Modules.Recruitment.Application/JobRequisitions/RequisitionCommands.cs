namespace Worfair.Modules.Recruitment.Application.JobRequisitions;

using FluentValidation;
using Worfair.BuildingBlocks.Application.Cqrs;

public sealed record AddHiringTeamMemberCommand(Guid JobRequisitionId, Guid RecruiterUserId, int Role)
    : ICommand;

public sealed class AddHiringTeamMemberCommandValidator : AbstractValidator<AddHiringTeamMemberCommand>
{
    public AddHiringTeamMemberCommandValidator()
    {
        RuleFor(c => c.RecruiterUserId)
            .NotEmpty().WithErrorCode("JobRequisition.DuplicateTeamMember");
        RuleFor(c => c.Role)
            .InclusiveBetween(1, 4).WithErrorCode("JobRequisition.InvalidStatusTransition");
    }
}

public sealed record PublishJobRequisitionCommand(Guid JobRequisitionId) : ICommand;

public sealed record PauseJobRequisitionCommand(Guid JobRequisitionId) : ICommand;

public sealed record ResumeJobRequisitionCommand(Guid JobRequisitionId) : ICommand;

public sealed record CloseJobRequisitionCommand(Guid JobRequisitionId, string Reason) : ICommand;

public sealed class CloseJobRequisitionCommandValidator : AbstractValidator<CloseJobRequisitionCommand>
{
    public CloseJobRequisitionCommandValidator() =>
        RuleFor(c => c.Reason)
            .NotEmpty().WithErrorCode("JobRequisition.CloseReasonRequired");
}

public sealed record CancelJobRequisitionCommand(Guid JobRequisitionId, string Reason) : ICommand;

public sealed class CancelJobRequisitionCommandValidator : AbstractValidator<CancelJobRequisitionCommand>
{
    public CancelJobRequisitionCommandValidator() =>
        RuleFor(c => c.Reason)
            .NotEmpty().WithErrorCode("JobRequisition.CloseReasonRequired");
}

public sealed record ChangeSalaryRangeCommand(
    Guid JobRequisitionId,
    decimal SalaryMin,
    decimal? SalaryMax,
    string Currency) : ICommand;

public sealed class ChangeSalaryRangeCommandValidator : AbstractValidator<ChangeSalaryRangeCommand>
{
    public ChangeSalaryRangeCommandValidator()
    {
        RuleFor(c => c.SalaryMin)
            .GreaterThanOrEqualTo(0).WithErrorCode("Money.NegativeAmount");

        RuleFor(c => c.SalaryMax)
            .GreaterThanOrEqualTo(0).WithErrorCode("Money.NegativeAmount")
            .When(c => c.SalaryMax.HasValue);

        RuleFor(c => c.Currency)
            .NotEmpty().WithErrorCode("Money.InvalidCurrency")
            .Length(3).WithErrorCode("Money.InvalidCurrency");
    }
}
