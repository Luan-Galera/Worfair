namespace Worfair.Api.Extensions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Seed do SUPER_ADMIN inicial (substitui o antigo "primeiro usuário vira admin",
/// removido por segurança): cria o admin SOMENTE quando ainda não existe nenhum
/// (o banco impõe unicidade via índice parcial uq_single_super_admin).
/// O /register NUNCA concede SUPER_ADMIN.
/// </summary>
public static class AdminSeedExtensions
{
    public static async Task SeedInitialAdminAsync(this WebApplication app, IServiceScope scope)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<WebApplication>>();
        var email = app.Configuration["AdminSeed:Email"] ?? app.Configuration["AdminSeed__Email"];
        var password = app.Configuration["AdminSeed:Password"] ?? app.Configuration["AdminSeed__Password"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return; // sem seed configurado: nada a fazer

        if (password.Length < 12)
        {
            logger.LogWarning("Seed de admin ignorado: AdminSeed__Password precisa de ao menos 12 caracteres.");
            return;
        }

        var db = scope.ServiceProvider.GetRequiredService<
            Worfair.Modules.Identity.Infrastructure.Persistence.IdentityDbContext>();

        var superAdminRoleId = await db.Roles
            .Where(r => r.Code == "SUPER_ADMIN")
            .Select(r => r.Id)
            .FirstOrDefaultAsync().ConfigureAwait(false);
        if (superAdminRoleId == Guid.Empty)
        {
            logger.LogWarning("Seed de admin ignorado: role SUPER_ADMIN não encontrada.");
            return;
        }

        // Já existe um super admin? O índice uq_single_super_admin impediria o
        // segundo de qualquer forma; aqui só evitamos tentativa e log inúteis.
        var adminExists = await db.UserRoles
            .AnyAsync(ur => ur.RoleIdValue == superAdminRoleId && ur.TenantId == null)
            .ConfigureAwait(false);
        if (adminExists)
            return;

        var emailResult = Worfair.Modules.Identity.Domain.ValueObjects.Email.Create(email.Trim());
        if (emailResult.IsFailure)
        {
            logger.LogWarning("Seed de admin ignorado: e-mail inválido.");
            return;
        }

        var hasher = scope.ServiceProvider.GetRequiredService<
            Worfair.Modules.Identity.Application.Ports.IPasswordHasher>();

        var createResult = Worfair.Modules.Identity.Domain.Aggregates.User.User.Register(
            emailResult.Value,
            hasher.Hash(password),
            "Administrador",
            autoVerifyEmail: true,
            userType: Worfair.Modules.Identity.Domain.Aggregates.User.AccountType.Individual,
            document: new string('0', 11),
            phone: null);
        if (createResult.IsFailure)
        {
            logger.LogWarning("Seed de admin ignorado: {Error}", createResult.Error!.Message);
            return;
        }

        var user = createResult.Value;
        await db.Users.AddAsync(user).ConfigureAwait(false);

        var superAdmin = await db.Roles
            .FirstOrDefaultAsync(r => r.Id == superAdminRoleId).ConfigureAwait(false);
        if (superAdmin is null)
        {
            logger.LogWarning("Seed de admin ignorado: role SUPER_ADMIN não encontrada.");
            return;
        }

        var grant = Worfair.Modules.Identity.Domain.Aggregates.UserRole.UserRole.Grant(
            user.Id, superAdmin, tenantId: null, grantedBy: null);
        if (grant.IsFailure)
        {
            logger.LogWarning("Seed de admin ignorado: {Error}", grant.Error!.Message);
            return;
        }

        await db.UserRoles.AddAsync(grant.Value).ConfigureAwait(false);
        await db.SaveChangesAsync().ConfigureAwait(false);

        logger.LogInformation("SUPER_ADMIN inicial criado ({Email}). Remova as chaves AdminSeed__* do ambiente.", emailResult.Value.Value);
    }
}
