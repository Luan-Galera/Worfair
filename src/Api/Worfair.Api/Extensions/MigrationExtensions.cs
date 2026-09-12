namespace Worfair.Api.Extensions;

using Microsoft.EntityFrameworkCore;

/// <summary>
/// Runner de migrações (docs/devops/02): ordem controlada — Tenancy ANTES de
/// Identity (FKs whitelisted do núcleo). Em dev: DB_AUTO_MIGRATE=true aplica
/// no startup; DB_MIGRATE_ONLY=true (container migrate) aplica e encerra.
/// Staging/produção: passo controlado do pipeline, NUNCA automático.
/// </summary>
public static class MigrationExtensions
{
    public static async Task ApplyDatabaseMigrationsAsync(this WebApplication app)
    {
        var migrateOnly = app.Configuration.GetValue<bool>("DB_MIGRATE_ONLY");
        var autoMigrate = app.Configuration.GetValue<bool>("DB_AUTO_MIGRATE");

        if (!migrateOnly && !autoMigrate)
            return;

        using var scope = app.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<WebApplication>>();

        // 1) tenancy (tenants/companies/memberships/settings + RLS)
        var tenancyDb = scope.ServiceProvider.GetRequiredService<
            Worfair.Modules.Tenants.Infrastructure.Persistence.TenancyDbContext>();
        await tenancyDb.Database.MigrateAsync().ConfigureAwait(false);
        logger.LogInformation("Migrações aplicadas: tenancy");

        // 2) identity (users/roles/permissions/user_roles/refresh_tokens + seed + RLS)
        var identityDb = scope.ServiceProvider.GetRequiredService<
            Worfair.Modules.Identity.Infrastructure.Persistence.IdentityDbContext>();
        await identityDb.Database.MigrateAsync().ConfigureAwait(false);
        logger.LogInformation("Migrações aplicadas: identity");

        // 3) recruitment (job_requisitions/candidates/interviews + RLS)
        var recruitmentDb = scope.ServiceProvider.GetRequiredService<
            Worfair.Modules.Recruitment.Infrastructure.Persistence.RecruitmentDbContext>();
        await recruitmentDb.Database.MigrateAsync().ConfigureAwait(false);
        logger.LogInformation("Migrações aplicadas: recruitment");

        // 4) jobs (job_postings/service_projects + outbox_messages)
        var jobsDb = scope.ServiceProvider.GetRequiredService<
            Worfair.Modules.Jobs.Infrastructure.Persistence.JobsDbContext>();
        await jobsDb.Database.MigrateAsync().ConfigureAwait(false);
        logger.LogInformation("Migrações aplicadas: jobs");

        var financialDb = scope.ServiceProvider.GetRequiredService<Worfair.Api.Infrastructure.FinancialDbContext>();
        await financialDb.Database.MigrateAsync().ConfigureAwait(false);
        logger.LogInformation("Migrações aplicadas: financial");

        await app.SeedInitialAdminAsync(scope).ConfigureAwait(false);

        if (migrateOnly)
        {
            logger.LogInformation("DB_MIGRATE_ONLY=true — encerrando após migrações.");
            await app.StopAsync().ConfigureAwait(false);
        }
    }
}
