namespace Worfair.Modules.Identity;

using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Worfair.BuildingBlocks.Application.Security;
using Worfair.Modules.Identity.Application.Abstractions;
using Worfair.Modules.Identity.Application.Ports;
using Worfair.Modules.Identity.Contracts;
using Worfair.Modules.Identity.Domain.Abstractions;
using Worfair.Modules.Identity.Infrastructure.Persistence;
using Worfair.Modules.Identity.Infrastructure.Persistence.Repositories;
using Worfair.Modules.Identity.Infrastructure.ReadContract;
using Worfair.Modules.Identity.Infrastructure.Security;

public static class IdentityModule
{
    /// <summary>
    /// Registra tudo do módulo Identity no DI: MediatR, DbContext (schema
    /// identity), repositórios, hasher BCrypt, token RS256, leitores de
    /// permissão efetiva e o provedor de revalidação de autorização (SEC-01).
    /// </summary>
    public static IServiceCollection AddIdentity(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        // CQRS: handlers/INotificationHandlers vivem na assembly .Application;
        // validators FluentValidation são registrados da mesma assembly.
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(IdentityModule).Assembly);
            cfg.RegisterServicesFromAssemblyContaining<IIdentityUnitOfWork>();
        });
        services.AddValidatorsFromAssemblyContaining<IIdentityUnitOfWork>();

        // DbContext do módulo
        services.AddDbContext<IdentityDbContext>((sp, options) =>
        {
            var tenantProvider = sp.GetRequiredService<Worfair.BuildingBlocks.Domain.Tenancy.ITenantProvider>();

            options.UseNpgsql(configuration.GetConnectionString("Default"));
            options.ReplaceService<Microsoft.EntityFrameworkCore.Infrastructure.IModelCacheKeyFactory,
                Worfair.BuildingBlocks.Infrastructure.Persistence.Tenant.PerTenantModelCacheKeyFactory>();
            options.AddInterceptors(
                new Worfair.BuildingBlocks.Infrastructure.Persistence.Tenant.TenantSaveChangesInterceptor(tenantProvider),
                new Worfair.BuildingBlocks.Infrastructure.Persistence.Audit.AuditableSaveChangesInterceptor(
                    sp.GetRequiredService<Worfair.BuildingBlocks.Application.Ports.IDateTimeProvider>()),
                new Worfair.BuildingBlocks.Infrastructure.Persistence.Tenant.TenantConnectionInterceptor(tenantProvider));
        });

        // Portas → implementações
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IUserRoleRepository, UserRoleRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IIdentityUnitOfWork, IdentityUnitOfWork>();
        services.AddScoped<Worfair.Modules.Identity.Application.Abstractions.IIdentityWriteScope,
            Worfair.Modules.Identity.Infrastructure.Persistence.IdentityWriteScope>();

        // Segurança — RS256 singleton (private fica em memória, não relida/disposta por request)
        // Private nunca commitada: dev usa dev/jwt/*.pem (.gitignore); prod usa env-var JWT__PrivateKeyPem ou secret mount
        services.AddSingleton<Microsoft.IdentityModel.Tokens.RsaSecurityKey>(sp =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<JwtOptions>>().Value;
            var config = sp.GetRequiredService<IConfiguration>();
            var inlinePem = config["JWT__PrivateKeyPem"] ?? config["Jwt:PrivateKeyPemInline"];
            System.Security.Cryptography.RSA rsa;
            if (!string.IsNullOrWhiteSpace(inlinePem))
            {
                rsa = System.Security.Cryptography.RSA.Create();
                rsa.ImportFromPem(inlinePem);
            }
            else
            {
                var path = opts.PrivateKeyPath;
                string resolved = path;
                if (!System.IO.File.Exists(path))
                {
                    var cwd = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), path);
                    if (System.IO.File.Exists(cwd)) resolved = cwd;
                    else
                    {
                        var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
                        while (dir != null)
                        {
                            var cand = System.IO.Path.Combine(dir.FullName, path);
                            if (System.IO.File.Exists(cand)) { resolved = cand; break; }
                            dir = dir.Parent;
                        }
                    }
                }
                rsa = RsaKeyLoader.LoadPem(resolved);
            }
            return new Microsoft.IdentityModel.Tokens.RsaSecurityKey(rsa)
            {
                KeyId = RsaKeyLoader.ComputeKeyId(rsa)
            };
        });
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<ITokenService, RsaJwtTokenService>();
        services.AddScoped<IEffectivePermissionReader, EffectivePermissionReader>();
        services.AddScoped<IAuthorizationDataProvider, AuthorizationDataProvider>();

        // Sessões (login/refresh/switch)
        services.AddScoped<SessionIssuer>();

        // Contrato read-only para outros módulos
        services.AddScoped<IIdentityReadContract, IdentityReadContract>();

        // IEventBus do módulo = Outbox transacional deste contexto
        services.AddScoped<Worfair.BuildingBlocks.Application.Contracts.IEventBus>(sp =>
            ActivatorUtilities.CreateInstance<Worfair.BuildingBlocks.Infrastructure.Persistence.Outbox.OutboxEventBus<IdentityDbContext>>(sp));

        // Processador do Outbox (entrega at-least-once pós-commit)
        services.AddHostedService<Worfair.BuildingBlocks.Infrastructure.Persistence.Outbox.OutboxProcessorService<IdentityDbContext>>();

        return services;
    }
}
