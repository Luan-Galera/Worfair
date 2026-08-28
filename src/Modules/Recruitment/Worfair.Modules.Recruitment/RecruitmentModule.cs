namespace Worfair.Modules.Recruitment;

using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Worfair.BuildingBlocks.Application.Contracts;
using Worfair.BuildingBlocks.Application.Ports;
using Worfair.BuildingBlocks.Domain.Tenancy;
using Worfair.BuildingBlocks.Infrastructure.Persistence.Audit;
using Worfair.BuildingBlocks.Infrastructure.Persistence.Outbox;
using Worfair.BuildingBlocks.Infrastructure.Persistence.Tenant;
using Worfair.Modules.Recruitment.Application.Abstractions;
using Worfair.Modules.Recruitment.Domain.Abstractions;
using Worfair.Modules.Recruitment.Infrastructure.Persistence;
using Worfair.Modules.Recruitment.Infrastructure.Persistence.Repositories;

public static class RecruitmentModule
{
    /// <summary>
    /// Registra tudo do módulo Recruitment no DI: MediatR, DbContext (schema
    /// recruitment), repositórios, outbox e unidade de trabalho.
    /// </summary>
    public static IServiceCollection AddRecruitment(this IServiceCollection services, IConfiguration configuration)
    {
        // CQRS: handlers/INotificationHandlers vivem na assembly .Application;
        // validators FluentValidation são registrados da mesma assembly.
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(RecruitmentModule).Assembly);
            cfg.RegisterServicesFromAssemblyContaining<IRecruitmentUnitOfWork>();
        });
        services.AddValidatorsFromAssemblyContaining<IRecruitmentUnitOfWork>();

        // DbContext do módulo (tenant-aware + interceptors de defesa em profundidade)
        services.AddDbContext<RecruitmentDbContext>((sp, options) =>
        {
            var tenantProvider = sp.GetRequiredService<ITenantProvider>();

            options.UseNpgsql(configuration.GetConnectionString(RecruitmentOptions.ConnectionStringName));
            options.ReplaceService<Microsoft.EntityFrameworkCore.Infrastructure.IModelCacheKeyFactory,
                PerTenantModelCacheKeyFactory>();
            options.AddInterceptors(
                new TenantSaveChangesInterceptor(tenantProvider),
                new AuditableSaveChangesInterceptor(sp.GetRequiredService<IDateTimeProvider>()),
                new TenantConnectionInterceptor(tenantProvider));
        });

        // Portas → implementações
        services.AddScoped<IJobRequisitionRepository, JobRequisitionRepository>();
        services.AddScoped<ICandidateRepository, CandidateRepository>();
        services.AddScoped<IInterviewRepository, InterviewRepository>();
        services.AddScoped<IRecruitmentUnitOfWork, RecruitmentUnitOfWork>();

        // IEventBus do módulo = Outbox transacional deste contexto
        services.AddScoped<IEventBus>(sp =>
            ActivatorUtilities.CreateInstance<OutboxEventBus<RecruitmentDbContext>>(sp));

        // Processador do Outbox (entrega at-least-once pós-commit)
        services.AddHostedService<OutboxProcessorService<RecruitmentDbContext>>();

        return services;
    }
}
