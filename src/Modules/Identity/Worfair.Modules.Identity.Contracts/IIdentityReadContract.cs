namespace Worfair.Modules.Identity.Contracts;

/// <summary>Resumo read-only de usuário para outros módulos (R-07).</summary>
public sealed record UserSummaryInfo(Guid UserId, string FullName, string Email);

public interface IIdentityReadContract
{
    Task<UserSummaryInfo?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
