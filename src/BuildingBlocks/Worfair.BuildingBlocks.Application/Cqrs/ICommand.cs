namespace Worfair.BuildingBlocks.Application.Cqrs;

using MediatR;
using Worfair.BuildingBlocks.Domain.Errors;

/// <summary>Comando: intenção de escrita. Nunca carrega TenantId (R-06/docs architecture §6).</summary>
public interface ICommand<TResult> : IRequest<TResult>;

public interface ICommand : IRequest<Result>;

/// <summary>Consulta: leitura sem efeito colateral.</summary>
public interface IQuery<TResult> : IRequest<TResult>;
