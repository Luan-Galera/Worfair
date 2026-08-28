namespace Worfair.Modules.Tenants.Application.Abstractions;

using Worfair.BuildingBlocks.Domain.Abstractions;

/// <summary>UoW do módulo Tenants (commit único + despacho de domain events → outbox).</summary>
public interface ITenancyUnitOfWork : IUnitOfWork;
