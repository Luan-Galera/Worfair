namespace Worfair.Api.Endpoints;

using MediatR;
using Microsoft.EntityFrameworkCore;
using Worfair.Api.Authorization;
using Worfair.Api.Extensions;
using Worfair.Modules.Tenants.Application.CreateCompany;
using Worfair.Modules.Tenants.Application.Members;
using Worfair.Modules.Tenants.Application.ProvisionTenant;
using Worfair.Modules.Tenants.Application.Queries;
using Worfair.Modules.Identity.Application.ManageRoles;
using Worfair.Modules.Tenants.Application.SetCompanyStatus;
using Worfair.Modules.Tenants.Application.TransferCompanyOwner;

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

        platform.MapGet(string.Empty, async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new ListTenantsQuery(), ct).ConfigureAwait(false)).ToHttpResult())
            .WithName("ListTenants")
            .WithSummary("Lista tenants ativos para o painel do SUPER_ADMIN.");

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

        companies.MapPatch("/{companyId:guid}/owner",
                async (Guid companyId, TransferOwnerRequest request, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new TransferCompanyOwnerCommand(companyId, request.NewOwnerUserId), ct)
                        .ConfigureAwait(false)).ToHttpResult())
            .RequireAuthorization(SecurityPolicies.MembersManage)
            .WithName("TransferCompanyOwner")
            .WithSummary("Transfere o vínculo dono×empresa para outro membro ativo do espaço.");

        var members = app.MapGroup("/api/tenants/members").WithTags("Tenants · Members");

        members.MapPost(string.Empty,
                async (AddTenantMemberCommand command, ISender sender, CancellationToken ct) =>
                    (await sender.Send(command, ct).ConfigureAwait(false)).ToAcceptedResult())
            .RequireAuthorization(SecurityPolicies.MembersManage)
            .WithName("AddTenantMember");

        // Convite por e-mail (resolve o usuário globalmente e adiciona ao espaço).
        members.MapPost("/by-email",
                async (AddMemberByEmailRequest request, ISender sender,
                    Worfair.Modules.Identity.Infrastructure.Persistence.IdentityDbContext identity,
                    CancellationToken ct) =>
                {
                    if (string.IsNullOrWhiteSpace(request.Email))
                        return Results.BadRequest(new { message = "Informe o e-mail." });
                    var emailResult = Worfair.Modules.Identity.Domain.ValueObjects.Email
                        .Create(request.Email.Trim());
                    if (emailResult.IsFailure)
                        return Results.BadRequest(new { message = "E-mail inválido." });
                    var target = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                        .FirstOrDefaultAsync(identity.Users.Where(u => u.Email == emailResult.Value), ct)
                        .ConfigureAwait(false);
                    if (target is null)
                        return Results.NotFound(new { message = "Usuário não encontrado." });
                    var result = await sender.Send(
                        new AddTenantMemberCommand(target.Id), ct).ConfigureAwait(false);
                    return result.IsFailure
                        ? result.ToAcceptedResult()
                        : Results.Ok(new { userId = target.Id, email = target.Email.Value });
                })
            .RequireAuthorization(SecurityPolicies.MembersManage)
            .WithName("AddTenantMemberByEmail")
            .WithSummary("Convida usuário ao espaço pelo e-mail.");

        members.MapGet(string.Empty,
                async (ISender sender,
                    Worfair.Modules.Identity.Infrastructure.Persistence.IdentityDbContext identity,
                    Worfair.BuildingBlocks.Application.Security.ICurrentUser currentUser,
                    CancellationToken ct) =>
                {
                    var result = await sender.Send(new ListTenantMembersQuery(), ct).ConfigureAwait(false);
                    if (result.IsFailure)
                        return result.ToHttpResult();

                    // Enriquece com nome/e-mail (identity.users é global, legível no
                    // contexto do espaço) para a UI não exibir o Guid cru.
                    var ids = result.Value!.Select(m => m.UserId).ToList();
                    var users = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                        .ToListAsync(identity.Users.Where(u => ids.Contains(u.Id)), ct)
                        .ConfigureAwait(false);
                    var byId = users.ToDictionary(u => u.Id, u => u);

                    // Cargos atuais no espaço (user_roles do tenant + catálogo global
                    // de roles, mesmo padrão de leitura do EffectivePermissionReader).
                    var rolesByUser = new Dictionary<Guid, IReadOnlyList<string>>();
                    if (currentUser.TenantId is { } tid)
                    {
                        var pairs = await identity.UserRoles
                            .Where(ur => ur.TenantId == tid)
                            .Join(identity.Roles,
                                ur => ur.RoleIdValue, r => r.Id,
                                (ur, r) => new { ur.UserId, r.Code })
                            .ToListAsync(ct).ConfigureAwait(false);
                        foreach (var g in pairs.GroupBy(p => p.UserId))
                            rolesByUser[g.Key] = g.Select(p => p.Code).Distinct().ToList();
                    }

                    var enriched = result.Value!.Select(m =>
                    {
                        byId.TryGetValue(m.UserId, out var u);
                        rolesByUser.TryGetValue(m.UserId, out var codes);
                        return m with
                        {
                            Email = u?.Email.Value,
                            FullName = u?.FullName,
                            RoleCodes = codes,
                        };
                    }).ToList();
                    return Results.Ok(enriched);
                })
            .RequireAuthorization(SecurityPolicies.MembersManage)
            .WithName("ListTenantMembers");

        members.MapPatch("/{targetUserId:guid}/status",
                async (Guid targetUserId, SetStatusRequest request, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new SetTenantMemberStatusCommand(targetUserId, request.Active), ct)
                        .ConfigureAwait(false)).ToAcceptedResult())
            .RequireAuthorization(SecurityPolicies.MembersManage)
            .WithName("SetTenantMemberStatus");

        // Transferência da propriedade: só o dono atual (conferido no handler).
        members.MapPost("/ownership/transfer",
                async (TransferOwnershipRequest request, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new TransferSpaceOwnershipCommand(request.NewOwnerUserId), ct)
                        .ConfigureAwait(false)).ToAcceptedResult())
            .RequireAuthorization(SecurityPolicies.MembersManage)
            .WithName("TransferSpaceOwnership")
            .WithSummary("Concede OWNER ao novo dono e revoga do atual na mesma operação.");

        // ── Onboarding self-service (conta nova sem contexto) ─────────────────
        // Cria o espaço pessoal do usuário + membership + roles OWNER/PROVIDER.
        // Exige apenas autenticação (sem escopo de tenant): resolve o
        // ModeUnavailable de contas recém-registradas sem depender do admin.
        app.MapPost("/api/tenants/bootstrap", async (
                BootstrapTenantRequest request,
                Worfair.Modules.Tenants.Infrastructure.Persistence.TenancyDbContext tenancy,
                Worfair.Modules.Identity.Infrastructure.Persistence.IdentityDbContext identity,
                Worfair.Modules.Tenants.Contracts.ITenancyReadContract tenancyRead,
                Worfair.BuildingBlocks.Application.Security.ICurrentUser user,
                CancellationToken ct) =>
            await BootstrapTenantHandler.HandleAsync(request, tenancy, identity, tenancyRead, user, ct)
                .ConfigureAwait(false))
            .RequireAuthorization()
            .WithName("BootstrapTenant")
            .WithSummary("Onboarding: cria o tenant pessoal de quem ainda não tem membership.")
            .WithTags("Tenants · Onboarding");

        return app;
    }
}

