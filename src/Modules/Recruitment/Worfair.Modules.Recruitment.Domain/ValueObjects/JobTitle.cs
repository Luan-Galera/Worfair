namespace Worfair.Modules.Recruitment.Domain.ValueObjects;

/// <summary>Título da vaga/requisição (docs/architecture/05 §3).</summary>
public sealed class JobTitle : ValueObject
{
    public const int MaxLength = 120;

    private JobTitle(string value) => Value = value;

    public string Value { get; }

    public static Result<JobTitle> Create(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Result.Failure<JobTitle>(RecruitmentErrors.JobTitleRequired);

        var normalized = input.Trim();
        if (normalized.Length > MaxLength)
            return Result.Failure<JobTitle>(RecruitmentErrors.JobTitleTooLong);

        return new JobTitle(normalized);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
