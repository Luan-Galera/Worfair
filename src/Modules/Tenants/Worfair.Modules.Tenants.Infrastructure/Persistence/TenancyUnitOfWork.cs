namespace Worfair.Modules.Tenants.Infrastructure.Persistence;

using Worfair.BuildingBlocks.Domain.Abstractions;
using Worfair.BuildingBlocks.Infrastructure.Persistence;
using Worfair.Modules.Tenants.Application.Abstractions;

/// <summary>UoW tipado do módulo Tenants (registro DI sem conflito entre módulos).</summary>
public sealed class TenancyUnitOfWork(TenancyDbContext dbContext, IDomainEventDispatcher dispatcher)
    : UnitOfWork<TenancyDbContext>(dbContext, dispatcher), ITenancyUnitOfWork;
