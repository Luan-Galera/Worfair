namespace Worfair.Modules.Tenants.Domain.Aggregates.Company;

using Worfair.BuildingBlocks.Domain.ValueObjects;

/// <summary>Identificador tipado da empresa (docs/architecture/05 §2).</summary>
public readonly record struct CompanyId(Guid Value)
{
    public override string ToString() => Value.ToString();
}
