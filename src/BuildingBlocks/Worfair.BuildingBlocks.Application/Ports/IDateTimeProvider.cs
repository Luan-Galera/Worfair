namespace Worfair.BuildingBlocks.Application.Ports;

/// <summary>Relógio injetável — nunca DateTime.UtcNow direto em handlers (testabilidade).</summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}

/// <summary>Cache genérico opcional (nunca para decisões sensíveis — docs/security/01 §4.1).</summary>
public interface ICacheService
{
    TItem? Get<TItem>(string key);

    void Set<TItem>(string key, TItem item, TimeSpan absoluteExpirationRelativeToNow);

    void Remove(string key);
}
