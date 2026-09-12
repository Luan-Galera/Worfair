namespace Worfair.Api.Endpoints;

using Worfair.Api.Authorization;
using MediatR;
using Worfair.Api.Extensions;
using Worfair.Modules.Jobs.Application.Proposals;

public static class ProposalsEndpoints
{
    public static IEndpointRouteBuilder MapProposalsEndpoints(this IEndpointRouteBuilder app)
    {
        var proposals = app.MapGroup("/api/proposals").WithTags("Proposals");

        proposals.MapGet(string.Empty, async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new ListProposalsQuery(), ct).ConfigureAwait(false)).ToHttpResult())
            .WithName("ListProposals")
            .RequireAuthorization(SecurityPolicies.ProposalRead);

        proposals.MapGet("/mine", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new ListMyProposalsQuery(), ct).ConfigureAwait(false)).ToHttpResult())
            .WithName("ListMyProposals")
            .RequireAuthorization(SecurityPolicies.ProposalSubmit);

        proposals.MapPost(string.Empty, async (SubmitProposalRequest request, ISender sender, CancellationToken ct) =>
                (await sender.Send(new SubmitProposalCommand(request.JobPostingId, request.ServiceProjectId,
                    request.ProviderCompanyId, request.Message, request.Amount, request.Currency), ct)
                    .ConfigureAwait(false)).ToHttpResult())
            .WithName("SubmitProposal")
            .RequireAuthorization(SecurityPolicies.ProposalSubmit);

        proposals.MapPost("/{proposalId:guid}/decide", async (Guid proposalId, DecideProposalRequest request,
                ISender sender, CancellationToken ct) =>
                (await sender.Send(new DecideProposalCommand(proposalId, request.Accept), ct)
                    .ConfigureAwait(false)).ToHttpResult())
            .WithName("DecideProposal")
            .RequireAuthorization(SecurityPolicies.ProposalDecide);

        return app;
    }
}

public sealed record SubmitProposalRequest(
    Guid? JobPostingId,
    Guid? ServiceProjectId,
    Guid? ProviderCompanyId,
    string Message,
    decimal Amount,
    string? Currency);

public sealed record DecideProposalRequest(bool Accept);
