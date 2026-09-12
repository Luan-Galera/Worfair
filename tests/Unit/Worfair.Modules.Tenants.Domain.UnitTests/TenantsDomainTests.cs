using FluentAssertions;
using Worfair.Modules.Tenants.Domain.Aggregates.Company;
using Worfair.Modules.Tenants.Domain.Aggregates.Membership;
using Worfair.Modules.Tenants.Domain.Aggregates.Settings;
using Worfair.Modules.Tenants.Domain.Aggregates.Tenant;
using Worfair.Modules.Tenants.Domain.Errors;
using Worfair.Modules.Tenants.Domain.ValueObjects;

namespace Worfair.Modules.Tenants.Domain.UnitTests;

public class TenantTests
{
    [Fact]
    public void Create_valida_slug_e_emite_evento()
    {
        var result = Tenant.Create("Acme Ltda", "acme-ltda");

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(TenantStatus.Active);
        result.Value.Tier.Should().Be(TenantTier.Standard);
        result.Value.DomainEvents.Should().ContainSingle(e => e is TenantProvisionedDomainEvent);
    }

    [Theory]
    [InlineData("Acme Ltda!")]
    [InlineData("-comeca-hifen")]
    [InlineData("termina-")]
    [InlineData("")]
    public void Create_rejeita_slugs_invalidos(string slug)
    {
        var result = Tenant.Create("Acme", slug);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(TenantErrors.SlugInvalid.Code);
    }

    [Fact]
    public void Create_normaliza_slug_para_minusculas()
    {
        Tenant.Create("Acme", "ACME").Value.Slug.Should().Be("acme");
    }

    [Fact]
    public void Maquina_de_estados_do_tenant()
    {
        var tenant = Tenant.Create("Acme", "acme").Value;

        tenant.Suspend().IsSuccess.Should().BeTrue();
        tenant.Suspend().IsFailure.Should().BeTrue();      // Suspended → Suspended proibido
        tenant.Reactivate().IsSuccess.Should().BeTrue();
        tenant.Cancel().IsSuccess.Should().BeTrue();
        tenant.Cancel().IsFailure.Should().BeTrue();       // terminal
        tenant.Suspend().IsFailure.Should().BeTrue();      // Cancelled → nada mais
    }
}

public class CompanyTests
{
    private static Document Documento() => Document.Create("12.345.678/0001-95").Value;

    [Fact]
    public void Documento_normaliza_para_digitos()
    {
        var doc = Document.Create("123.456.789-09").Value;

        doc.Value.Should().Be("12345678909");
    }

    [Theory]
    [InlineData("123")]
    [InlineData("abcd")]
    [InlineData("")]
    public void Documento_invalido_e_rejeitado(string input)
    {
        Document.Create(input).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Empresa_nasce_ativa_e_pode_desativar()
    {
        var company = Company.Create("Acme Comércio LTDA", "Acme", Documento()).Value;

        company.Status.Should().Be(CompanyStatus.Active);
        company.Activate().IsFailure.Should().BeTrue();     // já ativa
        company.Deactivate().IsSuccess.Should().BeTrue();
        company.Deactivate().IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Razao_social_obrigatoria()
    {
        var result = Company.Create(" ", null, Documento());

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(CompanyErrors.LegalNameRequired.Code);
    }
}

public class MembershipTests
{
    [Fact]
    public void Fluxo_convida_ativa_desativa()
    {
        var membership = TenantMembership.Invite(Guid.NewGuid()).Value;

        membership.Status.Should().Be(MembershipStatus.Invited);

        membership.Activate().IsSuccess.Should().BeTrue();
        membership.Disable().IsSuccess.Should().BeTrue();
        membership.Disable().IsFailure.Should().BeTrue();   // Disabled → Disabled proibido
        membership.Activate().IsSuccess.Should().BeTrue();  // Disabled → Active permitido
    }

    [Fact]
    public void Membership_pode_ser_criada_para_novo_tenant_em_bootstrap_global()
    {
        var tenantId = new TenantId(Guid.NewGuid());

        var membership = TenantMembership.Add(Guid.NewGuid(), tenantId).Value;

        membership.TenantId.Should().Be(tenantId);
        membership.Status.Should().Be(MembershipStatus.Active);
    }
}

public class TenantSettingsTests
{
    [Fact]
    public void Json_invalido_e_recusado()
    {
        var settings = TenantSettings.DefaultFor(new TenantId(Guid.NewGuid()));

        settings.UpdateBranding("{ não é json").IsFailure.Should().BeTrue();
        settings.UpdateFeatureFlags("{\"onboarding\":true}").IsSuccess.Should().BeTrue();
    }
}
