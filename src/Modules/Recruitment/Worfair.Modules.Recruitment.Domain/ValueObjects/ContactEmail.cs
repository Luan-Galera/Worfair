namespace Worfair.Modules.Recruitment.Domain.ValueObjects;

using System.Text.RegularExpressions;

/// <summary>
/// E-mail de contato do candidato — único POR TENANT (não global):
/// dois tenants podem ter o mesmo e-mail (docs/architecture/03 §3.2).
/// </summary>
public sealed partial class ContactEmail : ValueObject
{
    public const int MaxLength = 320;

    private ContactEmail(string value) => Value = value;

    public string Value { get; }

    public static Result<ContactEmail> Create(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Result.Failure<ContactEmail>(CandidateErrors.ContactEmailRequired);

        var normalized = input.Trim().ToLowerInvariant();
        if (normalized.Length > MaxLength || !EmailPattern().IsMatch(normalized))
            return Result.Failure<ContactEmail>(CandidateErrors.ContactEmailInvalid);

        return new ContactEmail(normalized);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled, matchTimeoutMilliseconds: 250)]
    private static partial Regex EmailPattern();
}
