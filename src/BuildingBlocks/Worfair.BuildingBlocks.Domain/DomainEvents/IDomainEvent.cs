namespace Worfair.BuildingBlocks.Domain.DomainEvents;

/// <summary>
/// Marker de evento de domínio — sem dependências externas (MediatR é
/// adaptado na camada Application, docs/architecture/02 §2).
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }

    DateTime OccurredOnUtc { get; }
}

public abstract record DomainEvent : IDomainEvent
{
    protected DomainEvent()
    {
        EventId = Guid.NewGuid();
        OccurredOnUtc = DateTime.UtcNow;
    }

    public Guid EventId { get; }

    public DateTime OccurredOnUtc { get; }
}
