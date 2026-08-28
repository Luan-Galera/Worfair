using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Scalar.AspNetCore;
using Worfair.Api.Authorization;
using Worfair.Api.Endpoints;
using Worfair.Api.Extensions;
using Worfair.Api.Middleware;
using Worfair.Api.Modules;
using Worfair.Modules.Identity;
using Worfair.Modules.Tenants;

var builder = WebApplication.CreateBuilder(args);

// ── Módulos + infraestrutura do host ────────────────────────────────────────
builder.Services.AddAllModules(builder.Configuration);
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddSecurityPolicies();

builder.Services.AddHttpContextAccessor();
builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi(); // documento OpenAPI (.NET nativo)

builder.Services.AddHealthChecks()
    .AddDbContextCheck<Worfair.Modules.Tenants.Infrastructure.Persistence.TenancyDbContext>("tenancy_db")
    .AddDbContextCheck<Worfair.Modules.Identity.Infrastructure.Persistence.IdentityDbContext>("identity_db")
    .AddDbContextCheck<Worfair.Modules.Recruitment.Infrastructure.Persistence.RecruitmentDbContext>("recruitment_db");

var app = builder.Build();

// ── Pipeline (ordem importa — SEC-02) ───────────────────────────────────────
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<TenantHeaderGuardMiddleware>();   // X-Tenant-Id ⇒ 400
app.UseAuthentication();
app.UseMiddleware<TenantContextMiddleware>();       // claim tenant_id → ITenantProvider
app.UseAuthorization();

// ── OpenAPI + Scalar (UI interativa p/ testes em dev) ───────────────────────
app.MapOpenApi();
if (app.Environment.IsDevelopment())
{
    // http://localhost:5000/scalar
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

await app.ApplyDatabaseMigrationsAsync();

await app.RunAsync();
