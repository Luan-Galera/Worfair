namespace Worfair.Api.Endpoints;

using MediatR;
using Microsoft.AspNetCore.Builder;
using Worfair.Api.Extensions;
using Worfair.Modules.Identity.Application.Dtos;
using Worfair.Modules.Identity.Application.GetMe;
using Worfair.Modules.Identity.Application.Login;
using Worfair.Modules.Identity.Application.Logout;
using Worfair.Modules.Identity.Application.ManageRoles;
using Worfair.Modules.Identity.Application.RefreshToken;
using Worfair.Modules.Identity.Application.RegisterUser;
using Worfair.Modules.Identity.Application.SwitchMode;
using Worfair.Modules.Identity.Application.SwitchTenant;

public static class IdentityEndpoints
{
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/identity").WithTags("Identity");

        // Público
        group.MapPost("/register", async (RegisterUserCommand command, ISender sender, CancellationToken ct) =>
            (await sender.Send(command, ct).ConfigureAwait(false)).Created("GetUserById", id => new { id }))
            .WithName("RegisterUser")
            .WithSummary("Auto-cadastro público. Primeiro usuário recebe SUPER_ADMIN.");

        group.MapPost("/login", async (LoginCommand command, ISender sender, CancellationToken ct) =>
            (await sender.Send(command, ct).ConfigureAwait(false)).ToHttpResult())
            .WithName("Login")
            .WithSummary("Emite access token RS256 (15min) + refresh rotativo (7d). TargetTenantId opcional e VALIDADO no servidor.");

        group.MapPost("/refresh", async (RefreshAccessTokenCommand command, ISender sender, CancellationToken ct) =>
            (await sender.Send(command, ct).ConfigureAwait(false)).ToHttpResult())
            .WithName("RefreshTokens")
            .WithSummary("Rotação com uso único; reuso revoga a família inteira.");

        group.MapPost("/logout", async (LogoutCommand command, ISender sender, CancellationToken ct) =>
            (await sender.Send(command, ct).ConfigureAwait(false)).ToAcceptedResult())
            .WithName("Logout")
            .RequireAuthorization();

        // Autenticados
        group.MapGet("/me", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetMeQuery(), ct).ConfigureAwait(false)).ToHttpResult())
            .WithName("GetMe")
            .RequireAuthorization()
            .WithSummary("Espelho do contexto: roles/permissões efetivas/modos disponíveis lidos do BANCO.");

        group.MapPost("/switch-tenant", async (SwitchTenantCommand command, ISender sender, CancellationToken ct) =>
            (await sender.Send(command, ct).ConfigureAwait(false)).ToHttpResult())
            .WithName("SwitchTenant")
            .RequireAuthorization()
            .WithSummary("ÚNICA via de troca de tenant — valida membership ativa e emite novo token.");

        group.MapPost("/switch-mode", async (SwitchModeCommand command, ISender sender, CancellationToken ct) =>
            (await sender.Send(command, ct).ConfigureAwait(false)).ToHttpResult())
            .WithName("SwitchMode")
            .RequireAuthorization()
            .WithSummary("Alternância contracting/provider/global — reemite token se o modo estiver disponível.");

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

        return app;
    }
}

public sealed record AssignRoleRequest(string RoleCode);
