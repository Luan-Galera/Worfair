namespace Worfair.BuildingBlocks.Infrastructure.Caching;

using Microsoft.Extensions.Caching.Memory;
using Worfair.BuildingBlocks.Application.Ports;

/// <summary>Cache em memória (dev/padrão). Recursos sensíveis nunca usam cache (SEC-01 §4.1).</summary>
public sealed class MemoryCacheService(IMemoryCache cache) : ICacheService
{
    public TItem? Get<TItem>(string key) =>
        cache.TryGetValue(key, out var value) ? (TItem?)value : default;

    public void Set<TItem>(string key, TItem item, TimeSpan absoluteExpirationRelativeToNow) =>
        cache.Set(key, item, absoluteExpirationRelativeToNow);

    public void Remove(string key) => cache.Remove(key);
}
