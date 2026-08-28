namespace Worfair.Modules.Recruitment.Application.Abstractions;

using Worfair.BuildingBlocks.Domain.Abstractions;

/// <summary>UoW do módulo Recruitment (commit único + despacho de domain events → outbox).</summary>
public interface IRecruitmentUnitOfWork : IUnitOfWork;
