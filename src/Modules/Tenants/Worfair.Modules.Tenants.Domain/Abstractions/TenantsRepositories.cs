namespace Worfair.Modules.Tenants.Domain.Abstractions;

using Worfair.BuildingBlocks.Domain.ValueObjects;
using Worfair.Modules.Tenants.Domain.Aggregates.Company;
using Worfair.Modules.Tenants.Domain.Aggregates.Membership;
using Worfair.Modules.Tenants.Domain.Aggregates.Settings;
using Worfair.Modules.Tenants.Domain.Aggregates.Tenant;

/// <summary>
/// Portas de persistência do módulo Tenants (implementadas em Infrastructure).
/// O filtro de tenant vem dos Global Query Filters — GetById de outro tenant
/// devolve null (R-06).
/// </summary>
public interface ITenantRepository
{
    Task<Tenant?> GetByIdAsync(TenantId id, CancellationToken cancellationToken = default);

    Task<Tenant?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);

    Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Tenant>> ListAllActiveAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default);
}

public interface ICompanyRepository
{
    Task<Company?> GetByTenantAndIdAsync(TenantId tenantId, CompanyId id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Company>> ListByTenantAsync(TenantId tenantId, CancellationToken cancellationToken = default);

    Task<bool> DocumentExistsAsync(Document document, CancellationToken cancellationToken = default);

    Task AddAsync(Company company, CancellationToken cancellationToken = default);
}

public interface ITenantMembershipRepository
{
    Task<TenantMembership?> FindAsync(TenantId tenantId, Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TenantMembership>> ListByTenantAsync(TenantId tenantId, CancellationToken cancellationToken = default);

    Task AddAsync(TenantMembership membership, CancellationToken cancellationToken = default);
}

public interface ITenantSettingsRepository
{
    Task<TenantSettings?> GetForTenantAsync(TenantId tenantId, CancellationToken cancellationToken = default);

    Task AddAsync(TenantSettings settings, CancellationToken cancellationToken = default);
}
