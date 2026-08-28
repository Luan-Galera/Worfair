namespace Worfair.Modules.Recruitment.Domain.Abstractions;

using Worfair.Modules.Recruitment.Domain.Aggregates.Candidate;
using Worfair.Modules.Recruitment.Domain.Aggregates.Interview;
using Worfair.Modules.Recruitment.Domain.Aggregates.JobRequisition;

/// <summary>
/// Portas de persistência do módulo Recruitment (implementadas em Infrastructure).
/// O filtro de tenant vem dos Global Query Filters — GetById de outro tenant
/// devolve null → 404 indistinguível (R-06).
/// </summary>
public interface IJobRequisitionRepository
{
    Task<JobRequisition?> GetByIdAsync(JobRequisitionId id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JobRequisition>> ListByTenantAsync(CancellationToken cancellationToken = default);

    Task AddAsync(JobRequisition requisition, CancellationToken cancellationToken = default);
}

public interface ICandidateRepository
{
    Task<Candidate?> GetByIdAsync(CandidateId id, CancellationToken cancellationToken = default);

    /// <summary>ContactEmail é único por tenant — checagem via Global Query Filter.</summary>
    Task<bool> EmailExistsAsync(ContactEmail email, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Candidate>> ListByTenantAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Candidate candidate, CancellationToken cancellationToken = default);
}

public interface IInterviewRepository
{
    Task<Interview?> GetByIdAsync(InterviewId id, CancellationToken cancellationToken = default);

    /// <summary>Invariante: avanço para Interviewing exige entrevista agendada.</summary>
    Task<bool> HasScheduledForCandidateAsync(Guid candidateId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Interview>> ListByCandidateAsync(Guid candidateId, CancellationToken cancellationToken = default);

    Task AddAsync(Interview interview, CancellationToken cancellationToken = default);
}
