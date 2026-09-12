namespace Worfair.Modules.Tenants.Contracts;

using Worfair.BuildingBlocks.Domain.ValueObjects;

/// <summary>Resumo de membership para leitura cross-module (read-only, R-07).</summary>
public sealed record MembershipInfo(
    Guid TenantIdValue,
    string TenantName,
    int Status,
    DateTime JoinedAtUtc);

public sealed record TenantSummaryInfo(Guid Id, string Name, string Slug, int Status);

/// <summary>
/// Porta read-only do módulo Tenants consumida por outros módulos
/// (leitura síncrona via contratos — docs/architecture/01 §4.3).
/// Implementada em Tenants.Infrastructure; NUNCA referencia tabelas alheias.
/// </summary>
public interface ITenancyReadContract
{
    Task<bool> IsTenantActiveAsync(TenantId tenantId, CancellationToken cancellationToken = default);

    Task<string?> GetTenantNameAsync(TenantId tenantId, CancellationToken cancellationToken = default);

    /// <summary>Membership ativa do usuário no tenant informado (ou null).</summary>
    Task<bool> HasActiveMembershipAsync(Guid userId, TenantId tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Todas as memberships ativas do usuário em TODOS os tenants (uso exclusivo
    /// da autorização/switch — leitura elevada controlada, auditável).
    /// </summary>
    Task<IReadOnlyList<MembershipInfo>> ListActiveMembershipsAcrossTenantsAsync(
        Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// A empresa existe e está ativa NO tenant informado (trava vaga×empresa —
    /// leitura elevada no tenant alvo, revertida ao final).
    /// </summary>
    Task<bool> CompanyBelongsToTenantAsync(
        TenantId tenantId, Guid companyId, CancellationToken cancellationToken = default);
}
