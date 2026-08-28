namespace Worfair.BuildingBlocks.Domain.Errors;

/// <summary>
/// Result pattern (D-10): falhas de negócio nunca são exceções (docs/architecture/05 §1).
/// </summary>
public class Result
{
    protected Result(bool isSuccess, Error? error)
    {
        if (isSuccess && error is not null && error != Error.None)
            throw new InvalidOperationException("Resultado de sucesso não pode carregar erro.");
        if (!isSuccess && (error is null || error == Error.None))
            throw new InvalidOperationException("Resultado de falha exige um erro.");

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error? Error { get; }

    public static Result Success() => new(true, null);

    public static Result Failure(Error error) => new(false, error);

    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, null);

    public static Result<TValue> Failure<TValue>(Error error) => new(default!, false, error);
}

public sealed class Result<TValue> : Result
{
    private readonly TValue? _value;

    internal Result(TValue value, bool isSuccess, Error? error)
        : base(isSuccess, error)
        => _value = value;

    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Não há valor em um resultado de falha.");

    public static implicit operator Result<TValue>(TValue value) => Success(value);
}
