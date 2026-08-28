namespace Worfair.Modules.Identity.Domain.Aggregates.User;

using Worfair.BuildingBlocks.Domain.Auditing;
using Worfair.BuildingBlocks.Domain.Entities;
using Worfair.BuildingBlocks.Domain.Errors;
using Worfair.Modules.Identity.Domain.Errors;
using Worfair.Modules.Identity.Domain.Events;
using Worfair.Modules.Identity.Domain.ValueObjects;

/// <summary>
/// Identidade GLOBAL da pessoa (R-03: sem tenant_id, sem RLS).
/// A filiação a tenants vive no módulo Tenants; as roles vivem em UserRole.
/// </summary>
public sealed class User : AggregateRoot<Guid>, IAuditableEntity
{
    public const int FullNameMaxLength = 200;

    private User()
    {
        // EF Core
    }

    private User(Guid id, Email email, string passwordHash, string fullName, DateTime utcNow)
    {
        Id = id;
        Email = email;
        PasswordHash = passwordHash;
        FullName = fullName;
        Status = UserStatus.Active;
        CreatedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    public Email Email { get; private set; } = default!;

    public string PasswordHash { get; private set; } = default!;

    public string FullName { get; private set; } = default!;

    public UserStatus Status { get; private set; }

    public DateTime? EmailVerifiedAtUtc { get; private set; }

    public DateTime? LastLoginAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? UpdatedAtUtc { get; private set; }

    /// <summary>Registra o usuário em status Active e emite UserRegisteredDomainEvent.</summary>
    public static Result<User> Register(
        Email email, string passwordHash, string? fullName, bool autoVerifyEmail = false, DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;

        if (string.IsNullOrWhiteSpace(fullName))
            return Result.Failure<User>(AuthErrors.FullNameRequired);
        if (fullName.Trim().Length > FullNameMaxLength)
            return Result.Failure<User>(AuthErrors.FullNameRequired);
        if (string.IsNullOrWhiteSpace(passwordHash))
            return Result.Failure<User>(AuthErrors.PasswordHashRequired);

        var user = new User(Guid.NewGuid(), email, passwordHash, fullName.Trim(), now);

        if (autoVerifyEmail)
            user.EmailVerifiedAtUtc = now;

        user.RaiseDomainEvent(new UserRegisteredDomainEvent(user.Key, user.Email.Value, user.FullName));

        return user;
    }

    public UserId Key => new(Id);

    /// <summary>Usuário pode autenticar? (SEC-01 §4 passo 1 — status lido do banco)</summary>
    public Result EnsureCanAuthenticate()
    {
        if (Status != UserStatus.Active)
            return Result.Failure(AuthErrors.UserInactive);

        return Result.Success();
    }

    public void RecordSuccessfulLogin(DateTime utcNow) => LastLoginAtUtc = utcNow;

    public void ChangePasswordHash(string newPasswordHash, DateTime utcNow)
    {
        PasswordHash = newPasswordHash;
        UpdatedAtUtc = utcNow;
    }

    public Result Lock()
    {
        if (Status != UserStatus.Active)
            return Result.Failure(AuthErrors.UserInactive);

        Status = UserStatus.Locked;
        return Result.Success();
    }

    public Result Unlock()
    {
        if (Status != UserStatus.Locked)
            return Result.Failure(AuthErrors.UserInactive);

        Status = UserStatus.Active;
        return Result.Success();
    }

    public Result Disable()
    {
        if (Status == UserStatus.Disabled)
            return Result.Failure(AuthErrors.UserInactive);

        Status = UserStatus.Disabled;
        return Result.Success();
    }
}
