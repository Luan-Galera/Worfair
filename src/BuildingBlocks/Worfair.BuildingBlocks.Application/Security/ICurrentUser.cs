namespace Worfair.BuildingBlocks.Application.Security;

/// <summary>
/// Usuário corrente extraído do JWT JÁ VALIDADO (apenas chaves de busca — SEC-01 §4).
/// Implementação no host lê os claims; handlers nunca recebem TenantId como input (R-06).
/// </summary>
public interface ICurrentUser
{
    Guid? UserId { get; }

    /// <summary>Tenant efetivo do contexto (claim tenant_id); null = global.</summary>
    Guid? TenantId { get; }

    bool IsAuthenticated { get; }
}
