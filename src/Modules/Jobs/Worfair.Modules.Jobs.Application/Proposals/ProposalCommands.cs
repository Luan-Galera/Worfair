namespace Worfair.Modules.Jobs.Application.Proposals;

using FluentValidation;
using Worfair.BuildingBlocks.Application.Cqrs;
using Worfair.BuildingBlocks.Application.Security;
using Worfair.BuildingBlocks.Domain.Errors;
using Worfair.BuildingBlocks.Domain.Tenancy;
using Worfair.Modules.Jobs.Application.Abstractions;
using Worfair.Modules.Jobs.Application.Dtos;
using Worfair.Modules.Jobs.Domain.Abstractions;
using Worfair.Modules.Jobs.Domain.Aggregates.Proposal;
using Worfair.Modules.Jobs.Domain.Errors;

public sealed record SubmitProposalCommand(
    Guid? JobPostingId,
    Guid? ServiceProjectId,
    Guid? ProviderCompanyId,
    string Message,
    decimal Amount,
    string? Currency) : ICommand<Result<ProposalDto>>;

public sealed class SubmitProposalCommandValidator : AbstractValidator<SubmitProposalCommand>
{
    public SubmitProposalCommandValidator()
    {
        RuleFor(x => x.Message).NotEmpty().WithErrorCode("Jobs.ProposalMessageRequired");
        RuleFor(x => x.Amount).GreaterThan(0).WithErrorCode("Jobs.InvalidProposalAmount");
    }
}

public sealed class SubmitProposalCommandHandler(
    IProposalRepository proposals,
    IJobsUnitOfWork unitOfWork,
    ITenantProvider tenantProvider,
    ICurrentUser currentUser)
    : ICommandHandler<SubmitProposalCommand, Result<ProposalDto>>
{
    public async Task<Result<ProposalDto>> Handle(SubmitProposalCommand command, CancellationToken cancellationToken)
    {
        if (tenantProvider.TenantId is null || currentUser.UserId is not { } userId)
            return Result.Failure<ProposalDto>(JobsErrors.NotFound);

        if (await proposals.ExistsAsync(command.JobPostingId, command.ServiceProjectId, userId, cancellationToken).ConfigureAwait(false))
            return Result.Failure<ProposalDto>(JobsErrors.ProposalAlreadyExists);

        var result = Proposal.Create(command.JobPostingId, command.ServiceProjectId, userId,
            command.ProviderCompanyId, command.Message, command.Amount, command.Currency);
        if (result.IsFailure)
            return Result.Failure<ProposalDto>(result.Error!);

        await proposals.AddAsync(result.Value, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ProposalQueries.ToDto(result.Value);
    }
}

public sealed record DecideProposalCommand(Guid ProposalId, bool Accept) : ICommand<Result<ProposalDto>>;

public sealed class DecideProposalCommandHandler(
    IProposalRepository proposals,
    IJobsUnitOfWork unitOfWork)
    : ICommandHandler<DecideProposalCommand, Result<ProposalDto>>
{
    public async Task<Result<ProposalDto>> Handle(DecideProposalCommand command, CancellationToken cancellationToken)
    {
        var proposal = await proposals.GetByIdAsync(new ProposalId(command.ProposalId), cancellationToken).ConfigureAwait(false);
        if (proposal is null)
            return Result.Failure<ProposalDto>(JobsErrors.NotFound);

        var result = proposal.Decide(command.Accept);
        if (result.IsFailure)
            return Result.Failure<ProposalDto>(result.Error!);

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ProposalQueries.ToDto(proposal);
    }
}
