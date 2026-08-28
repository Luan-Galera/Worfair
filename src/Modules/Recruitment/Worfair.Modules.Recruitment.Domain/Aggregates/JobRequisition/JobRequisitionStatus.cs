namespace Worfair.Modules.Recruitment.Domain.Aggregates.JobRequisition;

using Worfair.Modules.Recruitment.Domain.Enums;

/// <summary>Máquina de estados (docs/architecture/05 §2): Draft → Published ⇄ Paused → Closed/Cancelled.</summary>
public enum JobRequisitionStatus
{
    Draft = 1,
    Published = 2,
    Paused = 3,
    Closed = 4,
    Cancelled = 5
}
