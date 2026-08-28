namespace Worfair.BuildingBlocks.Application.Cqrs;

using MediatR;
using Worfair.BuildingBlocks.Domain.Errors;

/// <summary>Handler de comando com retorno tipado (ex.: Result&lt;Dto&gt;).</summary>
public interface ICommandHandler<in TCommand, TResult> : IRequestHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>;

/// <summary>Handler de comando que devolve Result puro.</summary>
public interface ICommandHandler<in TCommand> : IRequestHandler<TCommand, Result>
    where TCommand : ICommand;

/// <summary>Handler de query.</summary>
public interface IQueryHandler<in TQuery, TResult> : IRequestHandler<TQuery, TResult>
    where TQuery : IQuery<TResult>;
