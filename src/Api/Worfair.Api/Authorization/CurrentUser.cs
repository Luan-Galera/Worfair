namespace Worfair.Api.Authorization;

using System.Security.Claims;
using Worfair.BuildingBlocks.Application.Security;

/// <summary>Claims do JWT validado — apenas chaves de busca (SEC-01).</summary>
public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid? UserId
    {
        get
        {
            var value = User()?.FindFirst("sub")?.Value;
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public Guid? TenantId
    {
        get
        {
            var value = User()?.FindFirst("tenant_id")?.Value;
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public bool IsAuthenticated => User()?.Identity?.IsAuthenticated == true;

    private ClaimsPrincipal? User() => accessor.HttpContext?.User;
}
