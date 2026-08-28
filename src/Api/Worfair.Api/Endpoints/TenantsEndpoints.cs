namespace Worfair.Api.Endpoints;

using MediatR;
using Worfair.Api.Authorization;
using Worfair.Api.Extensions;
using Worfair.Modules.Tenants.Application.CreateCompany;
using Worfair.Modules.Tenants.Application.Members;
using Worfair.Modules.Tenants.Application.ProvisionTenant;
using Worfair.Modules.Tenants.Application.Queries;
using Worfair.Modules.Tenants.Application.SetCompanyStatus;

public static class TenantsEndpoints
{
    public static IEndpointRouteBuilder MapTenantsEndpoints(this IEndpointRouteBuilder app)
    {
        // ── Plataforma (contexto GLOBAL — SUPER_ADMIN) ──────────────────────
        var platform = app.MapGroup("/api/platform/tenants").WithTags("Platform · Tenants");
        platform.RequireAuthorization(SecurityPolicies.PlatformManageTenants);

        platform.MapPost(string.Empty,
                async (ProvisionTenantCommand command, ISender sender, CancellationToken ct) =>
                    (await sender.Send(command, ct).ConfigureAwait(false)).ToHttpResult())
            .WithName("ProvisionTenant")
            .WithSummary("Cria tenant + settings + owner opcional. Publica TenantProvisioned via Outbox.");

        platform.MapGet("/{tenantId:guid}", async (Guid tenantId, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetTenantByIdQuery(tenantId), ct).ConfigureAwait(false)).ToHttpResult())
            .WithName("GetTenantById");

        // ── Dentro do tenant corrente ───────────────────────────────────────
        var companies = app.MapGroup("/api/tenants/companies").WithTags("Tenants · Companies");

        companies.MapPost(string.Empty,
                async (CreateCompanyCommand command, ISender sender, CancellationToken ct) =>
                    (await sender.Send(command, ct).ConfigureAwait(false)).ToHttpResult())
            .RequireAuthorization(SecurityPolicies.SettingsRead)
            .WithName("CreateCompany");

        companies.MapGet(string.Empty,
                async (ISender sender, CancellationToken ct) =>
                    (await sender.Send(new ListCompaniesQuery(), ct).ConfigureAwait(false)).ToHttpResult())
            .RequireAuthorization(SecurityPolicies.SettingsRead)
            .WithName("ListCompanies");

        companies.MapPatch("/{companyId:guid}/status",
                async (Guid companyId, SetStatusRequest request, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new SetCompanyStatusCommand(companyId, request.Active), ct)
                        .ConfigureAwait(false)).ToAcceptedResult())
            .RequireAuthorization(SecurityPolicies.SettingsRead)
            .WithName("SetCompanyStatus");

        var members = app.MapGroup("/api/tenants/members").WithTags("Tenants · Members");

        members.MapPost(string.Empty,
                async (AddTenantMemberCommand command, ISender sender, CancellationToken ct) =>
                    (await sender.Send(command, ct).ConfigureAwait(false)).ToAcceptedResult())
            .RequireAuthorization(SecurityPolicies.MembersManage)
            .WithName("AddTenantMember");

        members.MapGet(string.Empty,
                async (ISender sender, CancellationToken ct) =>
                    (await sender.Send(new ListTenantMembersQuery(), ct).ConfigureAwait(false)).ToHttpResult())
            .RequireAuthorization(SecurityPolicies.MembersManage)
            .WithName("ListTenantMembers");

        members.MapPatch("/{targetUserId:guid}/status",
                async (Guid targetUserId, SetStatusRequest request, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new SetTenantMemberStatusCommand(targetUserId, request.Active), ct)
                        .ConfigureAwait(false)).ToAcceptedResult())
            .RequireAuthorization(SecurityPolicies.MembersManage)
            .WithName("SetTenantMemberStatus");

        return app;
    }
}

public sealed record SetStatusRequest(bool Active);
