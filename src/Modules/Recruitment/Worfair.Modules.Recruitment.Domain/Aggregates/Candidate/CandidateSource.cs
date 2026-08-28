namespace Worfair.Modules.Recruitment.Domain.Aggregates.Candidate;

/// <summary>Origem do candidato (docs/database/03 §5): 1=Sourced (prospecção), 2=Applied (candidatura).</summary>
public enum CandidateSource
{
    Sourced = 1,
    Applied = 2
}
