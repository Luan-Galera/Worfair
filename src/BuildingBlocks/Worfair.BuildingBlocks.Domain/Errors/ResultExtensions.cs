namespace Worfair.BuildingBlocks.Domain.Errors;

public static class ResultExtensions
{
    public static TOut Match<TOut>(
        this Result result,
        Func<TOut> onSuccess,
        Func<Error, TOut> onFailure)
        => result.IsSuccess ? onSuccess() : onFailure(result.Error!);

    public static TOut Match<TValue, TOut>(
        this Result<TValue> result,
        Func<TValue, TOut> onSuccess,
        Func<Error, TOut> onFailure)
        => result.IsSuccess ? onSuccess(result.Value) : onFailure(result.Error!);

    public static async Task<TOut> MatchAsync<TValue, TOut>(
        this Task<Result<TValue>> task,
        Func<TValue, TOut> onSuccess,
        Func<Error, TOut> onFailure)
    {
        var result = await task.ConfigureAwait(false);
        return result.Match(onSuccess, onFailure);
    }

    /// <summary>Encadeia a próxima operação apenas quando o resultado é sucesso.</summary>
    public static Result<TOut> Bind<TIn, TOut>(this Result<TIn> result, Func<TIn, Result<TOut>> next)
        => result.IsSuccess ? next(result.Value) : Result.Failure<TOut>(result.Error!);

    public static Result Bind(this Result result, Func<Result> next)
        => result.IsSuccess ? next() : result;
}
