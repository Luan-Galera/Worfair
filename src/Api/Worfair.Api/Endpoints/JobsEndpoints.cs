namespace Worfair.Api.Endpoints;

using MediatR;
using Worfair.Api.Authorization;
using Worfair.Api.Extensions;
using Worfair.BuildingBlocks.Application.Security;
using Worfair.BuildingBlocks.Domain.ValueObjects;
using Worfair.Modules.Jobs.Application.JobPostings;
using Worfair.Modules.Jobs.Application.JobApplications;
using Worfair.Modules.Jobs.Application.Queries;
using Worfair.Modules.Jobs.Application.ServiceProjects;
using Worfair.Modules.Jobs.Domain.Abstractions;
using Worfair.Modules.Jobs.Domain.Aggregates.JobPosting;
using Worfair.Modules.Jobs.Domain.Aggregates.ServiceProject;

public static class JobsEndpoints
{
    public static IEndpointRouteBuilder MapJobsEndpoints(this IEndpointRouteBuilder app)
    {
        var postings = app.MapGroup("/api/jobs/postings").WithTags("Jobs · Postings");

        // Vitrine pública: listagem de PUBLICADAS em todos os tenants.
        // Parâmetros como string + TryParse: query vazia ("?remote=") não pode
        // derrubar o binding (400) — filtro ausente = sem filtro.
        postings.MapGet(string.Empty,
            async (string? search, string? companyId, string? category, string? remote,
                ISender sender, CancellationToken ct) =>
                (await sender.Send(new ListShowcasePostingsQuery(
                    new ShowcasePostingFilter(
                        search,
                        Guid.TryParse(companyId, out var cid) ? cid : null,
                        category,
                        int.TryParse(remote, out var rem) ? rem : null)), ct)
                    .ConfigureAwait(false)).ToHttpResult())
            .AllowAnonymous()
            .WithName("ListPublishedJobPostings")
            .WithSummary("Vitrine pública (sem login): vagas publicadas de todos os espaços.");

        postings.MapGet("/{jobPostingId:guid}",
            async (Guid jobPostingId, ISender sender, ICurrentUser user, CancellationToken ct) =>
            {
                var result = await sender.Send(new GetShowcasePostingQuery(jobPostingId), ct)
                    .ConfigureAwait(false);
                if (result.IsFailure)
                    return result.ToHttpResult();
                // Rascunhos/encerradas: visíveis só para membros do próprio tenant.
                if (result.Value.Status != (int)JobPostingStatus.Published
                    && (user.TenantId is not { } tenantId || tenantId != result.Value.TenantId))
                    return Results.NotFound();
                return Results.Ok(result.Value);
            })
            .AllowAnonymous()
            .WithName("GetJobPostingById");

        postings.MapPost(string.Empty,
                async (CreateJobPostingCommand command, ISender sender,
                    Worfair.Modules.Tenants.Contracts.ITenancyReadContract tenancyRead,
                    ICurrentUser user, CancellationToken ct) =>
                {
                    // Trava vaga×empresa: além do validador exigir CompanyId, a
                    // empresa precisa existir e estar ATIVA no tenant corrente.
                    if (command.CompanyId is not { } companyId)
                        return Results.BadRequest(new
                        {
                            code = "Jobs.CompanyRequired",
                            message = "Vagas de emprego exigem uma empresa do espaço atual."
                        });
                    if (user.TenantId is not { } tenantId)
                        return Results.Unauthorized();
                    if (!await tenancyRead.CompanyBelongsToTenantAsync(
                            new TenantId(tenantId), companyId, ct).ConfigureAwait(false))
                        return Results.BadRequest(new
                        {
                            code = "Jobs.CompanyRequired",
                            message = "A empresa informada não pertence ao espaço atual."
                        });
                    // Nome da empresa na hora da criação (foto para a vitrine).
                    var companyName = await ResolveCompanyNameAsync(sender, companyId, ct)
                        .ConfigureAwait(false);
                    var enriched = command with
                    {
                        CompanyName = companyName ?? command.CompanyName
                    };
                    return (await sender.Send(enriched, ct).ConfigureAwait(false)).ToHttpResult();
                })
            .RequireAuthorization(SecurityPolicies.RequisitionsCreate)
            .WithName("CreateJobPosting");

        postings.MapPost("/{jobPostingId:guid}/publish",
                async (Guid jobPostingId, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new PublishJobPostingCommand(jobPostingId), ct).ConfigureAwait(false)).ToHttpResult())
            .RequireAuthorization(SecurityPolicies.RequisitionsPublish)
            .WithName("PublishJobPosting");

