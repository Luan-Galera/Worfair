namespace Worfair.Api.Modules;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Worfair.Api.Authorization;
using Worfair.BuildingBlocks.Application.Security;
using Worfair.Modules.Identity;
using Worfair.Modules.Jobs;
using Worfair.Modules.Recruitment;
using Worfair.Modules.Tenants;

public static class ModuleRegistrar
{
    /// <summary>Bootstrap: registra TODOS os módulos + segurança do host.</summary>
    public static IServiceCollection AddAllModules(this IServiceCollection services, IConfiguration configuration)
    {
        // Módulos de negócio (composition roots)
        services.AddTenants(configuration);
        services.AddIdentity(configuration);
        services.AddRecruitment(configuration);
        services.AddJobs(configuration);

        services.AddDbContext<Worfair.Api.Infrastructure.FinancialDbContext>((sp, options) =>
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
        services.AddScoped<Worfair.Api.Infrastructure.FinancialUnitOfWork>();

        // Pipeline behaviors do CQRS — registro ÚNICO no host: cada módulo chama
        // AddMediatR separadamente e duplicaria a execução se registrado lá.
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(ModuleRegistrar).Assembly);
            cfg.AddOpenBehavior(typeof(Worfair.BuildingBlocks.Application.Behaviors.LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(Worfair.BuildingBlocks.Application.Behaviors.ValidationBehavior<,>));
        });

        // Building Blocks do host
        services.AddMemoryCache();
        services.AddSingleton<Worfair.BuildingBlocks.Application.Ports.IDateTimeProvider,
            Worfair.BuildingBlocks.Infrastructure.Clock.SystemDateTimeProvider>();
        services.AddSingleton<Worfair.BuildingBlocks.Application.Ports.ICacheService,
            Worfair.BuildingBlocks.Infrastructure.Caching.MemoryCacheService>();
        services.AddScoped<Worfair.BuildingBlocks.Domain.Tenancy.ITenantProvider,
            Worfair.BuildingBlocks.Infrastructure.Tenancy.HttpTenantProvider>();
        services.AddScoped<Worfair.BuildingBlocks.Application.Security.ICurrentUser, CurrentUser>();

        // Domain events → MediatR; Outbox → entrega in-process pós-commit
        services.AddScoped<Worfair.BuildingBlocks.Domain.Abstractions.IDomainEventDispatcher,
            Worfair.BuildingBlocks.Infrastructure.DomainEvents.DomainEventDispatcher>();
        services.AddScoped<Worfair.BuildingBlocks.Infrastructure.Events.InProcessEventBus>();

