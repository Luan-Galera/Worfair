namespace Worfair.BuildingBlocks.Infrastructure.Persistence.Outbox;

using System.Collections.Concurrent;

/// <summary>
/// Registro estático contrato → CLR type. Alimenta a desserialização das
/// mensagens do Outbox dentro do mesmo processo (transporte in-process padrão).
/// </summary>
public static class OutboxEventTypeRegistry
{
    private static readonly ConcurrentDictionary<string, Type> Types = new(StringComparer.Ordinal);

    public static void Register(string contractType, Type clrType) =>
        Types.AddOrUpdate(contractType, clrType, (_, _) => clrType);

    public static bool TryResolve(string contractType, out Type? clrType)
    {
        if (Types.TryGetValue(contractType, out var found))
        {
            clrType = found;
            return true;
        }

        clrType = null;
        return false;
    }
}
