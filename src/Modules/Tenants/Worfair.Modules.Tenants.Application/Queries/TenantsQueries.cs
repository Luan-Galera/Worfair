namespace Worfair.Modules.Tenants.Application.Queries;

using Worfair.BuildingBlocks.Application.Cqrs;
using Worfair.Modules.Tenants.Application.Dtos;

public sealed record GetTenantByIdQuery(Guid TenantId) : IQuery<Result<TenantDto>>;

public sealed class GetTenantByIdQueryHandler(ITenantRepository tenants)
    : IQueryHandler<GetTenantByIdQuery, Result<TenantDto>>
{
    public async Task<Result<TenantDto>> Handle(GetTenantByIdQuery query, CancellationToken cancellationToken)
    {
        var tenant = await tenants
            .GetByIdAsync(new TenantId(query.TenantId), cancellationToken)
            .ConfigureAwait(false);

        if (tenant is null)
            return Result.Failure<TenantDto>(TenantErrors.NotFound);

        return new TenantDto(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            (int)tenant.Tier,
            (int)tenant.Status,
            tenant.Timezone,
            tenant.Locale,
            tenant.CreatedAtUtc);
    }
}

/// <summary>Empresas do tenant CORRENTE — contexto vem do token (R-06).</summary>
public sealed record ListCompaniesQuery : IQuery<Result<IReadOnlyList<CompanyDto>>>;

public sealed class ListCompaniesQueryHandler(
    ICompanyRepository companies,
    ITenantProvider tenantProvider)
    : IQueryHandler<ListCompaniesQuery, Result<IReadOnlyList<CompanyDto>>>
{
    public async Task<Result<IReadOnlyList<CompanyDto>>> Handle(
        ListCompaniesQuery query, CancellationToken cancellationToken)
    {
        if (tenantProvider.TenantId is not { } tenantId)
            return Result.Failure<IReadOnlyList<CompanyDto>>(CompanyErrors.TenantRequired);

        var list = await companies.ListByTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);

        return Result.Success<IReadOnlyList<CompanyDto>>(list.Select(c => new CompanyDto(
            c.Id.Value,
            c.LegalName,
            c.TradeName,
            c.Document.Value,
            c.Email,
            c.Phone,
            (int)c.Status)).ToList());
    }
}

public sealed record ListTenantMembersQuery : IQuery<Result<IReadOnlyList<MemberDto>>>;

public sealed class ListTenantMembersQueryHandler(
    ITenantMembershipRepository memberships,
    ITenantProvider tenantProvider)
    : IQueryHandler<ListTenantMembersQuery, Result<IReadOnlyList<MemberDto>>>
{
    public async Task<Result<IReadOnlyList<MemberDto>>> Handle(
        ListTenantMembersQuery query, CancellationToken cancellationToken)
    {
        if (tenantProvider.TenantId is not { } tenantId)
            return Result.Failure<IReadOnlyList<MemberDto>>(MembershipErrors.TenantRequired);

        var list = await memberships.ListByTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);

        return Result.Success<IReadOnlyList<MemberDto>>(
            list.Select(m => new MemberDto(m.UserId, (int)m.Status, m.JoinedAtUtc)).ToList());
    }
}
