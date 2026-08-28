namespace Worfair.Modules.Identity.Contracts.Events;

using Worfair.BuildingBlocks.Contracts.IntegrationEvents;

/// <summary>
/// Catálogo v1 (docs/architecture/03 §4): Identity → Todos.
/// Disponibiliza o usuário para os módulos. Nunca carrega dados sensíveis.
/// </summary>
public sealed record UserProvisionedIntegrationEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }

    public required string Email { get; init; }

    public required string FullName { get; init; }

    public override string ContractType => "worfair.identity.user-provisioned.v1";
}
