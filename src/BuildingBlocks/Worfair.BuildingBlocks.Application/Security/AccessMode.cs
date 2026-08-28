namespace Worfair.BuildingBlocks.Application.Security;

/// <summary>
/// Modos operacionais (SEC-03): NÃO são roles — são contextos derivados das
/// permissões efetivas. Um token carrega UM modo por vez.
/// </summary>
public enum AccessMode
{
    Global = 0,
    Contracting = 1,
    Provider = 2
}
