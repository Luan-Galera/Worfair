namespace Worfair.Modules.Recruitment.Domain.Aggregates.Candidate;

using Worfair.BuildingBlocks.Domain.ValueObjects;

/// <summary>Identificador tipado do candidato.</summary>
public readonly record struct CandidateId(Guid Value)
{
    public override string ToString() => Value.ToString();
}
