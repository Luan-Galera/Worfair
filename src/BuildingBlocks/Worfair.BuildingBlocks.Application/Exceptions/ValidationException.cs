namespace Worfair.BuildingBlocks.Application.Exceptions;

using FluentValidation.Results;

/// <summary>Erro de entrada (400) — distinto de falha de negócio (Result).</summary>
public sealed class ValidationException(IReadOnlyList<ValidationFailure> failures)
    : Exception("A requisição contém dados inválidos.")
{
    public IReadOnlyList<ValidationFailure> Failures { get; } = failures;
}
