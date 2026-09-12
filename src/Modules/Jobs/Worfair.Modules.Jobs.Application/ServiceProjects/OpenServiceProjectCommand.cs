namespace Worfair.Modules.Jobs.Application.ServiceProjects;

using Worfair.BuildingBlocks.Application.Cqrs;
using Worfair.BuildingBlocks.Domain.Errors;
using Worfair.Modules.Jobs.Application.Abstractions;
using Worfair.Modules.Jobs.Application.Dtos;
using Worfair.Modules.Jobs.Domain.Abstractions;
using Worfair.Modules.Jobs.Domain.Aggregates.ServiceProject;
using Worfair.Modules.Jobs.Domain.Errors;

public sealed record OpenServiceProjectCommand(Guid ServiceProjectId) : ICommand<Result<ServiceProjectDto>>;

public sealed class OpenServiceProjectCommandHandler(
    IServiceProjectRepository projects,
    IJobsUnitOfWork unitOfWork)
    : ICommandHandler<OpenServiceProjectCommand, Result<ServiceProjectDto>>
{
    public async Task<Result<ServiceProjectDto>> Handle(OpenServiceProjectCommand command, CancellationToken cancellationToken)
    {
        var project = await projects.GetByIdAsync(new ServiceProjectId(command.ServiceProjectId), cancellationToken).ConfigureAwait(false);
        if (project is null)
            return Result.Failure<ServiceProjectDto>(JobsErrors.NotFound);

        var result = project.Open();
        if (result.IsFailure)
            return Result.Failure<ServiceProjectDto>(result.Error!);

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
