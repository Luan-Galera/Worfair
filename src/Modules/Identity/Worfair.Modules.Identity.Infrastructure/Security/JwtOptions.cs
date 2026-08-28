namespace Worfair.Modules.Identity.Infrastructure.Security;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

/// <summary>Configuração JWT (appsettings "Jwt") — RS256, access curto.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "https://worfair.local";

    public string Audience { get; set; } = "worfair-web";

    public int AccessTokenMinutes { get; set; } = 15;

    /// <summary>PEM da chave PRIVADA (emissão — módulo Identity).</summary>
    public string PrivateKeyPath { get; set; } = string.Empty;

    /// <summary>PEM da chave PÚBLICA (validação — host).</summary>
    public string PublicKeyPath { get; set; } = string.Empty;
}

public static class RsaKeyLoader
{
    /// <summary>Carrega RSA de arquivo PEM (PKCS#1/PKCS#8).</summary>
    public static System.Security.Cryptography.RSA LoadPem(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new InvalidOperationException(
                $"Chave PEM não encontrada em '{path}'. Gere as chaves de dev com dev/jwt/generate.ps1.");

        var rsa = System.Security.Cryptography.RSA.Create();
        rsa.ImportFromPem(File.ReadAllText(path));
        return rsa;
    }

    /// <summary>kid estável: SHA-256 dos primeiros bytes do modulus (rotação por kid).</summary>
    public static string ComputeKeyId(System.Security.Cryptography.RSA rsa)
    {
        var parameters = rsa.ExportParameters(false);
        var modulus = parameters.Modulus ?? [];
        var hash = System.Security.Cryptography.SHA256.HashData(modulus);
        return Convert.ToHexString(hash, 0, 8);
    }
}

public sealed class JwtOptionsSetup(IConfiguration configuration) : IConfigureOptions<JwtOptions>
{
    private readonly IConfiguration _configuration = configuration;

    public void Configure(JwtOptions options)
    {
        _configuration.GetSection(JwtOptions.SectionName).Bind(options);
    }
}
