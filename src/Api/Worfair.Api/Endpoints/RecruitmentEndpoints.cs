namespace Worfair.Api.Endpoints;

using MediatR;
using Worfair.Api.Authorization;
using Worfair.Api.Extensions;
using Worfair.Modules.Recruitment.Application.Candidates;
using Worfair.Modules.Recruitment.Application.Interviews;
using Worfair.Modules.Recruitment.Application.JobRequisitions;
using Worfair.Modules.Recruitment.Application.Queries;

public static class RecruitmentEndpoints
{
    public static IEndpointRouteBuilder MapRecruitmentEndpoints(this IEndpointRouteBuilder app)
    {
        // ── Requisições de vaga (operação interna do ATS) ───────────────────
        var requisitions = app.MapGroup("/api/recruitment/requisitions").WithTags("Recruitment · Requisitions");

        requisitions.MapPost(string.Empty,
                async (CreateJobRequisitionCommand command, ISender sender, CancellationToken ct) =>
                    (await sender.Send(command, ct).ConfigureAwait(false)).ToHttpResult())
            .RequireAuthorization(SecurityPolicies.RequisitionsCreate)
            .WithName("CreateJobRequisition")
            .WithSummary("Cria JobRequisition em Draft. Publicação exige time de contratação.");

        requisitions.MapGet("/{requisitionId:guid}",
                async (Guid requisitionId, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new GetJobRequisitionByIdQuery(requisitionId), ct)
                        .ConfigureAwait(false)).ToHttpResult())
            .RequireAuthorization(SecurityPolicies.RequisitionsCreate)
            .WithName("GetJobRequisitionById");

        requisitions.MapGet(string.Empty,
                async (ISender sender, CancellationToken ct) =>
                    (await sender.Send(new ListJobRequisitionsQuery(), ct).ConfigureAwait(false)).ToHttpResult())
            .RequireAuthorization(SecurityPolicies.RequisitionsCreate)
            .WithName("ListJobRequisitions");

        requisitions.MapPost("/{requisitionId:guid}/team",
                async (Guid requisitionId, AddTeamMemberRequest request, ISender sender, CancellationToken ct) =>
                    (await sender
                        .Send(new AddHiringTeamMemberCommand(requisitionId, request.RecruiterUserId, request.Role), ct)
                        .ConfigureAwait(false)).ToAcceptedResult())
            .RequireAuthorization(SecurityPolicies.TeamManage)
            .WithName("AddHiringTeamMember");

        requisitions.MapPatch("/{requisitionId:guid}/salary-range",
                async (Guid requisitionId, SalaryRangeRequest request, ISender sender, CancellationToken ct) =>
                    (await sender
                        .Send(new ChangeSalaryRangeCommand(requisitionId, request.SalaryMin, request.SalaryMax, request.Currency), ct)
                        .ConfigureAwait(false)).ToAcceptedResult())
            .RequireAuthorization(SecurityPolicies.RequisitionsCreate)
            .WithName("ChangeSalaryRange");

