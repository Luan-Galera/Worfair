namespace Worfair.Modules.Recruitment.Infrastructure.Persistence;

using Worfair.BuildingBlocks.Domain.Abstractions;
using Worfair.BuildingBlocks.Infrastructure.Persistence;

/// <summary>UoW tipado do módulo Recruitment (registro DI sem conflito entre módulos).</summary>
public sealed class RecruitmentUnitOfWork(RecruitmentDbContext dbContext, IDomainEventDispatcher dispatcher)
    : UnitOfWork<RecruitmentDbContext>(dbContext, dispatcher), IRecruitmentUnitOfWork;
