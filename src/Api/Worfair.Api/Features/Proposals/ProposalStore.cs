namespace Worfair.Api.Features.Proposals;

using System.Collections.Concurrent;

public enum ProposalStatus
{
    Submitted = 0,
    Accepted = 1,
    Rejected = 2
}

public sealed record ProposalRequest(Guid JobId, Guid UserId, string CoverLetter, decimal Price);

public sealed record ProposalResponse(
    Guid Id,
    Guid JobId,
    Guid UserId,
    string CoverLetter,
    decimal Price,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public static class ProposalStore
{
    private static readonly ConcurrentDictionary<Guid, ProposalResponse> Items = new();

    static ProposalStore()
    {
        AddSeed();
    }

    public static IReadOnlyCollection<ProposalResponse> List() => Items.Values.OrderByDescending(x => x.CreatedAtUtc).ToList();

    public static ProposalResponse Create(ProposalRequest request)
    {
        var proposal = new ProposalResponse(
            Guid.NewGuid(),
            request.JobId,
            request.UserId,
            request.CoverLetter.Trim(),
            request.Price,
            ProposalStatus.Submitted.ToString(),
            DateTime.UtcNow,
            null);

        Items[proposal.Id] = proposal;
        return proposal;
    }

    public static ProposalResponse? Decide(Guid proposalId, string decision)
    {
        if (!Items.TryGetValue(proposalId, out var proposal))
            return null;

        var nextStatus = decision.Trim().ToLowerInvariant() switch
        {
            "accepted" => ProposalStatus.Accepted,
            "rejected" => ProposalStatus.Rejected,
            _ => ProposalStatus.Submitted
        };

        var updated = proposal with
        {
            Status = nextStatus.ToString(),
            UpdatedAtUtc = DateTime.UtcNow
        };

        Items[proposalId] = updated;
        return updated;
    }

    private static void AddSeed()
    {
        var jobId = Guid.Parse("8d8d4b3d-10f6-4a1f-bb5e-3ff8d9d9ad61");
        var userId = Guid.Parse("511d1c40-90d1-4c63-bf4b-c05d6d77286f");

        Items[Guid.Parse("1f9b72b8-7efa-4e4d-b8f8-1e8f6490b711")] = new ProposalResponse(
            Guid.Parse("1f9b72b8-7efa-4e4d-b8f8-1e8f6490b711"),
            jobId,
            userId,
            "Tenho experiência em automações, integração e arquitetura de backend para produtos SaaS.",
            2500m,
            ProposalStatus.Submitted.ToString(),
            DateTime.UtcNow.AddDays(-1),
            null);
    }
}
