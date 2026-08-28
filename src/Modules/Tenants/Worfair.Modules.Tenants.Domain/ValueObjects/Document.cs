namespace Worfair.Modules.Tenants.Domain.ValueObjects;

using System.Text.RegularExpressions;
using Worfair.BuildingBlocks.Domain.Errors;
using Worfair.BuildingBlocks.Domain.ValueObjects;

/// <summary>
/// Documento fiscal (CNPJ/CPF) normalizado: apenas dígitos, 11 ou 14 posições.
/// Unicidade é (tenant_id, document) — docs/database/03 §3.
/// </summary>
public sealed class Document : ValueObject
{
    public const int MaxLength = 20;
    private static readonly Regex DigitsOnly = new(
        @"^\d{11}$|^\d{14}$",
        RegexOptions.Compiled,
        matchTimeout: TimeSpan.FromMilliseconds(250));

    private Document(string value) => Value = value;

    public string Value { get; }

    public static Result<Document> Create(string? input)
    {
        var normalized = new string((input ?? string.Empty).Where(char.IsDigit).ToArray());

        if (!DigitsOnly.IsMatch(normalized))
            return Result.Failure<Document>(Worfair.Modules.Tenants.Domain.Errors.CompanyErrors.DocumentInvalid);

        return new Document(normalized);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
