namespace Worfair.Modules.Identity.Infrastructure.Persistence;

using Worfair.BuildingBlocks.Domain.Abstractions;
using Worfair.BuildingBlocks.Infrastructure.Persistence;
using Worfair.Modules.Identity.Application.Abstractions;

/// <summary>UoW tipado do módulo Identity (registro DI sem conflito entre módulos).</summary>
public sealed class IdentityUnitOfWork(IdentityDbContext dbContext, IDomainEventDispatcher dispatcher)
    : UnitOfWork<IdentityDbContext>(dbContext, dispatcher), IIdentityUnitOfWork;
