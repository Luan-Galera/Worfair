namespace Worfair.Api.Extensions;

using Microsoft.AspNetCore.Mvc;
using Worfair.BuildingBlocks.Domain.Errors;

/// <summary>Mapeia Result (D-10) → respostas HTTP sem exceções.</summary>
public static class ResultsExtensions
{
    /// <summary>Result com valor: 200; falha → ProblemDetails por código.</summary>
    public static IResult ToHttpResult<T>(this Result<T> result) =>
        result.IsSuccess ? Results.Ok(result.Value) : ToProblem(result.Error!);

    /// <summary>Result simples: 204; falha → ProblemDetails por código.</summary>
    public static IResult ToAcceptedResult(this Result result) =>
        result.IsSuccess ? Results.NoContent() : ToProblem(result.Error!);

    public static IResult Created<T>(this Result<T> result, string routeName, Func<T, object> routeValues) =>
        result.IsSuccess
            ? Results.CreatedAtRoute(routeName, routeValues(result.Value), result.Value)
            : ToProblem(result.Error!);

    private static IResult ToProblem(Error error)
    {
        var status = error.Code switch
        {
            var c when c.EndsWith(".NotFound") => StatusCodes.Status404NotFound,
            var c when c.EndsWith(".Taken") || c.EndsWith(".Duplicated") || c.EndsWith(".AlreadyMember")
                || c.EndsWith(".AlreadyGranted") => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };

        return Results.Problem(
            title: error.Message,
            detail: error.Message,
            statusCode: status,
            extensions: new Dictionary<string, object?>
            {
                ["errorCode"] = error.Code
            });
    }
}
