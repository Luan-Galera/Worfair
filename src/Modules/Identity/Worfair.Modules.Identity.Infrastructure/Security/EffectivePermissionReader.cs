namespace Worfair.Modules.Identity.Infrastructure.Security;

using Microsoft.EntityFrameworkCore;
using Worfair.Modules.Identity.Application.Abstractions;
using Worfair.Modules.Identity.Domain.Abstractions;
using Worfair.Modules.Identity.Domain.ValueObjects;
using Worfair.Modules.Identity.Infrastructure.Persistence;

/// <summary>
/// Roles atuais + permissões efetivas = união das roles (R-05) — lidos do banco
/// a cada uso. Em contexto global lê apenas linhas tenant_id NULL.
/// </summary>
public sealed class EffectivePermissionReader(IdentityDbContext db) : IEffectivePermissionReader
{
    public Task<IReadOnlyList<string>> GetRoleCodesAsync(
        Guid userId, TenantId? tenantId, CancellationToken cancellationToken = default)
        => ReadAsync(
            userId, tenantId, cancellationToken,
            userRoles => userRoles.Join(db.Roles,
                    ur => ur.RoleIdValue, r => r.Id, (ur, r) => r.Code));

    public Task<IReadOnlyList<string>> GetEffectivePermissionsAsync(
        Guid userId, TenantId? tenantId, CancellationToken cancellationToken = default)
        => ReadAsync(
            userId, tenantId, cancellationToken,
            userRoles => userRoles
                .Join(db.RolePermissions, ur => ur.RoleIdValue, rp => rp.RoleId, (ur, rp) => rp.PermissionId)
                .Join(db.Permissions, permissionId => permissionId, p => p.Id, (_, p) => p.Code));

    private async Task<IReadOnlyList<string>> ReadAsync(
        Guid userId, TenantId? tenantId,
        CancellationToken cancellationToken,
        Func<IQueryable<Worfair.Modules.Identity.Domain.Aggregates.UserRole.UserRole>, IQueryable<string>> project)
    {
        // Espelha o SQL de resolução dos docs (database/03 §4): contexto de
        // tenant lê apenas linhas do tenant; contexto global apenas tenant_id NULL.
        // O RLS do banco impõe o mesmo recorte (defesa final).
        //
        // Leitura ELEVADA (R-04, mesmo padrão do TenancyReadContract): o RLS de
        // user_roles filtra por app.tenant_id da SESSÃO, que nem sempre é o tenant
        // alvo (login/refresh/switch partem de contexto nulo ou de outro tenant).
        // Fixa o tenant alvo no escopo da TRANSAÇÃO e reverte ao final — leitura
        // apenas, nunca escrita; sem isso, roles recém-criadas ficam invisíveis.
        if (tenantId is not { } tid)
        {
            var query = db.UserRoles.Where(ur =>
                ur.UserId == userId && ur.TenantId == null);

            return await project(query)
                .Distinct()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        await using var transaction =
            await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await db.Database.ExecuteSqlRawAsync(
            "SELECT set_config('app.tenant_id', {0}, true)",
            [tid.Value.ToString()],
            cancellationToken).ConfigureAwait(false);

        var scoped = db.UserRoles.Where(ur =>
            ur.UserId == userId && ur.TenantId == tid.Value);

        var result = await project(scoped)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);

        return result;
    }
}