public sealed record BootstrapTenantRequest(string? WorkspaceName);

public sealed record BootstrapTenantResponse(Guid TenantId, string Slug, string ModeHint);

internal static class BootstrapTenantHandler
{
    private static readonly System.Text.RegularExpressions.Regex NonSlugChars =
        new("[^a-z0-9]+", System.Text.RegularExpressions.RegexOptions.Compiled);

    public static async Task<IResult> HandleAsync(
        BootstrapTenantRequest request,
        Worfair.Modules.Tenants.Infrastructure.Persistence.TenancyDbContext tenancy,
        Worfair.Modules.Identity.Infrastructure.Persistence.IdentityDbContext identity,
        Worfair.Modules.Tenants.Contracts.ITenancyReadContract tenancyRead,
        Worfair.BuildingBlocks.Application.Security.ICurrentUser user,
        CancellationToken ct)
    {
        if (user.UserId is not { } userId)
            return Results.Unauthorized();

        var existing = await tenancyRead
            .ListActiveMembershipsAcrossTenantsAsync(userId, ct)
            .ConfigureAwait(false);
        if (existing.Count != 0)
            return Results.Conflict(new { message = "Usuário já possui um espaço ativo. Use switch-tenant." });

        var displayName = string.IsNullOrWhiteSpace(request.WorkspaceName)
            ? "Meu espaço"
            : request.WorkspaceName.Trim();

        var slug = await UniqueSlugAsync(displayName, tenancy, ct).ConfigureAwait(false);

        var createResult = Worfair.Modules.Tenants.Domain.Aggregates.Tenant.Tenant.Create(
            displayName, slug,
            Worfair.Modules.Tenants.Domain.Aggregates.Tenant.TenantTier.Standard,
            "America/Sao_Paulo", "pt-BR");
        if (createResult.IsFailure)
            return Results.BadRequest(new { message = createResult.Error!.Message });

        var tenant = createResult.Value;

        var membershipResult = Worfair.Modules.Tenants.Domain.Aggregates.Membership.TenantMembership.Add(
            userId, tenant.Key);
        if (membershipResult.IsFailure || membershipResult.Value.Status
                != Worfair.Modules.Tenants.Domain.Aggregates.Membership.MembershipStatus.Active)
            return Results.BadRequest(new { message = "Não foi possível criar o vínculo com o espaço." });

        // Escrita elevada (R-04): a conexão chega com app.tenant_id vazio (sem
        // contexto) e o RLS exigiria igualdade — fixa o novo tenant no escopo
        // da TRANSAÇÃO (set_config local, revertido no commit/rollback).
        await using (var tx = await tenancy.Database.BeginTransactionAsync(ct).ConfigureAwait(false))
        {
            await tenancy.Database.ExecuteSqlRawAsync(
                "SELECT set_config('app.tenant_id', {0}, true)",
                [tenant.Key.Value.ToString()], ct).ConfigureAwait(false);

            await tenancy.Tenants.AddAsync(tenant, ct).ConfigureAwait(false);
            await tenancy.TenantSettings.AddAsync(
                Worfair.Modules.Tenants.Domain.Aggregates.Settings.TenantSettings.DefaultFor(tenant.Key), ct)
                .ConfigureAwait(false);
            await tenancy.TenantMemberships.AddAsync(membershipResult.Value, ct).ConfigureAwait(false);
            await tenancy.SaveChangesAsync(ct).ConfigureAwait(false);
            await tx.CommitAsync(ct).ConfigureAwait(false);
        }

        var roleCodes = new[]
        {
            Worfair.Modules.Identity.Domain.Aggregates.Role.RoleCodes.Owner,
            Worfair.Modules.Identity.Domain.Aggregates.Role.RoleCodes.Provider,
        };
        var roles = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            identity.Roles.Where(r =>
                !r.IsGlobal && roleCodes.Contains(r.Code)), ct).ConfigureAwait(false);

