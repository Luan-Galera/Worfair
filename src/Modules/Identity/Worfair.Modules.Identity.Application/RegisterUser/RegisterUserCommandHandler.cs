namespace Worfair.Modules.Identity.Application.RegisterUser;

using Worfair.BuildingBlocks.Application.Cqrs;
using Worfair.BuildingBlocks.Domain.Errors;
using Worfair.Modules.Identity.Application.Abstractions;
using Worfair.Modules.Identity.Application.Ports;
using Worfair.Modules.Identity.Domain.Aggregates.Role;
using Worfair.Modules.Identity.Domain.Aggregates.User;
using Worfair.Modules.Identity.Domain.Aggregates.UserRole;
using Worfair.Modules.Identity.Domain.Abstractions;
using Worfair.Modules.Identity.Domain.Errors;
using Worfair.Modules.Identity.Domain.ValueObjects;

/// <summary>
/// Auto-cadastro público. Bootstrap: o PRIMEIRO usuário da plataforma recebe
/// SUPER_ADMIN (contexto global) para permitir provisionar tenants.
/// </summary>
public sealed class RegisterUserCommandHandler(
    IUserRepository users,
    IRoleRepository roles,
    IUserRoleRepository userRoles,
    IIdentityUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher)
    : ICommandHandler<RegisterUserCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        var emailResult = Email.Create(command.Email);
        if (emailResult.IsFailure)
            return Result.Failure<Guid>(emailResult.Error!);

        if (await users.EmailExistsAsync(emailResult.Value.Value, cancellationToken).ConfigureAwait(false))
            return Result.Failure<Guid>(AuthErrors.EmailTaken);

        var isFirstUser = !await users.HasAnyAsync(cancellationToken).ConfigureAwait(false);

        var createResult = User.Register(
            emailResult.Value,
            passwordHasher.Hash(command.Password),
            command.FullName,
            autoVerifyEmail: true);
        if (createResult.IsFailure)
            return Result.Failure<Guid>(createResult.Error!);

        var user = createResult.Value;
        await users.AddAsync(user, cancellationToken).ConfigureAwait(false);

        if (isFirstUser)
        {
            var superAdmin = await roles.GetByCodeAsync(RoleCodes.SuperAdmin, cancellationToken).ConfigureAwait(false);
            if (superAdmin is not null)
            {
                var grant = UserRole.Grant(user.Id, superAdmin, tenantId: null, grantedBy: null);
                if (grant.IsSuccess)
                    await userRoles.AddAsync(grant.Value, cancellationToken).ConfigureAwait(false);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return user.Id;
    }

    private Task<bool> AnyUserExistsProbe(CancellationToken cancellationToken)
        => users.EmailExistsAsync(string.Empty, cancellationToken); // nunca true — ver probe abaixo
}
