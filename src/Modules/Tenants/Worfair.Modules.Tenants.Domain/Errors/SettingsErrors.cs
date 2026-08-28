namespace Worfair.Modules.Tenants.Domain.Errors;

using Worfair.BuildingBlocks.Domain.Errors;

public static class SettingsErrors
{
    public static readonly Error InvalidJson =
        new("TenantSettings.InvalidJson", "O conteúdo informado não é um JSON válido.");

    public static readonly Error NotFound =
        new("TenantSettings.NotFound", "Configurações do tenant não encontradas.");
}
