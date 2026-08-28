namespace Worfair.Modules.Recruitment.Application.JobRequisitions;

using FluentValidation;
using Worfair.BuildingBlocks.Application.Cqrs;
using Worfair.Modules.Recruitment.Application.Dtos;

/// <summary>Cria requisição de vaga em Draft (tenant vem do token — R-06).</summary>
public sealed record CreateJobRequisitionCommand(
    Guid? CompanyId,
    string Title,
    string Description,
    decimal SalaryMin,
    decimal? SalaryMax,
    string Currency) : ICommand<Result<JobRequisitionDto>>;

public sealed class CreateJobRequisitionCommandValidator : AbstractValidator<CreateJobRequisitionCommand>
{
    public CreateJobRequisitionCommandValidator()
    {
        RuleFor(c => c.Title)
            .NotEmpty().WithErrorCode("JobRequisition.JobTitleRequired")
            .MaximumLength(JobTitleMaxLength).WithErrorCode("JobRequisition.JobTitleTooLong");

        RuleFor(c => c.Description)
            .NotEmpty().WithErrorCode("JobRequisition.DescriptionRequired");

        RuleFor(c => c.SalaryMin)
            .GreaterThanOrEqualTo(0).WithErrorCode("Money.NegativeAmount");

        RuleFor(c => c.SalaryMax)
            .GreaterThanOrEqualTo(0).WithErrorCode("Money.NegativeAmount")
            .When(c => c.SalaryMax.HasValue);

        RuleFor(c => c.Currency)
            .NotEmpty().WithErrorCode("Money.InvalidCurrency")
            .Length(3).WithErrorCode("Money.InvalidCurrency");
    }

    private const int JobTitleMaxLength = 120;
}
