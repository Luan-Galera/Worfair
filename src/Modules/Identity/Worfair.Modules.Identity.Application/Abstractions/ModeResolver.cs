namespace Worfair.Modules.Identity.Application.Abstractions;

using Worfair.BuildingBlocks.Application.Security;

/// <summary>
/// Derivação de modo operacional (SEC-03): modo NÃO é role — é derivado das
/// permissões efetivas do CONTEXTO. SUPER_ADMIN sozinho ⇒ Global.
/// </summary>
public static class ModeResolver
{
    public const string ContractingMode = "contracting";
    public const string ProviderMode = "provider";
    public const string GlobalMode = "global";
    public const string NoneMode = "none";

    public static bool HasContracting(IEnumerable<string> permissions) =>
        permissions.Any(p => AccessModeMapper.ContractingPermissions.Any(p.StartsWith));

    public static bool HasProvider(IEnumerable<string> permissions) =>
        permissions.Any(p => AccessModeMapper.ProviderPermissions.Any(p.StartsWith));

    public static bool HasGlobal(IEnumerable<string> roleCodes) =>
        roleCodes.Contains("SUPER_ADMIN", StringComparer.Ordinal);

    /// <summary>Modo do CONTEXTO corrente (um único modo por token).</summary>
    public static Result<AccessMode> DeriveContextMode(
        IReadOnlyList<string> effectivePermissions, IReadOnlyList<string> roleCodes)
    {
        var contracting = HasContracting(effectivePermissions);
        var provider = HasProvider(effectivePermissions);
        var global = HasGlobal(roleCodes);

        return (contracting, provider) switch
        {
            // Um contexto pode exercer as duas capacidades; o modo é apenas o padrão da sessão.
            (true, _) => Result.Success(AccessMode.Contracting),
            (false, true) => Result.Success(AccessMode.Provider),
            _ => global
                ? Result.Success(AccessMode.Global)
                : Result.Failure<AccessMode>(AuthErrors.ModeUnavailable)
        };
    }

    public static string ToClaimValue(AccessMode mode) => mode switch
    {
        AccessMode.Contracting => ContractingMode,
        AccessMode.Provider => ProviderMode,
        AccessMode.None => NoneMode,
        _ => GlobalMode
    };

    public static bool TryParse(string? value, out AccessMode mode)
    {
        switch (value?.ToLowerInvariant())
        {
            case ContractingMode: mode = AccessMode.Contracting; return true;
            case ProviderMode: mode = AccessMode.Provider; return true;
            case GlobalMode: mode = AccessMode.Global; return true;
            case NoneMode: mode = AccessMode.None; return true;
            default: mode = AccessMode.Global; return false;
        }
    }
}
