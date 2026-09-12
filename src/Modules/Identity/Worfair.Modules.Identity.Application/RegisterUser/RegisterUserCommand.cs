namespace Worfair.Modules.Identity.Application.RegisterUser;

using FluentValidation;
using Worfair.BuildingBlocks.Application.Cqrs;

public sealed record RegisterUserCommand(
    string Email,
    string Password,
    string FullName,
    int UserType,
    string Document,
    string? Phone) : ICommand<Result<Guid>>;

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

        RuleFor(c => c.UserType)
            .InclusiveBetween(1, 2).WithErrorCode("Auth.UserTypeInvalid");

        RuleFor(c => c.Document)
            .NotEmpty().Must(d => (d ?? "").Count(char.IsDigit) is 11 or 14)
            .WithErrorCode("Auth.DocumentInvalid");

        RuleFor(c => c.Phone)
            .MaximumLength(30).WithErrorCode("Auth.PhoneTooLong");
    }
}