        postings.MapPost("/{jobPostingId:guid}/applications",
                async (Guid jobPostingId, ApplyToJobRequest request, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new ApplyToJobCommand(jobPostingId, request.ApplicantCompanyId, request.Message), ct)
                        .ConfigureAwait(false)).ToHttpResult())
            .RequireAuthorization(SecurityPolicies.JobApply)
            .WithName("ApplyToJob");

        postings.MapGet("/applications/me",
                async (ISender sender, CancellationToken ct) =>
                    (await sender.Send(new ListMyJobApplicationsQuery(), ct).ConfigureAwait(false)).ToHttpResult())
            .RequireAuthorization(SecurityPolicies.JobApply)
            .WithName("ListMyJobApplications");

        var projects = app.MapGroup("/api/jobs/projects").WithTags("Jobs · Service Projects");

        projects.MapGet(string.Empty,
            async (string? search, string? companyId, string? category,
                string? minBudget, string? maxBudget, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ListShowcaseProjectsQuery(
                    new ShowcaseProjectFilter(
                        search,
                        Guid.TryParse(companyId, out var cid) ? cid : null,
                        category,
                        decimal.TryParse(minBudget, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out var min) ? min : null,
                        decimal.TryParse(maxBudget, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out var max) ? max : null)), ct)
                    .ConfigureAwait(false)).ToHttpResult())
            .AllowAnonymous()
            .WithName("ListOpenServiceProjects")
            .WithSummary("Vitrine pública (sem login): trabalhos abertos de todos os espaços.");

        projects.MapGet("/{serviceProjectId:guid}",
            async (Guid serviceProjectId, ISender sender, ICurrentUser user, CancellationToken ct) =>
            {
                var result = await sender.Send(new GetShowcaseProjectQuery(serviceProjectId), ct)
                    .ConfigureAwait(false);
                if (result.IsFailure)
                    return result.ToHttpResult();
                if (result.Value.Status != (int)ServiceProjectStatus.Open
                    && (user.TenantId is not { } tenantId || tenantId != result.Value.TenantId))
                    return Results.NotFound();
                return Results.Ok(result.Value);
            })
            .AllowAnonymous()
            .WithName("GetServiceProjectById");

        projects.MapPost(string.Empty,
                async (CreateServiceProjectCommand command, ISender sender, CancellationToken ct) =>
                {
                    if (command.CompanyId is { } projectCompanyId)
                    {
                        var projectCompanyName = await ResolveCompanyNameAsync(
                            sender, projectCompanyId, ct).ConfigureAwait(false);
                        command = command with
                        {
                            CompanyName = projectCompanyName ?? command.CompanyName
                        };
                    }
                    return (await sender.Send(command, ct).ConfigureAwait(false)).ToHttpResult();
                })
            .RequireAuthorization(SecurityPolicies.SettingsRead)
            .WithName("CreateServiceProject");

        projects.MapPost("/{serviceProjectId:guid}/open",
                async (Guid serviceProjectId, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new OpenServiceProjectCommand(serviceProjectId), ct).ConfigureAwait(false)).ToHttpResult())
            .RequireAuthorization(SecurityPolicies.SettingsRead)
            .WithName("OpenServiceProject");

        return app;
    }

    private static async Task<string?> ResolveCompanyNameAsync(
        ISender sender, Guid companyId, CancellationToken ct)
    {
        var companies = await sender.Send(
            new Worfair.Modules.Tenants.Application.Queries.ListCompaniesQuery(), ct)
            .ConfigureAwait(false);
        if (companies.IsFailure)
            return null;
        var company = companies.Value.FirstOrDefault(c => c.Id == companyId);
        return company is null
            ? null
            : string.IsNullOrWhiteSpace(company.TradeName) ? company.LegalName : company.TradeName;
    }
}

public sealed record ApplyToJobRequest(Guid? ApplicantCompanyId, string Message);
