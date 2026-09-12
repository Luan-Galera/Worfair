namespace Worfair.Modules.Jobs.Domain.Aggregates.ServiceProject;

using Worfair.BuildingBlocks.Domain.Abstractions;
using Worfair.BuildingBlocks.Domain.Errors;
using Worfair.BuildingBlocks.Domain.Tenancy;
using Worfair.Modules.Jobs.Domain.Abstractions;
using Worfair.Modules.Jobs.Domain.Errors;

public enum ServiceProjectStatus
{
    Draft = 1,
    Open = 2,
    InProgress = 3,
    Completed = 4,
    Cancelled = 5
}

public sealed class ServiceProject : TenantAggregateRoot<ServiceProjectId>
{
    private ServiceProject()
    {
    }

    private ServiceProject(
        ServiceProjectId id,
        Guid? companyId,
        Guid? createdBy,
        string title,
        string description,
        decimal budgetMin,
        decimal? budgetMax,
        string currency,
        DateTime? deadline,
        DateTime utcNow,
        string? category = null,
        string? companyName = null)
    {
        Id = id;
        CompanyId = companyId;
        CompanyName = companyName;
        Category = category;
        CreatedBy = createdBy;
        Title = title.Trim();
        Description = description.Trim();
        BudgetMin = budgetMin;
        BudgetMax = budgetMax;
        Currency = currency.Trim().ToUpperInvariant();
        Deadline = deadline;
        Status = ServiceProjectStatus.Draft;
        CreatedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    public Guid? CompanyId { get; private set; }

    public string? CompanyName { get; private set; }

    public string? Category { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public string Title { get; private set; } = default!;

    public string Description { get; private set; } = default!;

    public decimal BudgetMin { get; private set; }

    public decimal? BudgetMax { get; private set; }

    public string Currency { get; private set; } = default!;

    public DateTime? Deadline { get; private set; }

    public ServiceProjectStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? UpdatedAtUtc { get; private set; }

    public static Result<ServiceProject> Create(
        Guid? companyId,
        Guid? createdBy,
        string? title,
        string? description,
        decimal budgetMin,
        decimal? budgetMax,
        string? currency,
        DateTime? deadline,
        DateTime? utcNow = null,
        string? category = null,
        string? companyName = null)
    {
        var now = utcNow ?? DateTime.UtcNow;

        if (string.IsNullOrWhiteSpace(title))
            return Result.Failure<ServiceProject>(JobsErrors.TitleRequired);

        if (string.IsNullOrWhiteSpace(description))
            return Result.Failure<ServiceProject>(JobsErrors.DescriptionRequired);

        if (budgetMin < 0)
            return Result.Failure<ServiceProject>(JobsErrors.InvalidBudget);

        if (budgetMax.HasValue && budgetMax.Value < budgetMin)
            return Result.Failure<ServiceProject>(JobsErrors.InvalidBudgetRange);

        var normalizedCurrency = (currency ?? "BRL").Trim();
        if (normalizedCurrency.Length != 3)
            return Result.Failure<ServiceProject>(JobsErrors.InvalidCurrency);

        var normalizedCategory = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        if (normalizedCategory is { Length: > 60 })
            return Result.Failure<ServiceProject>(JobsErrors.CategoryTooLong);

        var normalizedCompanyName = string.IsNullOrWhiteSpace(companyName) ? null : companyName.Trim();
        if (normalizedCompanyName is { Length: > 120 })
            return Result.Failure<ServiceProject>(JobsErrors.CompanyNameTooLong);

        return new ServiceProject(
            new ServiceProjectId(Guid.NewGuid()),
            companyId,
            createdBy,
            title,
            description,
            budgetMin,
            budgetMax,
            normalizedCurrency.ToUpperInvariant(),
            deadline,
            now,
            normalizedCategory,
            normalizedCompanyName);
    }

    public Result Open()
    {
        if (Status is ServiceProjectStatus.Completed or ServiceProjectStatus.Cancelled)
            return Result.Failure(JobsErrors.InvalidStatusTransition);

        Status = ServiceProjectStatus.Open;
        UpdatedAtUtc = DateTime.UtcNow;
        return Result.Success();
    }
}

public readonly record struct ServiceProjectId(Guid Value)
{
    public static implicit operator Guid(ServiceProjectId id) => id.Value;
    public override string ToString() => Value.ToString();
}
