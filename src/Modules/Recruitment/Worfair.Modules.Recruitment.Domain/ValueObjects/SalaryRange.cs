namespace Worfair.Modules.Recruitment.Domain.ValueObjects;

/// <summary>
/// Faixa salarial da requisição — invariante R-xx: Minimum ≤ Maximum na MESMA
/// moeda; Maximum nulo = "a combinar" (docs/architecture/03 §3.2 e 05 §3).
/// </summary>
public sealed class SalaryRange : ValueObject
{
    private SalaryRange()
    {
        // EF Core
    }

    private SalaryRange(Money minimum, Money? maximum)
    {
        Minimum = minimum;
        Maximum = maximum;
    }

    public Money Minimum { get; private set; } = default!;

    public Money? Maximum { get; private set; }

    public static Result<SalaryRange> Create(Money minimum, Money? maximum = null)
    {
        if (maximum is not null)
        {
            if (minimum.Currency != maximum.Currency)
                return Result.Failure<SalaryRange>(RecruitmentErrors.SalaryRangeCurrencyMismatch);

            if (minimum.Amount > maximum.Amount)
                return Result.Failure<SalaryRange>(RecruitmentErrors.SalaryRangeMinimumExceedsMaximum);
        }

        return new SalaryRange(minimum, maximum);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Minimum;
        if (Maximum is not null)
            yield return Maximum;
    }
}
