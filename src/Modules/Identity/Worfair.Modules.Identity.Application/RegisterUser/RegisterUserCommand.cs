namespace Worfair.Modules.Identity.Application.RegisterUser;

using FluentValidation;
using Worfair.BuildingBlocks.Application.Cqrs;

public sealed record RegisterUserCommand(
    string Email,
    string Password,
    string FullName) : ICommand<Result<Guid>>;

public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(c => c.Email)
            .NotEmpty().EmailAddress().WithErrorCode("Auth.EmailInvalid");

        RuleFor(c => c.Password)
            .NotEmpty().MinimumLength(8).WithErrorCode("Auth.PasswordTooShort");

        RuleFor(c => c.FullName)
            .NotEmpty().WithErrorCode("Auth.FullNameRequired")
            .MaximumLength(200);
    }
}
