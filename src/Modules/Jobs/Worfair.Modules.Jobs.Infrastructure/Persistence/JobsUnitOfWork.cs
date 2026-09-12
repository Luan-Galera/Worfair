namespace Worfair.Modules.Jobs.Infrastructure.Persistence;

using Worfair.BuildingBlocks.Domain.Abstractions;
using Worfair.BuildingBlocks.Infrastructure.Persistence;
using Worfair.Modules.Jobs.Application.Abstractions;

public sealed class JobsUnitOfWork(JobsDbContext dbContext, IDomainEventDispatcher dispatcher)
    : UnitOfWork<JobsDbContext>(dbContext, dispatcher), IJobsUnitOfWork;
