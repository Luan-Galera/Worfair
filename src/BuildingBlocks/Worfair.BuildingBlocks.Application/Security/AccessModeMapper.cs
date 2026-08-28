namespace Worfair.BuildingBlocks.Application.Security;

/// <summary>
/// Mapeamento estático modo → prefixos de permissão (única fonte da verdade,
/// docs/security/03 §3).
/// </summary>
public static class AccessModeMapper
{
    public static readonly IReadOnlySet<string> ContractingPermissions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "tenants.settings", "tenants.members", "recruitment.", "jobs.",
            "proposals.decide", "proposals.offer", "financial.invoice"
        };

    public static readonly IReadOnlySet<string> ProviderPermissions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "proposals.submit", "jobs.project.apply", "financial.payout", "contracts.deliver"
        };

    public static AccessMode? Derive(IEnumerable<string> permissions)
    {
        var hasContracting = permissions.Any(p => ContractingPermissions.Any(p.StartsWith));
        var hasProvider = permissions.Any(p => ProviderPermissions.Any(p.StartsWith));
        return (hasContracting, hasProvider) switch
        {
            (true, true) => throw new InvalidOperationException("Modos exclusivos: um contexto de token define um único modo."),
            (true, false) => AccessMode.Contracting,
            (false, true) => AccessMode.Provider,
            _ => null
        };
    }
}
