namespace Worfair.Modules.Identity.Infrastructure.ReadContract;

using Microsoft.EntityFrameworkCore;
using Worfair.Modules.Identity.Contracts;
using Worfair.Modules.Identity.Domain.ValueObjects;
using Worfair.Modules.Identity.Infrastructure.Persistence;

public sealed class IdentityReadContract(IdentityDbContext db) : IIdentityReadContract
{
    public async Task<UserSummaryInfo?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await db.Users.AsNoTracking()
            .Where(u => u.Id == userId && u.Status == Domain.Aggregates.User.UserStatus.Active)
            .Select(u => new UserSummaryInfo(u.Id, u.FullName, u.Email.Value))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
