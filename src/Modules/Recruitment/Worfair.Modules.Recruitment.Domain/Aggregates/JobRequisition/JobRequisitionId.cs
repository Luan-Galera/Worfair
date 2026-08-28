namespace Worfair.Modules.Recruitment.Domain.Aggregates.JobRequisition;

using Worfair.BuildingBlocks.Domain.ValueObjects;

/// <summary>Identificador tipado da requisição de vaga (docs/architecture/05 §2).</summary>
public readonly record struct JobRequisitionId(Guid Value)
{
    public override string ToString() => Value.ToString();
}
