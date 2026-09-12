namespace Worfair.Api.Endpoints;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Worfair.Api.Extensions;
using Worfair.Modules.Identity.Application.GetMe;
using Worfair.Modules.Identity.Application.Login;
using Worfair.Modules.Identity.Application.Logout;
using Worfair.Modules.Identity.Application.ManageRoles;
using Worfair.Modules.Identity.Application.RefreshToken;
using Worfair.Modules.Identity.Application.RegisterUser;
using Worfair.Modules.Identity.Application.SwitchTenant;
using Worfair.Modules.Identity.Application.LockUser;

public static class IdentityEndpoints
{
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/identity").WithTags("Identity");

        // Público
        group.MapPost("/register", async (RegisterUserCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct).ConfigureAwait(false);
            return result.IsFailure
                ? result.ToHttpResult()
                : Results.Created($"/api/identity/{result.Value}", result.Value);
        })
            .WithName("RegisterUser")
            .WithSummary("Auto-cadastro público. Usuário criado sem papel; onboarding necessário.");

        group.MapPost("/login", async (LoginCommand command, ISender sender, CancellationToken ct) =>
            (await sender.Send(command, ct).ConfigureAwait(false)).ToHttpResult())
            .WithName("Login")
            .WithSummary("Emite token RS256 (15min) + refresh (7d). TargetTenantId validado no servidor.");

        group.MapPost("/refresh", async (RefreshAccessTokenCommand command, ISender sender, CancellationToken ct) =>
            (await sender.Send(command, ct).ConfigureAwait(false)).ToHttpResult())
            .WithName("RefreshTokens")
            .WithSummary("Rotação única; reuso invalida a token family.");

        group.MapPost("/logout", async (LogoutCommand command, ISender sender, CancellationToken ct) =>
            (await sender.Send(command, ct).ConfigureAwait(false)).ToAcceptedResult())
            .WithName("Logout")
            .RequireAuthorization();

        // Autenticados
        group.MapGet("/me", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetMeQuery(), ct).ConfigureAwait(false)).ToHttpResult())
            .WithName("GetMe")
            .RequireAuthorization()
            .WithSummary("Contexto: roles, permissões e modos lidos do banco.");

        group.MapPost("/switch-tenant", async (SwitchTenantCommand command, ISender sender, CancellationToken ct) =>
            (await sender.Send(command, ct).ConfigureAwait(false)).ToHttpResult())
            .WithName("SwitchTenant")
            .RequireAuthorization()
            .WithSummary("Troca de contexto valida membership e deriva modo automaticamente.");

        // Gestão de roles de tenant (tenants.members.manage + modo Contratante)
        var usersGroup = group.MapGroup("/users/{targetUserId:guid}/roles")
            .RequireAuthorization(Worfair.Api.Authorization.SecurityPolicies.MembersManage);

        usersGroup.MapPost(string.Empty,
                async (Guid targetUserId, AssignRoleRequest request, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new AssignTenantRoleCommand(targetUserId, request.RoleCode), ct)
                        .ConfigureAwait(false)).ToAcceptedResult())
            .WithName("AssignTenantRole");

        usersGroup.MapDelete("/{roleCode}",
                async (Guid targetUserId, string roleCode, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new RemoveTenantRoleCommand(targetUserId, roleCode), ct)
                        .ConfigureAwait(false)).ToAcceptedResult())
            .WithName("RemoveTenantRole");

        // Gestão de bloqueio/desbloqueio (apenas SUPER_ADMIN global)
        var adminGroup = group.MapGroup("/users/{targetUserId:guid}/lock")
            .RequireAuthorization(Worfair.Api.Authorization.SecurityPolicies.DisputeAdmin);

        adminGroup.MapPost("",
                async (Guid targetUserId, LockUserCommand command, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new LockUserCommand(targetUserId), ct)
                        .ConfigureAwait(false)).ToAcceptedResult())
            .WithName("LockUser");

        adminGroup.MapPost("/unlock",
                async (Guid targetUserId, UnlockUserCommand command, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new UnlockUserCommand(targetUserId), ct)
                        .ConfigureAwait(false)).ToAcceptedResult())
            .WithName("UnlockUser");

        return app;
    }
}

public sealed record AssignRoleRequest(string RoleCode);
