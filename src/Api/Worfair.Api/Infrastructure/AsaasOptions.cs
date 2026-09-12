namespace Worfair.Api.Infrastructure;

public sealed class AsaasOptions
{
    public const string SectionName = "Asaas";

    public string BaseUrl { get; set; } = "https://sandbox.asaas.com/api/v3/";

    public string Environment { get; init; } = "sandbox";

    public string? ApiKey { get; set; }

    /// <summary>Token do webhook configurado no painel Asaas (header asaas-access-token).</summary>
    public string? WebhookToken { get; set; }

    /// <summary>
    /// Wallet id da plataforma para split automático de 15% (opcional).
    /// Sem wallet, a cobrança sai sem split e a taxa é controlada localmente.
    /// </summary>
    public string? SplitWalletId { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
