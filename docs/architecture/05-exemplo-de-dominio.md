# 05 — Exemplo Prático de Domínio (módulo Recruitment)

Código de referência dos conceitos definidos nos documentos 01–04. Foco em:
**encapsulamento**, **invariantes no construtor/métodos** (nunca `set` público) e
**Value Objects imutáveis com igualdade estrutural**.

Arquivos deste exemplo (futura localização real no repositório):

```
src/Modules/Recruitment/Worfair.Modules.Recruitment.Domain/
├── Aggregates/JobRequisition/
│   ├── JobRequisition.cs
│   ├── JobRequisitionId.cs
│   ├── JobRequisitionStatus.cs
│   ├── HiringTeamMember.cs
│   └── Events/JobRequisitionPublishedDomainEvent.cs
├── ValueObjects/
│   ├── Money.cs
│   ├── SalaryRange.cs
│   └── JobTitle.cs
└── Errors/JobRequisitionErrors.cs
```

## 1. Building blocks (base compartilhada)

```csharp
// BuildingBlocks.Domain/Entities/Entity.cs
public abstract class Entity<TId>
{
    public TId Id { get; protected set; } = default!;

    public override bool Equals(object? obj) =>
        obj is Entity<TId> other && EqualityComparer<TId>.Default.Equals(Id, other.Id);

    public override int GetHashCode() => Id is null ? 0 : Id.GetHashCode();
}

// BuildingBlocks.Domain/Entities/AggregateRoot.cs
public abstract class AggregateRoot<TId> : Entity<TId>
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents;

    protected void RaiseDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}

// BuildingBlocks.Domain/ValueObjects/ValueObject.cs
public abstract class ValueObject
{
    protected abstract IEnumerable<object> GetEqualityComponents();

    public override bool Equals(object? obj)
    {
        if (obj is null || obj.GetType() != GetType()) return false;

        var left = GetEqualityComponents().GetEnumerator();
        var right = ((ValueObject)obj).GetEqualityComponents().GetEnumerator();

        while (left.MoveNext() && right.MoveNext())
        {
            if (!Equals(left.Current, right.Current)) return false;
        }
        return !left.MoveNext() && !right.MoveNext();
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var component in GetEqualityComponents())
            hash.Add(component);
        return hash.ToHashCode();
    }

    public static bool operator ==(ValueObject? left, ValueObject? right) => Equals(left, right);
    public static bool operator !=(ValueObject? left, ValueObject? right) => !Equals(left, right);
}

// BuildingBlocks.Domain/Errors/Result.cs  (D-10: sem exceções para fluxo de negócio)
public sealed record Error(string Code, string Message);

public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error? Error { get; }

    protected Result(bool isSuccess, Error? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, null);
    public static Result Failure(Error error) => new(false, error);
    public static Result<T> Success<T>(T value) => new(value, true, null);
    public static Result<T> Failure<T>(Error error) => new(default!, false, error);
}

public sealed class Result<T> : Result
{
    public T Value { get; }

    internal Result(T value, bool isSuccess, Error? error) : base(isSuccess, error)
        => Value = value;
}

// BuildingBlocks.Domain/Tenancy/TenantId.cs
public readonly record struct TenantId(Guid Value)
{
    public static TenantId New() => new(Guid.NewGuid());
}
```

## 2. Typed IDs e máquina de estados

```csharp
// Aggregates/JobRequisition/JobRequisitionId.cs
public readonly record struct JobRequisitionId(Guid Value);

// Aggregates/JobRequisition/JobRequisitionStatus.cs
public enum JobRequisitionStatus
{
    Draft = 1,
    Published = 2,
    Paused = 3,
    Closed = 4,
    Cancelled = 5
}

// Enums/HiringRole.cs
public enum HiringRole
{
    Recruiter = 1,
    Sourcer = 2,
    Interviewer = 3,
    HiringManager = 4
}
```

## 3. Value Objects — `Money`, `SalaryRange` e `JobTitle`

