namespace Worfair.BuildingBlocks.Application.Security;

/// <summary>
/// Modos operacionais (SEC-03): NÃO são roles — são contextos derivados das
/// permissões efetivas. Um token carrega UM modo por vez.
/// </summary>
public enum AccessMode
{
    Global = 0,
    Contracting = 1,
    Provider = 2,

    /// <summary>
    /// Conta nova sem contexto (sem membership): token válido só para
    /// onboarding (/me, /tenants/bootstrap). Nenhuma policy exige este modo.
    /// </summary>
    None = 3
}
