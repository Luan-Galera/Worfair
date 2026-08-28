namespace Worfair.Modules.Tenants.Domain.Errors;

using Worfair.BuildingBlocks.Domain.Errors;

public static class TenantErrors
{
    public static readonly Error NotFound =
        new("Tenant.NotFound", "Tenant não encontrado.");

    public static readonly Error NameRequired =
        new("Tenant.NameRequired", "O nome do tenant é obrigatório.");

    public static Error NameTooLong(int maxLength) =>
        new("Tenant.NameTooLong", $"O nome do tenant aceita até {maxLength} caracteres.");

    public static readonly Error SlugInvalid =
        new("Tenant.SlugInvalid", "Slug inválido: use minúsculas, números e hífens (ex.: acme-ltda).");

    public static readonly Error SlugTaken =
        new("Tenant.SlugTaken", "Já existe um tenant com este slug.");

    public static readonly Error InvalidStatusTransition =
        new("Tenant.InvalidStatusTransition", "Transição de status não permitida.");
}

public static class CompanyErrors
{
    public static readonly Error NotFound =
        new("Company.NotFound", "Empresa não encontrada para este tenant.");

    public static readonly Error TenantRequired =
        new("Company.TenantRequired", "A empresa exige um tenant ativo no contexto.");

    public static readonly Error LegalNameRequired =
        new("Company.LegalNameRequired", "A razão social é obrigatória.");

    public static Error FieldTooLong(string field, int maxLength) =>
        new("Company.FieldTooLong", $"{field} aceita até {maxLength} caracteres.");

    public static readonly Error DocumentInvalid =
        new("Company.DocumentInvalid", "Documento inválido: informe CPF (11) ou CNPJ (14) dígitos.");

    public static readonly Error DocumentDuplicated =
        new("Company.DocumentDuplicated", "Este documento já está cadastrado neste tenant.");

    public static readonly Error InvalidStatusTransition =
        new("Company.InvalidStatusTransition", "Transição de status não permitida.");
}

public static class MembershipErrors
{
    public static readonly Error NotFound =
        new("Membership.NotFound", "Membership não encontrada neste tenant.");

    public static readonly Error TenantRequired =
        new("Membership.TenantRequired", "Membership exige um tenant no contexto.");

    public static readonly Error AlreadyMember =
        new("Membership.AlreadyMember", "O usuário já é membro deste tenant.");

    public static readonly Error InvalidStatusTransition =
        new("Membership.InvalidStatusTransition", "Transição de status não permitida.");
}