```csharp
// ValueObjects/Money.cs
public sealed class Money : ValueObject
{
    public decimal Amount { get; }
    public string Currency { get; }        // ISO 4217, ex.: "BRL"

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public static Result<Money> Create(decimal amount, string? currency)
    {
        if (amount < 0)
            return Result.Failure<Money>(MoneyErrors.NegativeAmount);

        var normalized = currency?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length != 3)
            return Result.Failure<Money>(MoneyErrors.InvalidCurrency);

        return new Money(amount, normalized);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }
}

// ValueObjects/SalaryRange.cs
public sealed class SalaryRange : ValueObject
{
    public Money Minimum { get; }
    public Money? Maximum { get; }         // null = "a combinar"

    private SalaryRange(Money minimum, Money? maximum)
    {
        Minimum = minimum;
        Maximum = maximum;
    }

    public static Result<SalaryRange> Create(Money minimum, Money? maximum = null)
    {
        if (maximum is not null)
        {
            if (minimum.Currency != maximum.Currency)
                return Result.Failure<SalaryRange>(SalaryRangeErrors.CurrencyMismatch);

            if (minimum.Amount > maximum.Amount)
                return Result.Failure<SalaryRange>(SalaryRangeErrors.MinimumExceedsMaximum);
        }

        return new SalaryRange(minimum, maximum);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Minimum;
        if (Maximum is not null) yield return Maximum;
    }
}

// ValueObjects/JobTitle.cs
public sealed class JobTitle : ValueObject
{
    public const int MaxLength = 120;

    public string Value { get; }

    private JobTitle(string value) => Value = value;

    public static Result<JobTitle> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result.Failure<JobTitle>(JobTitleErrors.Empty);

        var normalized = value.Trim();
        if (normalized.Length > MaxLength)
            return Result.Failure<JobTitle>(JobTitleErrors.TooLong);

        return new JobTitle(normalized);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}
```

**Por que Value Objects?** `SalaryRange` e `JobTitle` são definidos por seus
**valores**, não por identidade: dois `SalaryRange(3000 BRL, 5000 BRL)` são o mesmo
objeto. Invariantes moram no próprio tipo — impossível existir um `SalaryRange`
inválido em qualquer parte do sistema.

## 4. Entidade filha — `HiringTeamMember`

```csharp
// Aggregates/JobRequisition/HiringTeamMember.cs
public sealed class HiringTeamMember
{
    public Guid RecruiterUserId { get; }
    public HiringRole Role { get; }
    public DateTime AddedAtUtc { get; }

    private HiringTeamMember(Guid recruiterUserId, HiringRole role, DateTime addedAtUtc)
    {
        RecruiterUserId = recruiterUserId;
        Role = role;
        AddedAtUtc = addedAtUtc;
    }

    public static HiringTeamMember Create(Guid recruiterUserId, HiringRole role, DateTime addedAtUtc)
    {
        if (recruiterUserId == Guid.Empty)
            throw new ArgumentException("Recruiter inválido.", nameof(recruiterUserId));

        return new HiringTeamMember(recruiterUserId, role, addedAtUtc);
    }
}
```

## 5. Aggregate Root — `JobRequisition`

