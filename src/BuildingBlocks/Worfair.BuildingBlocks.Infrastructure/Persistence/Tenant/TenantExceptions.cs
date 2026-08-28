namespace Worfair.BuildingBlocks.Infrastructure.Persistence.Tenant;

/// <summary>Entidade tenant-owned sem tenant ativo no contexto (deny-by-default).</summary>
public sealed class TenantRequiredException(string message)
    : InvalidOperationException(message);

/// <summary>Tentativa de ler/escrever/excluir linha pertencente a outro tenant.</summary>
public sealed class TenantMismatchException(string message)
    : SecurityException(message);
