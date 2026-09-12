namespace Worfair.Api.Infrastructure;

using System.Net.Http.Json;
using Microsoft.Extensions.Options;

/// <summary>Contratos mínimos da API v3 do Asaas (sandbox) usados pela plataforma.</summary>
public sealed record AsaasCustomerRequest(string Name, string Email, string CpfCnpj);
public sealed record AsaasCustomerResponse(string Id);
public sealed record AsaasSplitRequest(string WalletId, decimal PercentualValue);
public sealed record AsaasPaymentRequest(
    string Customer,
    string BillingType,
    decimal Value,
    string DueDate,
    string Description,
    string ExternalReference,
    IReadOnlyList<AsaasSplitRequest>? Split);
public sealed record AsaasPaymentResponse(string Id, string Status, string? InvoiceUrl);
public sealed record AsaasPaymentStatus(string Id, string Status, decimal Value, string? ExternalReference);

/// <summary>Payload do webhook Asaas: { event, payment: { id, status, ... } }.</summary>
public sealed record AsaasWebhookPayment(
    string Id,
    string Status,
    decimal Value,
    string? ExternalReference);
public sealed record AsaasWebhookPayload(string Event, AsaasWebhookPayment Payment);

public interface IAsaasGateway
{
    Task<AsaasCustomerResponse> CreateCustomerAsync(
        string name, string email, string cpfCnpj, CancellationToken ct);

    Task<AsaasPaymentResponse> CreatePaymentAsync(
        AsaasPaymentRequest request, CancellationToken ct);

    Task<AsaasPaymentStatus> GetPaymentAsync(string asaasPaymentId, CancellationToken ct);
}

/// <summary>Gateway HTTP do Asaas (sandbox por padrão). Falha rápido sem ApiKey.</summary>
public sealed class AsaasGateway(HttpClient http, IOptions<AsaasOptions> options) : IAsaasGateway
{
    public async Task<AsaasCustomerResponse> CreateCustomerAsync(
        string name, string email, string cpfCnpj, CancellationToken ct)
    {
        EnsureConfigured();
        using var res = await http.PostAsJsonAsync("customers",
            new AsaasCustomerRequest(name, email, cpfCnpj), ct).ConfigureAwait(false);
        await EnsureSuccessAsync(res, ct).ConfigureAwait(false);
        return (await res.Content.ReadFromJsonAsync<AsaasCustomerResponse>(ct)
            .ConfigureAwait(false))!;
    }

    public async Task<AsaasPaymentResponse> CreatePaymentAsync(
        AsaasPaymentRequest request, CancellationToken ct)
    {
        EnsureConfigured();
        using var res = await http.PostAsJsonAsync("payments", request, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(res, ct).ConfigureAwait(false);
        return (await res.Content.ReadFromJsonAsync<AsaasPaymentResponse>(ct)
            .ConfigureAwait(false))!;
    }

    public async Task<AsaasPaymentStatus> GetPaymentAsync(string asaasPaymentId, CancellationToken ct)
    {
        EnsureConfigured();
        using var res = await http.GetAsync($"payments/{asaasPaymentId}", ct).ConfigureAwait(false);
        await EnsureSuccessAsync(res, ct).ConfigureAwait(false);
        return (await res.Content.ReadFromJsonAsync<AsaasPaymentStatus>(ct)
            .ConfigureAwait(false))!;
    }

    private void EnsureConfigured()
    {
        if (!options.Value.IsConfigured)
            throw new InvalidOperationException(
                "Asaas não configurado: defina ASAAS_API_KEY (sandbox) no backend.");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage res, CancellationToken ct)
    {
        if (res.IsSuccessStatusCode)
            return;
        var body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        throw new InvalidOperationException($"Asaas respondeu {(int)res.StatusCode}: {body}");
    }
}
