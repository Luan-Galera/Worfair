namespace Worfair.Api.Middleware;

using Microsoft.AspNetCore.Mvc;
using Worfair.BuildingBlocks.Application.Exceptions;
using Worfair.BuildingBlocks.Infrastructure.Persistence.Tenant;

/// <summary>
/// Mapeia exceções de infraestrutura/validação para respostas HTTP coerentes.
/// Falhas de NEGÓCIO não chegam aqui (Result pattern, D-10).
/// </summary>
public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException validationException)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = 400,
                Title = "Dados inválidos",
                Detail = string.Join("; ",
                    validationException.Failures.Select(f => $"{f.PropertyName}: {f.ErrorMessage}")),
                Instance = context.Request.Path
            });
        }
        catch (TenantRequiredException ex)
        {
            // Deny-by-default: contexto de tenant ausente em operação tenant-scoped.
            logger.LogWarning("Tenant obrigatório ausente: {Message}", ex.Message);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = 403,
                Title = "Contexto de tenant obrigatório",
                Detail = "Esta operação exige um contexto de tenant válido."
            });
        }
        catch (TenantMismatchException ex)
        {
            // R-06: tentativa cross-tenant bloqueada — sem detalhes reveladores.
            logger.LogWarning("Cross-tenant bloqueado: {Message}", ex.Message);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = 403,
                Title = "Acesso negado"
            });
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Cliente cancelou — nada a fazer.
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Erro não tratado em {Path}", context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = 500,
                Title = "Erro interno",
                Detail = "Ocorreu um erro inesperado."
            });
        }
    }
}
