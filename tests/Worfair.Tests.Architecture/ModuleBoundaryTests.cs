using FluentAssertions;
using NetArchTest.Rules;

namespace Worfair.Tests.Architecture;

/// <summary>
/// Testes de arquitetura — regras verificáveis em build (docs/architecture/01 §4,
/// docs/architecture/02 §3): fronteiras de módulo e Regra de Dependência.
/// </summary>
public class ModuleBoundaryTests
{
    private const string DomainSuffix = ".Domain";
    private const string ApplicationSuffix = ".Application";
    private const string InfrastructureSuffix = ".Infrastructure";

    [Fact]
    public void Dominio_nao_depende_de_aplicacao_infra_ou_api()
    {
        var result = Types.InAssembly(typeof(Worfair.Modules.Identity.Domain.Aggregates.User.User).Assembly)
            .Should()
            .NotHaveDependencyOnAny(
                "Worfair.Modules.Identity.Application",
                "Worfair.Modules.Identity.Infrastructure",
                "Worfair.Api",
                "MediatR",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Domínio Identity violou dependências: {string.Join(", ", result.FailingTypeNames ?? [])}");

        var tenantsResult = Types.InAssembly(typeof(Worfair.Modules.Tenants.Domain.Aggregates.Tenant.Tenant).Assembly)
            .Should()
            .NotHaveDependencyOnAny(
                "Worfair.Modules.Tenants.Application",
                "Worfair.Modules.Tenants.Infrastructure",
                "Worfair.Api",
                "MediatR",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore")
            .GetResult();

        tenantsResult.IsSuccessful.Should().BeTrue(
            $"Domínio Tenants violou dependências: {string.Join(", ", tenantsResult.FailingTypeNames ?? [])}");

        var recruitmentResult = Types.InAssembly(typeof(Worfair.Modules.Recruitment.Domain.Aggregates.JobRequisition.JobRequisition).Assembly)
            .Should()
            .NotHaveDependencyOnAny(
                "Worfair.Modules.Recruitment.Application",
                "Worfair.Modules.Recruitment.Infrastructure",
                "Worfair.Api",
                "MediatR",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore")
            .GetResult();

        recruitmentResult.IsSuccessful.Should().BeTrue(
            $"Domínio Recruitment violou dependências: {string.Join(", ", recruitmentResult.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Infrastructure_do_identity_nao_referencia_outros_modulos_de_negocio()
    {
        var result = Types.InAssembly(typeof(Worfair.Modules.Identity.Infrastructure.Persistence.IdentityDbContext).Assembly)
            .Should()
            .NotHaveDependencyOnAny(
                "Worfair.Modules.Jobs",
                "Worfair.Modules.Recruitment",
                "Worfair.Modules.Proposals",
                "Worfair.Modules.Financial",
                "Worfair.Modules.Notifications")
            .GetResult();

        // Contratos do núcleo Tenancy (ITenancyReadContract) são permitidos (R-07).
        result.IsSuccessful.Should().BeTrue(
            $"Violação de fronteira: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Infrastructure_do_recruitment_nao_referencia_outros_modulos_de_negocio()
    {
        var result = Types.InAssembly(typeof(Worfair.Modules.Recruitment.Infrastructure.Persistence.RecruitmentDbContext).Assembly)
            .Should()
            .NotHaveDependencyOnAny(
                "Worfair.Modules.Identity",
                "Worfair.Modules.Tenants.Application",
                "Worfair.Modules.Tenants.Domain",
                "Worfair.Modules.Tenants.Infrastructure",
                "Worfair.Modules.Jobs",
                "Worfair.Modules.Proposals",
                "Worfair.Modules.Financial",
                "Worfair.Modules.Notifications")
            .GetResult();

        // Referências cruzadas de leitura usam apenas contratos (R-07); nenhum
        // módulo de negócio é conhecido pela infraestrutura do Recruitment.
        result.IsSuccessful.Should().BeTrue(
            $"Violação de fronteira: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void BuildingBlocks_nao_dependem_de_modulos()
    {
        var domain = Types.InAssembly(typeof(Worfair.BuildingBlocks.Domain.Entities.Entity).Assembly)
            .Should().NotHaveDependencyOnAny("Worfair.Modules", "Worfair.Api").GetResult();

        var application = Types.InAssembly(typeof(Worfair.BuildingBlocks.Application.Cqrs.ICommand<>).Assembly)
            .Should().NotHaveDependencyOnAny("Worfair.Modules", "Worfair.Api").GetResult();

        var infrastructure = Types.InAssembly(typeof(Worfair.BuildingBlocks.Infrastructure.Persistence.UnitOfWork<>).Assembly)
            .Should().NotHaveDependencyOnAny("Worfair.Modules", "Worfair.Api").GetResult();

        var contracts = Types.InAssembly(typeof(Worfair.BuildingBlocks.Contracts.IntegrationEvents.IntegrationEvent).Assembly)
            .Should().NotHaveDependencyOnAny("Worfair.Modules", "Worfair.Api").GetResult();

        domain.IsSuccessful.Should().BeTrue($"BuildingBlocks.Domain: {string.Join(", ", domain.FailingTypeNames ?? [])}");
        application.IsSuccessful.Should().BeTrue($"BuildingBlocks.Application: {string.Join(", ", application.FailingTypeNames ?? [])}");
        infrastructure.IsSuccessful.Should().BeTrue($"BuildingBlocks.Infrastructure: {string.Join(", ", infrastructure.FailingTypeNames ?? [])}");
        contracts.IsSuccessful.Should().BeTrue($"BuildingBlocks.Contracts: {string.Join(", ", contracts.FailingTypeNames ?? [])}");
    }
}
