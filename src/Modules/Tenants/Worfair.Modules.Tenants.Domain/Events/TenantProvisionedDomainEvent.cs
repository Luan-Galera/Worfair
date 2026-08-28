namespace Worfair.Modules.Tenants.Domain.Events;

using Worfair.BuildingBlocks.Domain.DomainEvents;
using Worfair.BuildingBlocks.Domain.ValueObjects;

public sealed record TenantProvisionedDomainEvent(
    TenantId TenantId,
    string Name,
    string Slug) : DomainEvent;
