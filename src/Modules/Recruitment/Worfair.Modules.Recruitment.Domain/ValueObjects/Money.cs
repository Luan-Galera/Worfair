namespace Worfair.Modules.Recruitment.Domain.ValueObjects;

/// <summary>
/// Valor monetário com moeda ISO 4217 (docs/architecture/05 §3).
/// </summary>
public sealed class Money : ValueObject
{
    private Money()
    {
        // EF Core
    }

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = default!;

    public static Result<Money> Create(decimal amount, string? currency)
    {
        if (amount < 0)
            return Result.Failure<Money>(RecruitmentErrors.MoneyNegativeAmount);

        var normalized = currency?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length != 3)
            return Result.Failure<Money>(RecruitmentErrors.MoneyInvalidCurrency);

        return new Money(amount, normalized);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }
}
