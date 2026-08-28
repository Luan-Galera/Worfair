namespace Worfair.Modules.Identity.Domain.ValueObjects;

using Worfair.BuildingBlocks.Domain.Errors;
using Worfair.BuildingBlocks.Domain.ValueObjects;

/// <summary>E-mail normalizado (lower) com validação leve — unicidade global por lower(email).</summary>
public sealed class Email : ValueObject
{
    public const int MaxLength = 320;
    private static readonly System.Text.RegularExpressions.Regex Pattern = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$",
        System.Text.RegularExpressions.RegexOptions.Compiled,
        matchTimeout: TimeSpan.FromMilliseconds(250));

    private Email(string value) => Value = value;

    public string Value { get; }

    public static Result<Email> Create(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Result.Failure<Email>(AuthErrors.EmailRequired);

        var normalized = input.Trim().ToLowerInvariant();
        if (normalized.Length > MaxLength || !Pattern.IsMatch(normalized))
            return Result.Failure<Email>(AuthErrors.EmailInvalid);

        return new Email(normalized);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
