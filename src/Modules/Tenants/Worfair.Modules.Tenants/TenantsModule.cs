namespace Worfair.Modules.Tenants;

using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Worfair.Modules.Tenants.Application.Abstractions;
using Worfair.Modules.Tenants.Contracts;
using Worfair.Modules.Tenants.Domain.Abstractions;
using Worfair.Modules.Tenants.Infrastructure.Persistence;
using Worfair.Modules.Tenants.Infrastructure.Persistence.Repositories;
using Worfair.Modules.Tenants.Infrastructure.ReadContract;

public static class TenantsModule
{
    /// <summary>
    /// Registra tudo do módulo Tenants no DI: MediatR, DbContext (schema tenancy),
    /// repositórios, contrato read-only, outbox e unidade de trabalho.
    /// </summary>
    public static IServiceCollection AddTenants(this IServiceCollection services, IConfiguration configuration)
    {
        // CQRS: handlers/INotificationHandlers vivem na assembly .Application;
        // validators FluentValidation são registrados da mesma assembly.
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(TenantsModule).Assembly);
            cfg.RegisterServicesFromAssemblyContaining<ITenancyUnitOfWork>();
        });
        services.AddValidatorsFromAssemblyContaining<ITenancyUnitOfWork>();

        // DbContext do módulo (tenant-aware + interceptors de defesa em profundidade)
        services.AddDbContext<TenancyDbContext>((sp, options) =>
        {
            var tenantProvider = sp.GetRequiredService<Worfair.BuildingBlocks.Domain.Tenancy.ITenantProvider>();

            options.UseNpgsql(configuration.GetConnectionString(TenancyOptions.ConnectionStringName));
            options.ReplaceService<Microsoft.EntityFrameworkCore.Infrastructure.IModelCacheKeyFactory,
                Worfair.BuildingBlocks.Infrastructure.Persistence.Tenant.PerTenantModelCacheKeyFactory>();
            options.AddInterceptors(
                new Worfair.BuildingBlocks.Infrastructure.Persistence.Tenant.TenantSaveChangesInterceptor(tenantProvider),
                new Worfair.BuildingBlocks.Infrastructure.Persistence.Audit.AuditableSaveChangesInterceptor(
                    sp.GetRequiredService<Worfair.BuildingBlocks.Application.Ports.IDateTimeProvider>()),
                new Worfair.BuildingBlocks.Infrastructure.Persistence.Tenant.TenantConnectionInterceptor(tenantProvider));
        });

        // Portas → implementações
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<ITenantMembershipRepository, TenantMembershipRepository>();
        services.AddScoped<ITenantSettingsRepository, TenantSettingsRepository>();
        services.AddScoped<ITenancyUnitOfWork, TenancyUnitOfWork>();

        // Contrato read-only para outros módulos
        services.AddScoped<ITenancyReadContract, TenancyReadContract>();

        // IEventBus do módulo = Outbox transacional deste contexto
        services.AddScoped<Worfair.BuildingBlocks.Application.Contracts.IEventBus>(sp =>
            ActivatorUtilities.CreateInstance<Worfair.BuildingBlocks.Infrastructure.Persistence.Outbox.OutboxEventBus<TenancyDbContext>>(sp));

        // Processador do Outbox (entrega at-least-once pós-commit)
        services.AddHostedService<Worfair.BuildingBlocks.Infrastructure.Persistence.Outbox.OutboxProcessorService<TenancyDbContext>>();

        return services;
    }
}
