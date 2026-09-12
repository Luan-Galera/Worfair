namespace Worfair.Modules.Identity.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Worfair.BuildingBlocks.Domain.ValueObjects;
using Worfair.Modules.Identity.Application.Abstractions;

/// <summary>
/// Implementação do escopo de escrita (ver IIdentityWriteScope): transação
/// explícita + set_config local; a ação inclui o SaveChanges (que entra na
/// transação ambiente) e o commit fecha o escopo.
/// </summary>
public sealed class IdentityWriteScope(IdentityDbContext db) : IIdentityWriteScope
{
    public async Task<T> ExecuteAsync<T>(
        TenantId? tenantId, Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await db.Database.ExecuteSqlRawAsync(
            "SELECT set_config('app.tenant_id', {0}, true)",
            [tenantId?.Value.ToString() ?? string.Empty],
            cancellationToken).ConfigureAwait(false);

        var result = await action().ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return result;
    }

    public async Task ExecuteAsync(
        TenantId? tenantId, Func<Task> action, CancellationToken cancellationToken = default)
    {
        await ExecuteAsync<object?>(tenantId, async () =>
        {
            await action().ConfigureAwait(false);
            return null;
        }, cancellationToken).ConfigureAwait(false);
    }
}
