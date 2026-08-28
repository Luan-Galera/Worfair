# Gera par RSA 2048 para assinatura RS256 em DESENVOLVIMENTO.
# Estrategia universal: usa o proprio .NET SDK (dotnet run) — funciona em
# Windows PowerShell 5.1 e PowerShell 7+ (APIs PEM nao existem no .NET Framework).
# Uso: powershell -ExecutionPolicy Bypass -File dev/jwt/generate.ps1   (da raiz do repo)
# Em staging/producao as chaves sao gerenciadas por secrets/IaC — NUNCA commitadas
# (.gitignore ja cobre dev/jwt/*.pem).

$ErrorActionPreference = 'Stop'
$dir = $PSScriptRoot

$generator = Join-Path ([System.IO.Path]::GetTempPath()) 'worfair-keygen'
New-Item -ItemType Directory -Force -Path $generator | Out-Null

Set-Content (Join-Path $generator 'keygen.csproj') @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>disable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
'@

Set-Content (Join-Path $generator 'Program.cs') @'
using System.Security.Cryptography;
var rsa = RSA.Create(2048);
var outDir = args.Length > 0 ? args[0] : ".";
File.WriteAllText(Path.Combine(outDir, "private.pem"), rsa.ExportPkcs8PrivateKeyPem());
File.WriteAllText(Path.Combine(outDir, "public.pem"),  rsa.ExportSubjectPublicKeyInfoPem());
Console.WriteLine("OK");
'@

dotnet run --project $generator -- $dir | Out-Null

if ((Test-Path (Join-Path $dir 'private.pem')) -and (Test-Path (Join-Path $dir 'public.pem'))) {
    Write-Host "Chaves geradas em $dir (private.pem / public.pem)"
}
else {
    throw "Falha ao gerar as chaves."
}
