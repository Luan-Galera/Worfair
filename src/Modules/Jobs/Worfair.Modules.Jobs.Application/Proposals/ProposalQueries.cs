namespace Worfair.Modules.Jobs.Application.Proposals;

using Worfair.BuildingBlocks.Application.Cqrs;
using Worfair.BuildingBlocks.Application.Security;
using Worfair.BuildingBlocks.Domain.Errors;
using Worfair.Modules.Jobs.Application.Dtos;
using Worfair.Modules.Jobs.Domain.Abstractions;
using Worfair.Modules.Jobs.Domain.Aggregates.Proposal;

public sealed record ProposalDto(
    Guid Id,
    Guid? JobPostingId,
    Guid? ServiceProjectId,
    Guid ProviderUserId,
    Guid? ProviderCompanyId,
    string Message,
    decimal Amount,
    string Currency,
    int Status,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    decimal PlatformFee,
    decimal TotalAmount,
    decimal ProviderReceives);

public sealed record ListMyProposalsQuery : IQuery<Result<IReadOnlyList<ProposalDto>>>;

public sealed record ListProposalsQuery : IQuery<Result<IReadOnlyList<ProposalDto>>>;

public sealed class ListProposalsQueryHandler(IProposalRepository proposals)
    : IQueryHandler<ListProposalsQuery, Result<IReadOnlyList<ProposalDto>>>
{
    public async Task<Result<IReadOnlyList<ProposalDto>>> Handle(ListProposalsQuery query, CancellationToken cancellationToken)
    {
        var list = await proposals.ListAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success<IReadOnlyList<ProposalDto>>([.. list.Select(ProposalQueries.ToDto)]);
    }
}

public sealed class ListMyProposalsQueryHandler(
    IProposalRepository proposals,
    ICurrentUser currentUser)
    : IQueryHandler<ListMyProposalsQuery, Result<IReadOnlyList<ProposalDto>>>
{
    public async Task<Result<IReadOnlyList<ProposalDto>>> Handle(ListMyProposalsQuery query, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
            return Result.Failure<IReadOnlyList<ProposalDto>>(new Error("Auth.InvalidCredentials", "Usuário não autenticado."));

        var list = await proposals.ListByProviderAsync(userId, cancellationToken).ConfigureAwait(false);
        return Result.Success<IReadOnlyList<ProposalDto>>([.. list.Select(ProposalQueries.ToDto)]);
    }
}

public sealed record ListPendingProposalsQuery : IQuery<Result<IReadOnlyList<ProposalDto>>>;

public sealed class ListPendingProposalsQueryHandler(IProposalRepository proposals)
    : IQueryHandler<ListPendingProposalsQuery, Result<IReadOnlyList<ProposalDto>>>
{
    public async Task<Result<IReadOnlyList<ProposalDto>>> Handle(ListPendingProposalsQuery query, CancellationToken cancellationToken)
    {
        var list = await proposals.ListAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success<IReadOnlyList<ProposalDto>>([.. list
            .Where(p => p.Status == ProposalStatus.Submitted)
            .Select(ProposalQueries.ToDto)
            .OrderByDescending(p => p.CreatedAtUtc)]);
    }
}

internal static class ProposalQueries
{
    public static ProposalDto ToDto(Proposal proposal)
    {
        var fee = Math.Round(proposal.Amount * 0.15m, 2, MidpointRounding.AwayFromZero);
        var total = Math.Round(proposal.Amount * 1.15m, 2, MidpointRounding.AwayFromZero);
        return new(
            proposal.Id.Value,
            proposal.JobPostingId,
            proposal.ServiceProjectId,
            proposal.ProviderUserId,
            proposal.ProviderCompanyId,
            proposal.Message,
            proposal.Amount,
            proposal.Currency,
            (int)proposal.Status,
            proposal.CreatedAtUtc,
            proposal.UpdatedAtUtc,
            fee,
            total,
            proposal.Amount);
    }
}