        return services;
    }

    /// <summary>Autenticação JwtBearer RS256 (docs/security/01 §3).</summary>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var issuer = configuration["Jwt:Issuer"] ?? "https://worfair.local";
        var audience = configuration["Jwt:Audience"] ?? "worfair-web";
        var publicKeyPath = configuration["Jwt:PublicKeyPath"]
            ?? throw new InvalidOperationException("Jwt:PublicKeyPath não configurado.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,

                    // Resolução LAZY: suporta rotação por kid. Sem `using` — RsaSecurityKey mantém referência ao RSA.
                    IssuerSigningKeyResolver = (_, _, kid, _) =>
                    {
                        var rsa = Worfair.Modules.Identity.Infrastructure.Security.RsaKeyLoader.LoadPem(
                            ResolvePublicKeyPath(publicKeyPath));
                        var key = new RsaSecurityKey(rsa) { KeyId = Worfair.Modules.Identity.Infrastructure.Security.RsaKeyLoader.ComputeKeyId(rsa) };
                        return [key];
                    },

                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                    NameClaimType = "sub",
                    RoleClaimType = "roles"
                };
            });

        return services;
    }

    /// <summary>
    /// Resolve o PEM público como a emissão faz (IdentityModule): caminho direto,
    /// CWD ou subida a partir do BaseDirectory — a API roda de CWDs distintos
    /// (raiz do repo × pasta do projeto × container).
    /// </summary>
    private static string ResolvePublicKeyPath(string path)
    {
        if (System.IO.File.Exists(path))
            return path;

        var cwd = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), path);
        if (System.IO.File.Exists(cwd))
            return cwd;

        var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
        while (dir != null)
        {
            var cand = System.IO.Path.Combine(dir.FullName, path);
            if (System.IO.File.Exists(cand))
                return cand;
            dir = dir.Parent;
        }

        return path; // LoadPem lança erro claro com o caminho configurado
    }

    /// <summary>Policies por permissão efetiva (docs/security/03 §4).</summary>
    public static IServiceCollection AddSecurityPolicies(this IServiceCollection services)
    {
        // Registra os handlers de autorização resource-based (revalidam no banco a cada request)
        services.AddScoped<IAuthorizationHandler, Worfair.Api.Authorization.AuthenticatedScopeHandler>();
        services.AddScoped<IAuthorizationHandler, Worfair.Api.Authorization.TenantScopeHandler>();
        services.AddScoped<IAuthorizationHandler, Worfair.Api.Authorization.GlobalScopeHandler>();
        services.AddScoped<IAuthorizationHandler, Worfair.Api.Authorization.PermissionHandler>();

        services.AddAuthorization(options =>
        {
            options.AddPolicy(SecurityPolicies.PlatformManageTenants, p => p
                .RequireAuthenticatedUser()
                .AddRequirements(new AuthenticatedScopeRequirement())
                .AddRequirements(new GlobalScopeRequirement())
                .AddRequirements(new PermissionRequirement("platform.tenants.manage", AccessMode.Global)));

            options.AddPolicy(SecurityPolicies.PlatformManageUsers, p => p
                .RequireAuthenticatedUser()
                .AddRequirements(new AuthenticatedScopeRequirement())
                .AddRequirements(new GlobalScopeRequirement())
                .AddRequirements(new PermissionRequirement("platform.users.manage", AccessMode.Global)));

            options.AddPolicy(SecurityPolicies.MembersManage, p => p
                .RequireAuthenticatedUser()
                .AddRequirements(new AuthenticatedScopeRequirement())
                .AddRequirements(new TenantScopeRequirement())
                .AddRequirements(new PermissionRequirement("tenants.members.manage", AccessMode.Contracting)));

            options.AddPolicy(SecurityPolicies.SettingsRead, p => p
                .RequireAuthenticatedUser()
                .AddRequirements(new AuthenticatedScopeRequirement())
                .AddRequirements(new TenantScopeRequirement())
                .AddRequirements(new PermissionRequirement("tenants.settings.read", AccessMode.Contracting)));

            // ── Recruitment (modo Contratante — docs/security/03 §2) ──
            options.AddPolicy(SecurityPolicies.RequisitionsCreate, p => p
                .RequireAuthenticatedUser()
                .AddRequirements(new AuthenticatedScopeRequirement())
                .AddRequirements(new TenantScopeRequirement())
                .AddRequirements(new PermissionRequirement("recruitment.requisition.create", AccessMode.Contracting)));

            options.AddPolicy(SecurityPolicies.RequisitionsPublish, p => p
                .RequireAuthenticatedUser()
                .AddRequirements(new AuthenticatedScopeRequirement())
                .AddRequirements(new TenantScopeRequirement())
                .AddRequirements(new PermissionRequirement("recruitment.requisition.publish", AccessMode.Contracting)));

            options.AddPolicy(SecurityPolicies.RequisitionsClose, p => p
                .RequireAuthenticatedUser()
                .AddRequirements(new AuthenticatedScopeRequirement())
                .AddRequirements(new TenantScopeRequirement())
                .AddRequirements(new PermissionRequirement("recruitment.requisition.close", AccessMode.Contracting)));

            options.AddPolicy(SecurityPolicies.TeamManage, p => p
                .RequireAuthenticatedUser()
                .AddRequirements(new AuthenticatedScopeRequirement())
                .AddRequirements(new TenantScopeRequirement())
                .AddRequirements(new PermissionRequirement("recruitment.requisition.team.manage", AccessMode.Contracting)));

            options.AddPolicy(SecurityPolicies.CandidateRead, p => p
                .RequireAuthenticatedUser()
                .AddRequirements(new AuthenticatedScopeRequirement())
                .AddRequirements(new TenantScopeRequirement())
                .AddRequirements(new PermissionRequirement("recruitment.candidate.read", AccessMode.Contracting)));

            options.AddPolicy(SecurityPolicies.CandidateAdvance, p => p
                .RequireAuthenticatedUser()
                .AddRequirements(new AuthenticatedScopeRequirement())
                .AddRequirements(new TenantScopeRequirement())
                .AddRequirements(new PermissionRequirement("recruitment.candidate.advance", AccessMode.Contracting)));

            options.AddPolicy(SecurityPolicies.CandidateHire, p => p
                .RequireAuthenticatedUser()
                .AddRequirements(new AuthenticatedScopeRequirement())
                .AddRequirements(new TenantScopeRequirement())
                .AddRequirements(new PermissionRequirement("recruitment.candidate.hire", AccessMode.Contracting)));

            options.AddPolicy(SecurityPolicies.InterviewSchedule, p => p
                .RequireAuthenticatedUser()
                .AddRequirements(new AuthenticatedScopeRequirement())
                .AddRequirements(new TenantScopeRequirement())
                .AddRequirements(new PermissionRequirement("recruitment.interview.schedule", AccessMode.Contracting)));

            options.AddPolicy(SecurityPolicies.InterviewFeedback, p => p
                .RequireAuthenticatedUser()
                .AddRequirements(new AuthenticatedScopeRequirement())
                .AddRequirements(new TenantScopeRequirement())
                .AddRequirements(new PermissionRequirement("recruitment.interview.feedback", AccessMode.Contracting)));

            options.AddPolicy(SecurityPolicies.ProposalSubmit, p => p
                .RequireAuthenticatedUser()
                .AddRequirements(new AuthenticatedScopeRequirement())
                .AddRequirements(new TenantScopeRequirement())
                .AddRequirements(new PermissionRequirement("proposals.submit", AccessMode.Provider)));

            options.AddPolicy(SecurityPolicies.ProposalRead, p => p
                .RequireAuthenticatedUser()
                .AddRequirements(new AuthenticatedScopeRequirement())
                .AddRequirements(new TenantScopeRequirement()));

            options.AddPolicy(SecurityPolicies.ProposalDecide, p => p
                .RequireAuthenticatedUser()
                .AddRequirements(new AuthenticatedScopeRequirement())
                .AddRequirements(new TenantScopeRequirement())
                .AddRequirements(new PermissionRequirement("proposals.decide", AccessMode.Contracting)));

            options.AddPolicy(SecurityPolicies.InvoiceIssue, p => p
                .RequireAuthenticatedUser()
                .AddRequirements(new AuthenticatedScopeRequirement())
                .AddRequirements(new TenantScopeRequirement())
                .AddRequirements(new PermissionRequirement("financial.invoice.issue", AccessMode.Contracting)));

            options.AddPolicy(SecurityPolicies.NotificationRead, p => p
                .RequireAuthenticatedUser()
                .AddRequirements(new AuthenticatedScopeRequirement())
                .AddRequirements(new TenantScopeRequirement()));

            options.AddPolicy(SecurityPolicies.MarketplaceRead, p => p
                .RequireAuthenticatedUser()
                .AddRequirements(new AuthenticatedScopeRequirement())
                .AddRequirements(new TenantScopeRequirement()));

            options.AddPolicy(SecurityPolicies.JobApply, p => p
                .RequireAuthenticatedUser()
                .AddRequirements(new AuthenticatedScopeRequirement())
                .AddRequirements(new TenantScopeRequirement())
                .AddRequirements(new PermissionRequirement("jobs.project.apply", AccessMode.Provider)));

            options.AddPolicy(SecurityPolicies.MessageRead, p => p
                .RequireAuthenticatedUser().AddRequirements(new AuthenticatedScopeRequirement()).AddRequirements(new TenantScopeRequirement()));
            options.AddPolicy(SecurityPolicies.MessageSend, p => p
                .RequireAuthenticatedUser().AddRequirements(new AuthenticatedScopeRequirement()).AddRequirements(new TenantScopeRequirement()));
            options.AddPolicy(SecurityPolicies.DisputeOpen, p => p
                .RequireAuthenticatedUser().AddRequirements(new AuthenticatedScopeRequirement()).AddRequirements(new TenantScopeRequirement()));
            options.AddPolicy(SecurityPolicies.DisputeMediate, p => p
                .RequireAuthenticatedUser().AddRequirements(new AuthenticatedScopeRequirement()).AddRequirements(new TenantScopeRequirement()));
            // Gestão de conflitos: só autenticado aqui; o handler confere
            // SUPER_ADMIN no banco (global não tem tenant p/ TenantScope).
            options.AddPolicy(SecurityPolicies.DisputeAdmin, p => p
                .RequireAuthenticatedUser().AddRequirements(new AuthenticatedScopeRequirement()));
            // Leitura de suporte: idem — o endpoint decide (parte ou super admin).
            options.AddPolicy(SecurityPolicies.SupportRead, p => p
                .RequireAuthenticatedUser().AddRequirements(new AuthenticatedScopeRequirement()));
        });

        return services;
    }
}
