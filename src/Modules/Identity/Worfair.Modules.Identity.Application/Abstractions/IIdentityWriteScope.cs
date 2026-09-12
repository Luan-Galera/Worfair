namespace Worfair.Modules.Identity.Application.Abstractions;

using Worfair.BuildingBlocks.Domain.ValueObjects;

/// <summary>
/// Escopo de escrita com tenant explícito (R-04): login/switch/refresh gravam
/// refresh_tokens do tenant ALVO, mas a conexão chega com o tenant do token
/// vigente (ou vazio) — o RLS barraria. Fixa o alvo no escopo da transação.
/// </summary>
public interface IIdentityWriteScope
{
    Task<T> ExecuteAsync<T>(
        TenantId? tenantId, Func<Task<T>> action, CancellationToken cancellationToken = default);

    Task ExecuteAsync(
        TenantId? tenantId, Func<Task> action, CancellationToken cancellationToken = default);
}