        requisitions.MapPost("/{requisitionId:guid}/publish",
                async (Guid requisitionId, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new PublishJobRequisitionCommand(requisitionId), ct)
                        .ConfigureAwait(false)).ToAcceptedResult())
            .RequireAuthorization(SecurityPolicies.RequisitionsPublish)
            .WithName("PublishJobRequisition")
            .WithSummary("Draft → Published. Emite evento via Outbox para Notifications.");

        requisitions.MapPost("/{requisitionId:guid}/pause",
                async (Guid requisitionId, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new PauseJobRequisitionCommand(requisitionId), ct)
                        .ConfigureAwait(false)).ToAcceptedResult())
            .RequireAuthorization(SecurityPolicies.RequisitionsPublish)
            .WithName("PauseJobRequisition");

        requisitions.MapPost("/{requisitionId:guid}/resume",
                async (Guid requisitionId, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new ResumeJobRequisitionCommand(requisitionId), ct)
                        .ConfigureAwait(false)).ToAcceptedResult())
            .RequireAuthorization(SecurityPolicies.RequisitionsPublish)
            .WithName("ResumeJobRequisition");

        requisitions.MapPost("/{requisitionId:guid}/close",
                async (Guid requisitionId, ReasonRequest request, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new CloseJobRequisitionCommand(requisitionId, request.Reason), ct)
                        .ConfigureAwait(false)).ToAcceptedResult())
            .RequireAuthorization(SecurityPolicies.RequisitionsClose)
            .WithName("CloseJobRequisition");

        requisitions.MapPost("/{requisitionId:guid}/cancel",
                async (Guid requisitionId, ReasonRequest request, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new CancelJobRequisitionCommand(requisitionId, request.Reason), ct)
                        .ConfigureAwait(false)).ToAcceptedResult())
            .RequireAuthorization(SecurityPolicies.RequisitionsClose)
            .WithName("CancelJobRequisition");

        // ── Candidatos (pipeline) ───────────────────────────────────────────
        var candidates = app.MapGroup("/api/recruitment/candidates").WithTags("Recruitment · Candidates");

        candidates.MapPost(string.Empty,
                async (CreateCandidateCommand command, ISender sender, CancellationToken ct) =>
                    (await sender.Send(command, ct).ConfigureAwait(false)).ToHttpResult())
            .RequireAuthorization(SecurityPolicies.CandidateRead)
            .WithName("CreateCandidate");

        candidates.MapGet("/{candidateId:guid}",
                async (Guid candidateId, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new GetCandidateByIdQuery(candidateId), ct)
                        .ConfigureAwait(false)).ToHttpResult())
            .RequireAuthorization(SecurityPolicies.CandidateRead)
            .WithName("GetCandidateById");

        candidates.MapGet(string.Empty,
                async (ISender sender, CancellationToken ct) =>
                    (await sender.Send(new ListCandidatesQuery(), ct).ConfigureAwait(false)).ToHttpResult())
            .RequireAuthorization(SecurityPolicies.CandidateRead)
            .WithName("ListCandidates");

        candidates.MapPost("/{candidateId:guid}/advance",
                async (Guid candidateId, AdvanceRequest request, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new AdvanceCandidateCommand(candidateId, request.TargetStage), ct)
                        .ConfigureAwait(false)).ToHttpResult())
            .RequireAuthorization(SecurityPolicies.CandidateAdvance)
            .WithName("AdvanceCandidate")
            .WithSummary("Avança 1 estágio da máquina; Interviewing exige entrevista agendada.");

        candidates.MapPost("/{candidateId:guid}/reject",
                async (Guid candidateId, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new RejectCandidateCommand(candidateId), ct)
                        .ConfigureAwait(false)).ToAcceptedResult())
            .RequireAuthorization(SecurityPolicies.CandidateAdvance)
            .WithName("RejectCandidate");

        candidates.MapPost("/{candidateId:guid}/hire",
                async (Guid candidateId, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new HireCandidateCommand(candidateId), ct)
                        .ConfigureAwait(false)).ToAcceptedResult())
            .RequireAuthorization(SecurityPolicies.CandidateHire)
            .WithName("HireCandidate")
            .WithSummary("Offered → Hired. Publica CandidateHired via Outbox → Proposals/Notifications.");

        // ── Entrevistas ─────────────────────────────────────────────────────
        var interviews = app.MapGroup("/api/recruitment/interviews").WithTags("Recruitment · Interviews");

        interviews.MapPost(string.Empty,
                async (ScheduleInterviewCommand command, ISender sender, CancellationToken ct) =>
                    (await sender.Send(command, ct).ConfigureAwait(false)).ToHttpResult())
            .RequireAuthorization(SecurityPolicies.InterviewSchedule)
            .WithName("ScheduleInterview");

        interviews.MapGet(string.Empty,
                async (Guid candidateId, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new ListInterviewsByCandidateQuery(candidateId), ct)
                        .ConfigureAwait(false)).ToHttpResult())
            .RequireAuthorization(SecurityPolicies.CandidateRead)
            .WithName("ListInterviewsByCandidate");

        interviews.MapPost("/{interviewId:guid}/feedback",
                async (Guid interviewId, FeedbackRequest request, ISender sender, CancellationToken ct) =>
                    (await sender
                        .Send(new AddInterviewFeedbackCommand(interviewId, request.Rating, request.Notes), ct)
                        .ConfigureAwait(false)).ToAcceptedResult())
            .RequireAuthorization(SecurityPolicies.InterviewFeedback)
            .WithName("AddInterviewFeedback")
            .WithSummary("Um feedback por entrevistador; obrigatório antes de concluir.");

        interviews.MapPost("/{interviewId:guid}/complete",
                async (Guid interviewId, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new CompleteInterviewCommand(interviewId), ct)
                        .ConfigureAwait(false)).ToAcceptedResult())
            .RequireAuthorization(SecurityPolicies.InterviewFeedback)
            .WithName("CompleteInterview");

        interviews.MapPost("/{interviewId:guid}/cancel",
                async (Guid interviewId, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new CancelInterviewCommand(interviewId), ct)
                        .ConfigureAwait(false)).ToAcceptedResult())
            .RequireAuthorization(SecurityPolicies.InterviewSchedule)
            .WithName("CancelInterview");

        return app;
    }
}

public sealed record AddTeamMemberRequest(Guid RecruiterUserId, int Role);

public sealed record SalaryRangeRequest(decimal SalaryMin, decimal? SalaryMax, string Currency);

public sealed record ReasonRequest(string Reason);

public sealed record AdvanceRequest(int TargetStage);

public sealed record FeedbackRequest(int Rating, string Notes);
