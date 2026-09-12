namespace Worfair.Modules.Jobs.Application.JobApplications;

using Worfair.BuildingBlocks.Application.Cqrs;
using Worfair.BuildingBlocks.Application.Security;
using Worfair.BuildingBlocks.Domain.Errors;
using Worfair.Modules.Jobs.Application.Dtos;
using Worfair.Modules.Jobs.Domain.Abstractions;

public sealed record ListMyJobApplicationsQuery : IQuery<Result<IReadOnlyList<JobApplicationDto>>>;

public sealed class ListMyJobApplicationsQueryHandler(
    IJobApplicationRepository applications,
    ICurrentUser currentUser)
    : IQueryHandler<ListMyJobApplicationsQuery, Result<IReadOnlyList<JobApplicationDto>>>
{
    public async Task<Result<IReadOnlyList<JobApplicationDto>>> Handle(
        ListMyJobApplicationsQuery query, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
            return Result.Failure<IReadOnlyList<JobApplicationDto>>(new Error("Auth.InvalidCredentials", "Usuário não autenticado."));

        var list = await applications.ListByApplicantAsync(userId, cancellationToken).ConfigureAwait(false);
        return Result.Success<IReadOnlyList<JobApplicationDto>>(
            [.. list.Select(ApplyToJobCommandHandler.ToDto)]);
    }
}
