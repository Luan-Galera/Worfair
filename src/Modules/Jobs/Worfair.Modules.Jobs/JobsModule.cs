namespace Worfair.Modules.Jobs;

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
using Worfair.Modules.Jobs.Application.Abstractions;
using Worfair.Modules.Jobs.Domain.Abstractions;
using Worfair.Modules.Jobs.Infrastructure.Persistence;
using Worfair.Modules.Jobs.Infrastructure.Persistence.Repositories;

public static class JobsModule
{
    public static IServiceCollection AddJobs(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(JobsModule).Assembly);
            cfg.RegisterServicesFromAssemblyContaining<IJobsUnitOfWork>();
        });
        services.AddValidatorsFromAssemblyContaining<IJobsUnitOfWork>();

        services.AddDbContext<JobsDbContext>((sp, options) =>
        {
            var tenantProvider = sp.GetRequiredService<ITenantProvider>();

            options.UseNpgsql(configuration.GetConnectionString(JobsOptions.ConnectionStringName));
            options.ReplaceService<Microsoft.EntityFrameworkCore.Infrastructure.IModelCacheKeyFactory,
                PerTenantModelCacheKeyFactory>();
            options.AddInterceptors(
                new TenantSaveChangesInterceptor(tenantProvider),
                new AuditableSaveChangesInterceptor(sp.GetRequiredService<IDateTimeProvider>()),
                new TenantConnectionInterceptor(tenantProvider));
        });

        services.AddScoped<IJobPostingRepository, JobPostingRepository>();
        services.AddScoped<IServiceProjectRepository, ServiceProjectRepository>();
        services.AddScoped<IJobApplicationRepository, JobApplicationRepository>();
        services.AddScoped<IProposalRepository, ProposalRepository>();
        services.AddScoped<IJobsUnitOfWork, JobsUnitOfWork>();

        services.AddScoped<IEventBus>(sp =>
            ActivatorUtilities.CreateInstance<OutboxEventBus<JobsDbContext>>(sp));

        services.AddHostedService<OutboxProcessorService<JobsDbContext>>();

        return services;
    }
}
