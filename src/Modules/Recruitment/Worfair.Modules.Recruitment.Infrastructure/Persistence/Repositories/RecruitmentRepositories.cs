namespace Worfair.Modules.Recruitment.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using Worfair.Modules.Recruitment.Domain.Aggregates.Candidate;
using Worfair.Modules.Recruitment.Domain.Aggregates.Interview;
using Worfair.Modules.Recruitment.Domain.Aggregates.JobRequisition;

public sealed class JobRequisitionRepository(RecruitmentDbContext db) : IJobRequisitionRepository
{
    public Task<JobRequisition?> GetByIdAsync(JobRequisitionId id, CancellationToken cancellationToken = default) =>
        db.JobRequisitions.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<JobRequisition>> ListByTenantAsync(CancellationToken cancellationToken = default) =>
        await db.JobRequisitions
            .AsNoTracking()
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AddAsync(JobRequisition requisition, CancellationToken cancellationToken = default) =>
        await db.JobRequisitions.AddAsync(requisition, cancellationToken).ConfigureAwait(false);
}

public sealed class CandidateRepository(RecruitmentDbContext db) : ICandidateRepository
{
    public Task<Candidate?> GetByIdAsync(CandidateId id, CancellationToken cancellationToken = default) =>
        db.Candidates.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    // R-xx: e-mail é único por tenant — checagem via Global Query Filter;
    // UNIQUE (tenant_id, lower(email)) no banco é a última defesa.
    public Task<bool> EmailExistsAsync(ContactEmail email, CancellationToken cancellationToken = default) =>
        db.Candidates.AnyAsync(c => c.Email == email, cancellationToken);

    public async Task<IReadOnlyList<Candidate>> ListByTenantAsync(CancellationToken cancellationToken = default) =>
        await db.Candidates
            .AsNoTracking()
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AddAsync(Candidate candidate, CancellationToken cancellationToken = default) =>
        await db.Candidates.AddAsync(candidate, cancellationToken).ConfigureAwait(false);
}

public sealed class InterviewRepository(RecruitmentDbContext db) : IInterviewRepository
{
    public Task<Interview?> GetByIdAsync(InterviewId id, CancellationToken cancellationToken = default) =>
        db.Interviews.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public Task<bool> HasScheduledForCandidateAsync(Guid candidateId, CancellationToken cancellationToken = default) =>
        db.Interviews.AnyAsync(
            i => i.CandidateId == candidateId && i.Status == InterviewStatus.Scheduled,
            cancellationToken);

    public async Task<IReadOnlyList<Interview>> ListByCandidateAsync(Guid candidateId, CancellationToken cancellationToken = default) =>
        await db.Interviews
            .AsNoTracking()
            .Where(i => i.CandidateId == candidateId)
            .OrderBy(i => i.ScheduledAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AddAsync(Interview interview, CancellationToken cancellationToken = default) =>
        await db.Interviews.AddAsync(interview, cancellationToken).ConfigureAwait(false);
}
