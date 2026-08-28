namespace Worfair.Modules.Recruitment.Domain.Aggregates.Candidate;

/// <summary>
/// Máquina de estados do candidato (docs/architecture/03 §3.2):
/// Sourced → Applied → Screened → Interviewing → Offered → Hired/Rejected.
/// </summary>
public enum CandidateStatus
{
    Sourced = 1,
    Applied = 2,
    Screened = 3,
    Interviewing = 4,
    Offered = 5,
    Hired = 6,
    Rejected = 7
}
