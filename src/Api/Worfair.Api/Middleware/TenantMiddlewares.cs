namespace Worfair.Api.Middleware;

/// <summary>
/// SEC-02: rejeita explicitamente X-Tenant-Id com 400 — o tenant é derivado
/// EXCLUSIVAMENTE do token de autenticação. Registrar ANTES de todo o pipeline.
/// </summary>
public sealed class TenantHeaderGuardMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Headers.ContainsKey("X-Tenant-Id"))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "X-Tenant-Id não é aceito. O tenant é derivado do token de autenticação."
            });
            return;
        }

        await next(context);
    }
}

/// <summary>
/// Alimenta o ITenantProvider UMA vez por request, a partir do claim do JWT
/// JÁ validado pelo JwtBearer (docs/architecture/04 §3.1).
/// </summary>
public sealed class TenantContextMiddleware(
    RequestDelegate next,
    Worfair.BuildingBlocks.Domain.Tenancy.ITenantProvider tenantProvider)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var claim = context.User.FindFirst("tenant_id")?.Value;
        if (claim is not null && Guid.TryParse(claim, out var tenantId))
            tenantProvider.SetTenant(new Worfair.BuildingBlocks.Domain.ValueObjects.TenantId(tenantId));

        await next(context);
    }
}