```csharp
// Aggregates/JobRequisition/JobRequisition.cs
public sealed class JobRequisition : AggregateRoot<JobRequisitionId>
{
    private readonly List<HiringTeamMember> _hiringTeam = [];

    public JobRequisitionId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public JobTitle Title { get; private set; }
    public string Description { get; private set; }
    public SalaryRange SalaryRange { get; private set; }
    public JobRequisitionStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }
    public IReadOnlyList<HiringTeamMember> HiringTeam => _hiringTeam;

    // Construtor privado: criação somente via factory estática → invariantes garantidos.
    private JobRequisition(
        JobRequisitionId id,
        TenantId tenantId,
        JobTitle title,
        string description,
        SalaryRange salaryRange,
        JobRequisitionStatus status,
        DateTime createdAtUtc)
    {
        Id = id;
        TenantId = tenantId;
        Title = title;
        Description = description;
        SalaryRange = salaryRange;
        Status = status;
        CreatedAtUtc = createdAtUtc;
    }

    public static Result<JobRequisition> Create(
        TenantId tenantId, JobTitle title, string description, SalaryRange salaryRange)
    {
        if (tenantId == default)
            return Result.Failure<JobRequisition>(JobRequisitionErrors.TenantRequired);

        if (string.IsNullOrWhiteSpace(description))
            return Result.Failure<JobRequisition>(JobRequisitionErrors.DescriptionRequired);

        var requisition = new JobRequisition(
            new JobRequisitionId(Guid.NewGuid()),
            tenantId,
            title,
            description,
            salaryRange,
            JobRequisitionStatus.Draft,
            DateTime.UtcNow);

        requisition.RaiseDomainEvent(
            new JobRequisitionCreatedDomainEvent(requisition.Id, requisition.TenantId));

        return Result.Success(requisition);
    }

    public Result Publish(DateTime utcNow)
    {
        if (Status != JobRequisitionStatus.Draft)
            return Result.Failure(JobRequisitionErrors.InvalidStatusTransition);

        if (_hiringTeam.Count == 0)
            return Result.Failure(JobRequisitionErrors.CannotPublishWithoutTeam);

        Status = JobRequisitionStatus.Published;
        PublishedAtUtc = utcNow;

        RaiseDomainEvent(
            new JobRequisitionPublishedDomainEvent(Id, TenantId, Title.Value));

        return Result.Success();
    }

    public Result Pause()
    {
        if (Status != JobRequisitionStatus.Published)
            return Result.Failure(JobRequisitionErrors.InvalidStatusTransition);

        Status = JobRequisitionStatus.Paused;
        return Result.Success();
    }

    public Result ChangeSalaryRange(SalaryRange newSalaryRange)
    {
        if (Status is JobRequisitionStatus.Closed or JobRequisitionStatus.Cancelled)
            return Result.Failure(JobRequisitionErrors.CannotChangeClosed);

        SalaryRange = newSalaryRange;
        return Result.Success();
    }

    public Result AddTeamMember(Guid recruiterUserId, HiringRole role, DateTime utcNow)
    {
        if (Status != JobRequisitionStatus.Draft)
            return Result.Failure(JobRequisitionErrors.TeamLockedAfterPublish);

        if (_hiringTeam.Any(m => m.RecruiterUserId == recruiterUserId))
            return Result.Failure(JobRequisitionErrors.DuplicateTeamMember);

        _hiringTeam.Add(HiringTeamMember.Create(recruiterUserId, role, utcNow));
        return Result.Success();
    }

    public Result Close(DateTime utcNow, string reason)
    {
        if (Status is JobRequisitionStatus.Closed or JobRequisitionStatus.Cancelled)
            return Result.Failure(JobRequisitionErrors.InvalidStatusTransition);

        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure(JobRequisitionErrors.CloseReasonRequired);

        Status = JobRequisitionStatus.Closed;
        RaiseDomainEvent(new JobRequisitionClosedDomainEvent(Id, TenantId, reason));
        return Result.Success();
    }
}

// Aggregates/JobRequisition/Events/JobRequisitionPublishedDomainEvent.cs
public sealed record JobRequisitionPublishedDomainEvent(
    JobRequisitionId JobRequisitionId,
    TenantId TenantId,
    string Title) : IDomainEvent;

// Errors/JobRequisitionErrors.cs
public static class JobRequisitionErrors
{
    public static readonly Error TenantRequired =
        new("JobRequisition.TenantRequired", "A requisição exige um tenant.");

    public static readonly Error DescriptionRequired =
        new("JobRequisition.DescriptionRequired", "A descrição é obrigatória.");

    public static readonly Error InvalidStatusTransition =
        new("JobRequisition.InvalidStatusTransition", "Transição de status não permitida.");

    public static readonly Error CannotPublishWithoutTeam =
        new("JobRequisition.CannotPublishWithoutTeam",
            "A requisição precisa de ao menos um membro do time de contratação.");

    public static readonly Error TeamLockedAfterPublish =
        new("JobRequisition.TeamLockedAfterPublish",
            "O time de contratação só pode ser alterado em rascunho.");

    public static readonly Error DuplicateTeamMember =
        new("JobRequisition.DuplicateTeamMember",
            "Este membro já pertence ao time de contratação.");

    public static readonly Error CannotChangeClosed =
        new("JobRequisition.CannotChangeClosed",
            "Uma requisição encerrada ou cancelada não pode ser alterada.");

    public static readonly Error CloseReasonRequired =
        new("JobRequisition.CloseReasonRequired", "Informe o motivo do encerramento.");
}
```

### O que o encapsulamento garante

- **Nenhum `set` público**: o estado só muda por métodos que validam (regra de
  negócio no lugar certo — no domínio, não no handler).
- **Invariantes transicionais**: `Publish()` só funciona em `Draft` e exige time;
  `ChangeSalaryRange` é bloqueada após encerramento.
- **Eventos de domínio** registrados pela própria entidade (o que aconteceu de
  verdade, não deduzido por diffs).
- **`TenantId` definido no nascimento** e nunca alterável (o interceptor de escrita
  do doc 04 reforça isso na camada de persistência).

## 6. Mapeamento EF Core (configuração por entidade)

