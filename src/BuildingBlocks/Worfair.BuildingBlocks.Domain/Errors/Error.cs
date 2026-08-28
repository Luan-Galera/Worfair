namespace Worfair.BuildingBlocks.Domain.Errors;

/// <summary>Erro de negócio imutável (D-10: Result pattern, sem exceções para fluxo).</summary>
public sealed record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public static implicit operator string(Error error) => error.Code;

    public override string ToString() => $"{Code}: {Message}";
}
