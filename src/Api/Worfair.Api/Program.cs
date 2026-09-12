using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Scalar.AspNetCore;
using Worfair.Api.Authorization;
using Worfair.Api.Endpoints;
using Worfair.Api.Extensions;
using Worfair.Api.Infrastructure;
using Worfair.Api.Middleware;
using Worfair.Api.Modules;
using Worfair.Modules.Identity;
using Worfair.Modules.Tenants;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AsaasOptions>(options =>
{
    builder.Configuration.GetSection(AsaasOptions.SectionName).Bind(options);
    options.BaseUrl = builder.Configuration["ASAAS_BASE_URL"] ?? options.BaseUrl;
    options.ApiKey ??= builder.Configuration["ASAAS_API_KEY"];
    options.WebhookToken ??= builder.Configuration["ASAAS_WEBHOOK_TOKEN"];
    options.SplitWalletId ??= builder.Configuration["ASAAS_SPLIT_WALLET_ID"];
});

var safeUrls = HostUrlResolver.ResolveUrls(builder.Configuration);
builder.WebHost.UseUrls(safeUrls);

// ── Módulos + infraestrutura do host ────────────────────────────────────────
builder.Services.AddAllModules(builder.Configuration);
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddSecurityPolicies();

// Gateway Asaas (sandbox): HttpClient com base + access_token do backend.
// Sem ASAAS_API_KEY, as chamadas falham rápido com mensagem clara (503).
builder.Services.AddHttpClient<Worfair.Api.Infrastructure.IAsaasGateway, Worfair.Api.Infrastructure.AsaasGateway>(
    (sp, http) =>
    {
        var asaas = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Worfair.Api.Infrastructure.AsaasOptions>>().Value;
        http.BaseAddress = new Uri(asaas.BaseUrl.TrimEnd('/') + "/");
        http.Timeout = TimeSpan.FromSeconds(30);
        if (!string.IsNullOrWhiteSpace(asaas.ApiKey))
            http.DefaultRequestHeaders.Add("access_token", asaas.ApiKey);
    });

builder.Services.AddHttpContextAccessor();
builder.Services.AddAuthorization();
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevFrontend", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi(); // documento OpenAPI (.NET nativo)

builder.Services.AddHealthChecks()
    .AddDbContextCheck<Worfair.Modules.Tenants.Infrastructure.Persistence.TenancyDbContext>("tenancy_db")
    .AddDbContextCheck<Worfair.Modules.Identity.Infrastructure.Persistence.IdentityDbContext>("identity_db")
    .AddDbContextCheck<Worfair.Modules.Recruitment.Infrastructure.Persistence.RecruitmentDbContext>("recruitment_db")
    .AddDbContextCheck<Worfair.Modules.Jobs.Infrastructure.Persistence.JobsDbContext>("jobs_db");
builder.Services.AddHealthChecks()
    .AddDbContextCheck<Worfair.Api.Infrastructure.FinancialDbContext>("financial_db");

var app = builder.Build();

// ── Pipeline (ordem importa — SEC-02) ───────────────────────────────────────
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<TenantHeaderGuardMiddleware>();   // X-Tenant-Id ⇒ 400
app.UseCors("DevFrontend");
app.UseAuthentication();
app.UseMiddleware<TenantContextMiddleware>();       // claim tenant_id → ITenantProvider
app.UseAuthorization();

// ── OpenAPI + Scalar (UI interativa p/ testes em dev) ───────────────────────
app.MapOpenApi();
if (app.Environment.IsDevelopment())
{
    // http://localhost:5000/scalar ou próxima porta disponível
    app.MapScalarApiReference(options =>
        options.WithTitle("Worfair API").WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient));
}

// ── Health checks (docs/devops/04 §1) ───────────────────────────────────────
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false // liveness: processo vivo
});
app.MapHealthChecks("/health/ready"); // readiness: DBs

// ── Endpoints ───────────────────────────────────────────────────────────────
app.MapIdentityEndpoints();
app.MapTenantsEndpoints();
app.MapRecruitmentEndpoints();
app.MapJobsEndpoints();
app.MapProposalsEndpoints();
app.MapFinancialEndpoints();
app.MapAsaasEndpoints();
app.MapNotificationsEndpoints();
app.MapCommunicationEndpoints();

await app.ApplyDatabaseMigrationsAsync();

await app.RunAsync();