```csharp
// Persistence/Configurations/JobRequisitionConfiguration.cs
public sealed class JobRequisitionConfiguration : IEntityTypeConfiguration<JobRequisition>
{
    public void Configure(EntityTypeBuilder<JobRequisition> builder)
    {
        builder.ToTable("job_requisitions", "recruitment");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .HasConversion(id => id.Value, value => new JobRequisitionId(value));

        builder.Property(r => r.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value));
        builder.HasIndex(r => new { r.TenantId, r.Status });

        builder.Property(r => r.Title)
            .HasConversion(t => t.Value, value => JobTitle.Create(value).Value)
            .HasMaxLength(JobTitle.MaxLength);

        builder.OwnsOne(r => r.SalaryRange, salary =>
        {
            salary.Property(s => s.Minimum.Amount).HasColumnName("salary_min").HasColumnType("numeric(12,2)");
            salary.Property(s => s.Minimum.Currency).HasColumnName("salary_currency").HasMaxLength(3);
            salary.Property(s => s.Maximum!.Amount).HasColumnName("salary_max").HasColumnType("numeric(12,2)");
            salary.Property(s => s.Maximum!.Currency).HasColumnName("salary_max_currency").HasMaxLength(3);
        });

        builder.HasMany(r => r.HiringTeam)
            .WithOne()
            .HasForeignKey("JobRequisitionId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(r => r.DomainEvents);   // eventos são transient, não persistidos
    }
}
```

## 7. Teste de unidade do domínio (regras de negócio)

```csharp
// tests/Unit/Worfair.Modules.Recruitment.Domain.UnitTests/JobRequisitionTests.cs
public class JobRequisitionTests
{
    private static Result<JobRequisition> CreateDraft()
    {
        var salary = SalaryRange.Create(
            Money.Create(3_000m, "BRL").Value,
            Money.Create(5_000m, "BRL").Value);

        return JobRequisition.Create(
            new TenantId(Guid.NewGuid()),
            JobTitle.Create("Engenheiro de Software Sênior").Value,
            "Atuação no time de plataforma.",
            salary.Value);
    }

    [Fact]
    public void Publish_without_hiring_team_returns_failure()
    {
        var result = CreateDraft();

        var publishResult = result.Value.Publish(DateTime.UtcNow);

        publishResult.IsFailure.Should().BeTrue();
        publishResult.Error.Should().Be(JobRequisitionErrors.CannotPublishWithoutTeam);
    }

    [Fact]
    public void Publish_with_hiring_team_succeeds_and_raises_event()
    {
        var result = CreateDraft();
        result.Value.AddTeamMember(Guid.NewGuid(), HiringRole.Recruiter, DateTime.UtcNow);

        var publishResult = result.Value.Publish(DateTime.UtcNow);

        publishResult.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(JobRequisitionStatus.Published);
        result.Value.DomainEvents
            .Should().ContainSingle(e => e is JobRequisitionPublishedDomainEvent);
    }

    [Fact]
    public void Closed_requisition_cannot_change_salary_range()
    {
        var result = CreateDraft();
        result.Value.Close(DateTime.UtcNow, "Vaga interna preenchida");

        var change = SalaryRange.Create(Money.Create(6_000m, "BRL").Value).Value;
        var changeResult = result.Value.ChangeSalaryRange(change);

        changeResult.IsFailure.Should().BeTrue();
        changeResult.Error.Should().Be(JobRequisitionErrors.CannotChangeClosed);
    }

    [Fact]
    public void Duplicate_team_member_is_rejected()
    {
        var result = CreateDraft();
        var member = Guid.NewGuid();

        result.Value.AddTeamMember(member, HiringRole.Recruiter, DateTime.UtcNow);
        var duplicate = result.Value.AddTeamMember(member, HiringRole.Sourcer, DateTime.UtcNow);

        duplicate.IsFailure.Should().BeTrue();
        duplicate.Error.Should().Be(JobRequisitionErrors.DuplicateTeamMember);
    }
}
```

## 8. Como o handler usa o domínio (Application)

```csharp
// Application/JobRequisitions/Commands/PublishJobRequisition/PublishJobRequisitionCommandHandler.cs
public sealed class PublishJobRequisitionCommandHandler(
    IJobRequisitionRepository repository,
    IDateTimeProvider clock)
    : ICommandHandler<PublishJobRequisitionCommand, Result>
{
    public async Task<Result> Handle(PublishJobRequisitionCommand command, CancellationToken ct)
    {
        // NUNCA recebe TenantId: o tenant vem do ITenantProvider (doc 04).
        var requisition = await repository.GetByIdAsync(command.JobRequisitionId, ct);
        if (requisition is null)
            return Result.Failure(RecruitmentErrors.NotFound("JobRequisition"));

        var result = requisition.Publish(clock.UtcNow);
        if (result.IsSuccess)
            await repository.UnitOfWork.SaveChangesAsync(ct);   // dispatches domain events → outbox

        return result;
    }
}
```