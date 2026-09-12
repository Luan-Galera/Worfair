namespace Worfair.Modules.Identity.Application.RegisterUser;

using Worfair.BuildingBlocks.Application.Cqrs;
using Worfair.BuildingBlocks.Domain.Errors;
using Worfair.Modules.Identity.Application.Abstractions;
using Worfair.Modules.Identity.Application.Ports;
using Worfair.Modules.Identity.Domain.Aggregates.User;
using Worfair.Modules.Identity.Domain.Abstractions;
using Worfair.Modules.Identity.Domain.Errors;
using Worfair.Modules.Identity.Domain.ValueObjects;

/// <summary>
/// Auto-cadastro público. NUNCA concede SUPER_ADMIN (só via seed inicial
/// controlado por ambiente): toda conta nasce sem papel e cria seu espaço
/// no onboarding.
/// </summary>
public sealed class RegisterUserCommandHandler(
    IUserRepository users,
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

        var userType = Enum.IsDefined(typeof(AccountType), command.UserType)
            ? (AccountType)command.UserType
            : AccountType.Individual;

        var createResult = User.Register(
            emailResult.Value,
            passwordHasher.Hash(command.Password),
            command.FullName,
            autoVerifyEmail: true,
            userType: userType,
            document: command.Document,
            phone: command.Phone);
        if (createResult.IsFailure)
            return Result.Failure<Guid>(createResult.Error!);

        var user = createResult.Value;
        await users.AddAsync(user, cancellationToken).ConfigureAwait(false);

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return user.Id;
    }

    private Task<bool> AnyUserExistsProbe(CancellationToken cancellationToken)
        => users.EmailExistsAsync(string.Empty, cancellationToken); // nunca true — ver probe abaixo
}
