namespace Worfair.Modules.Recruitment.Application.Candidates;

using FluentValidation;
using Worfair.BuildingBlocks.Application.Cqrs;
using Worfair.Modules.Recruitment.Application.Dtos;

/// <summary>Registra candidato no pipeline do tenant (sourced ou aplicação direta).</summary>
public sealed record CreateCandidateCommand(
    string FullName,
    string Email,
    string? Phone,
    int Source,
    string? ResumeUrl) : ICommand<Result<CandidateDto>>;

public sealed class CreateCandidateCommandValidator : AbstractValidator<CreateCandidateCommand>
{
    public CreateCandidateCommandValidator()
    {
        RuleFor(c => c.FullName)
            .NotEmpty().WithErrorCode("Candidate.FullNameRequired")
            .MaximumLength(200).WithErrorCode("Candidate.FieldTooLong");

        RuleFor(c => c.Email)
            .NotEmpty().WithErrorCode("Candidate.ContactEmailRequired")
            .MaximumLength(320).WithErrorCode("Candidate.ContactEmailInvalid");

        RuleFor(c => c.Phone)
            .MaximumLength(30).WithErrorCode("Candidate.FieldTooLong");

        RuleFor(c => c.Source)
            .InclusiveBetween(1, 2).WithErrorCode("Candidate.InvalidStatusTransition");
    }
}

public sealed record AdvanceCandidateCommand(Guid CandidateId, int TargetStage) : ICommand<Result<CandidateDto>>;

public sealed class AdvanceCandidateCommandValidator : AbstractValidator<AdvanceCandidateCommand>
{
    public AdvanceCandidateCommandValidator() =>
        RuleFor(c => c.TargetStage)
            .InclusiveBetween(1, 7).WithErrorCode("Candidate.InvalidStatusTransition");
}

public sealed record RejectCandidateCommand(Guid CandidateId) : ICommand;

public sealed record HireCandidateCommand(Guid CandidateId) : ICommand;
