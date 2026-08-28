namespace Worfair.BuildingBlocks.Infrastructure.Persistence.Tenant;

using Worfair.BuildingBlocks.Domain.Tenancy;

/// <summary>
/// Define <c>app.tenant_id</c> POR CONEXÃO para o PostgreSQL Row-Level Security
/// (docs/architecture/04 §4). Executa sempre no ConnectionOpened:
///  - com tenant  → set_config(valor);
///  - sem tenant   → set_config(NULL) ⇒ deny-by-default (R-02) e limpa qualquer
///    valor residual de conexões do pool.
/// </summary>
public sealed class TenantConnectionInterceptor(ITenantProvider tenantProvider) : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) =>
        SetTenantSetting(connection, tenantProvider.TenantId?.Value);

    public override Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        SetTenantSetting(connection, tenantProvider.TenantId?.Value);
        return Task.CompletedTask;
    }

    private static void SetTenantSetting(DbConnection connection, Guid? tenantId)
    {
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT set_config('app.tenant_id', @tenant_id, false);";

            var parameter = command.CreateParameter();
            parameter.ParameterName = "@tenant_id";
            parameter.Value = (object?)tenantId?.ToString() ?? DBNull.Value;
            command.Parameters.Add(parameter);

            command.ExecuteNonQuery();
        }
        catch (ObjectDisposedException)
        {
            // Shutdown em andamento — seguro ignorar.
        }
    }
}
