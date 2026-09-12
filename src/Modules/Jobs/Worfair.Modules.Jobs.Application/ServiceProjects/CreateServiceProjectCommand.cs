namespace Worfair.Modules.Jobs.Application.ServiceProjects;

using FluentValidation;
using Worfair.BuildingBlocks.Application.Cqrs;
using Worfair.BuildingBlocks.Application.Security;
using Worfair.BuildingBlocks.Domain.Errors;
using Worfair.BuildingBlocks.Domain.Tenancy;
using Worfair.Modules.Jobs.Application.Abstractions;
using Worfair.Modules.Jobs.Application.Dtos;
using Worfair.Modules.Jobs.Domain.Abstractions;
using Worfair.Modules.Jobs.Domain.Aggregates.ServiceProject;
using Worfair.Modules.Jobs.Domain.Errors;

public sealed record CreateServiceProjectCommand(
    Guid? CompanyId,
    string Title,
    string Description,
    decimal BudgetMin,
    decimal? BudgetMax,
    string? Currency,
    DateTime? Deadline,
    string? Category = null,
    string? CompanyName = null) : ICommand<Result<ServiceProjectDto>>;

public sealed class CreateServiceProjectCommandValidator : AbstractValidator<CreateServiceProjectCommand>
{
    public CreateServiceProjectCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithErrorCode("Jobs.TitleRequired")
            .MaximumLength(200).WithErrorCode("Jobs.TitleTooLong");

        RuleFor(x => x.Description)
            .NotEmpty().WithErrorCode("Jobs.DescriptionRequired");

        RuleFor(x => x.BudgetMin)
            .GreaterThanOrEqualTo(0).WithErrorCode("Jobs.InvalidBudget");

        RuleFor(x => x.Currency)
            .MaximumLength(3).WithErrorCode("Jobs.InvalidCurrency");

        RuleFor(x => x.Category)
            .MaximumLength(60).WithErrorCode("Jobs.CategoryTooLong");

        RuleFor(x => x.CompanyName)
            .MaximumLength(120).WithErrorCode("Jobs.CompanyNameTooLong");
    }
}

public sealed class CreateServiceProjectCommandHandler(
    IServiceProjectRepository projects,
    IJobsUnitOfWork unitOfWork,
    ITenantProvider tenantProvider,
    ICurrentUser currentUser)
    : ICommandHandler<CreateServiceProjectCommand, Result<ServiceProjectDto>>
{
    public async Task<Result<ServiceProjectDto>> Handle(CreateServiceProjectCommand command, CancellationToken cancellationToken)
    {
        if (tenantProvider.TenantId is null)
            return Result.Failure<ServiceProjectDto>(Worfair.Modules.Jobs.Domain.Errors.JobsErrors.NotFound);

        var createResult = ServiceProject.Create(
            command.CompanyId,
            currentUser.UserId,
            command.Title,
            command.Description,
            command.BudgetMin,
            command.BudgetMax,
            command.Currency,
            command.Deadline,
            DateTime.UtcNow,
            command.Category,
            command.CompanyName);

        if (createResult.IsFailure)
            return Result.Failure<ServiceProjectDto>(createResult.Error!);

        var project = createResult.Value;
        await projects.AddAsync(project, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new ServiceProjectDto(
            project.Id.Value,
            project.CompanyId,
            project.CreatedBy,
            project.Title,
            project.Description,
            project.BudgetMin,
            project.BudgetMax,
            project.Currency,
            project.Deadline,
            (int)project.Status,
            project.CreatedAtUtc,
            project.UpdatedAtUtc,
            project.Category,
            project.CompanyName);
    }
}
