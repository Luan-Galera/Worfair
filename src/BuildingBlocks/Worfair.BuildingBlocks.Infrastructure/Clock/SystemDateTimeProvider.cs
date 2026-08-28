namespace Worfair.BuildingBlocks.Infrastructure.Clock;

using Worfair.BuildingBlocks.Application.Ports;

/// <summary>Relógio de sistema (UTC) — registro padrão em produção.</summary>
public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