        if (roles.Count != roleCodes.Length)
            return Results.Problem("Papéis padrão do espaço não encontrados.", statusCode: 503);

        await using (var tx = await identity.Database.BeginTransactionAsync(ct).ConfigureAwait(false))
        {
            await identity.Database.ExecuteSqlRawAsync(
                "SELECT set_config('app.tenant_id', {0}, true)",
                [tenant.Key.Value.ToString()], ct).ConfigureAwait(false);

            foreach (var role in roles)
            {
                var grant = Worfair.Modules.Identity.Domain.Aggregates.UserRole.UserRole.Grant(
                    userId, role, tenant.Key, grantedBy: userId);
                if (grant.IsFailure)
                    return Results.BadRequest(new { message = grant.Error!.Message });
                await identity.UserRoles.AddAsync(grant.Value, ct).ConfigureAwait(false);
            }

            await identity.SaveChangesAsync(ct).ConfigureAwait(false);
            await tx.CommitAsync(ct).ConfigureAwait(false);
        }

        return Results.Created($"/api/platform/tenants/{tenant.Id}",
            new BootstrapTenantResponse(tenant.Id, tenant.Slug,
                "Espaço criado com papéis de contratante e prestador. Chame switch-tenant para entrar."));
    }

    private static async Task<string> UniqueSlugAsync(
        string displayName,
        Worfair.Modules.Tenants.Infrastructure.Persistence.TenancyDbContext tenancy,
        CancellationToken ct)
    {
        var baseSlug = NonSlugChars.Replace(displayName.Trim().ToLowerInvariant(), "-").Trim('-');
        if (baseSlug.Length > 60)
            baseSlug = baseSlug[..60].Trim('-');
        if (string.IsNullOrWhiteSpace(baseSlug))
            baseSlug = "espaco";

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var suffix = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(3))
                .ToLowerInvariant();
            var candidate = $"{baseSlug}-{suffix}";
            var exists = await tenancy.Tenants.AsNoTracking()
                .AnyAsync(t => t.Slug == candidate, ct).ConfigureAwait(false);
            if (!exists)
                return candidate;
        }

        return $"{baseSlug}-{Guid.NewGuid():N}"[..80];
    }
}

public sealed record SetStatusRequest(bool Active);

public sealed record TransferOwnerRequest(Guid NewOwnerUserId);

public sealed record AddMemberByEmailRequest(string Email);

public sealed record TransferOwnershipRequest(Guid NewOwnerUserId);