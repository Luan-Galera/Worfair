using FluentAssertions;
using Worfair.Modules.Identity.Domain.Aggregates.User;
using Worfair.Modules.Identity.Domain.Aggregates.UserRole;
using Worfair.Modules.Identity.Domain.Aggregates.Role;
using Worfair.Modules.Identity.Domain.Aggregates.RefreshToken;
using Worfair.Modules.Identity.Domain.Errors;
using Worfair.Modules.Identity.Domain.ValueObjects;

namespace Worfair.Modules.Identity.Domain.UnitTests;

public class EmailTests
{
    [Theory]
    [InlineData("Ana.Silva@Empresa.com", "ana.silva@empresa.com")]
    public void Create_normaliza_para_lower(string input, string expected)
    {
        var result = Email.Create(input);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sem-arroba")]
    [InlineData("a@b")]
    [InlineData("dois @@emails")]
    public void Create_rejeita_emails_invalidos(string? input)
    {
        var result = Email.Create(input);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().BeOneOf(AuthErrors.EmailRequired.Code, AuthErrors.EmailInvalid.Code);
    }
}

public class UserTests
{
    private static User NewUser() =>
        User.Register(Email.Create("ana@empresa.com").Value, "hash-argon2", "Ana Silva").Value;

    [Fact]
    public void Register_emite_evento_e_nasce_ativa()
    {
        var result = User.Register(Email.Create("bruno@empresa.com").Value, "hash", "Bruno Lima");

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(UserStatus.Active);
        result.Value.DomainEvents.Should().ContainSingle(e => e is UserRegisteredDomainEvent);
    }

    [Fact]
    public void Register_sem_nome_falha()
    {
        var result = User.Register(Email.Create("x@y.com").Value, "hash", "  ");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AuthErrors.FullNameRequired);
    }

    [Fact]
    public void Usuario_bloqueado_nao_autentica()
    {
        var user = NewUser();
        user.Lock();

        var auth = user.EnsureCanAuthenticate();

        auth.IsFailure.Should().BeTrue();
        auth.Error.Should().Be(AuthErrors.UserInactive);
    }

    [Fact]
    public void Login_com_sucesso_registra_last_login()
    {
        var user = NewUser();
        var before = DateTime.UtcNow.AddSeconds(-1);

        user.RecordSuccessfulLogin(DateTime.UtcNow);

        user.LastLoginAtUtc.Should().BeOnOrAfter(before);
    }
}

public class UserRoleTests
{
    private static readonly Role TenantRole =
        Role.Create("RECRUITER", "Recrutador", isGlobal: false).Value;

    private static readonly Role GlobalRole =
        Role.Create("SUPER_ADMIN", "Super Administrador", isGlobal: true).Value;

    [Fact]
    public void Role_de_tenant_exige_tenant()
    {
        // role de tenant SEM tenant ⇒ rejeitado (R-04)
        var result = UserRole.Grant(Guid.NewGuid(), TenantRole, tenantId: null, grantedBy: null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AuthErrors.GlobalRoleRequiresPlatformScope);
    }

    [Fact]
    public void Role_global_proibe_tenant()
    {
        var result = UserRole.Grant(
            Guid.NewGuid(), GlobalRole, tenantId: new TenantId(Guid.NewGuid()), grantedBy: null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AuthErrors.GlobalRoleRequiresPlatformScope);
    }

    [Fact]
    public void Multiplas_roles_no_mesmo_tenant_sao_possiveis_R05()
    {
        var userId = Guid.NewGuid();
        var tenantId = new TenantId(Guid.NewGuid());

        var r1 = UserRole.Grant(userId, Role.Create("OWNER", "Proprietário", false).Value, tenantId, null).IsSuccess;
        var r2 = UserRole.Grant(userId, Role.Create("RECRUITER", "Recrutador", false).Value, tenantId, null).IsSuccess;
        var r3 = UserRole.Grant(userId, Role.Create("HIRING_MANAGER", "Gestor", false).Value, tenantId, null).IsSuccess;

        (r1 && r2 && r3).Should().BeTrue();
    }
}

public class RefreshTokenTests
{
    [Fact]
    public void Token_novo_esta_ativo_e_revogacao_invalida()
    {
        var token = RefreshToken.Issue(Guid.NewGuid(), null, "hash", TimeSpan.FromDays(7));

        token.IsActive(DateTime.UtcNow).Should().BeTrue();

        token.Revoke(DateTime.UtcNow);

        token.WasRevoked.Should().BeTrue();
        token.IsActive(DateTime.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void Token_expirado_nao_esta_ativo()
    {
        var token = RefreshToken.Issue(
            Guid.NewGuid(), null, "hash",
            lifetime: TimeSpan.FromDays(-1), // já expirado
            utcNow: DateTime.UtcNow.AddDays(-8));

        token.IsActive(DateTime.UtcNow).Should().BeFalse();
    }
}
