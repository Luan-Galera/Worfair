namespace Worfair.Modules.Identity.Domain.Errors;

using Worfair.BuildingBlocks.Domain.Errors;

public static class AuthErrors
{
    public static readonly Error EmailRequired = new("Auth.EmailRequired", "O e-mail é obrigatório.");
    public static readonly Error EmailInvalid = new("Auth.EmailInvalid", "E-mail inválido.");
    public static readonly Error FullNameRequired = new("Auth.FullNameRequired", "O nome completo é obrigatório.");
    public static readonly Error PasswordHashRequired = new("Auth.PasswordHashRequired", "Hash de senha é obrigatório.");
    public static readonly Error PasswordRequired = new("Auth.PasswordRequired", "A senha é obrigatória.");
    public static readonly Error PasswordTooShort = new("Auth.PasswordTooShort", "A senha precisa de no mínimo 8 caracteres.");
    public static readonly Error EmailTaken = new("Auth.EmailTaken", "Já existe uma conta com este e-mail.");
    public static readonly Error InvalidCredentials = new("Auth.InvalidCredentials", "Credenciais inválidas.");
    public static readonly Error UserInactive = new("Auth.UserInactive", "Usuário inativo ou bloqueado.");
    public static readonly Error NoActiveMembership = new("Auth.NoActiveMembership", "Usuário sem membership ativa em nenhum tenant.");
    public static readonly Error MembershipNotFound = new("Auth.MembershipNotFound", "Membership ativa não encontrada no tenant alvo.");
    public static readonly Error TenantInactive = new("Auth.TenantInactive", "Tenant indisponível.");
    public static readonly Error ModeUnavailable = new("Auth.ModeUnavailable", "Modo solicitado não está disponível para este usuário.");
    public static readonly Error ModeConflict = new("Auth.ModeConflict", "Permissões conflitantes impedem a derivação do modo.");
    public static readonly Error RefreshTokenInvalid = new("Auth.RefreshTokenInvalid", "Refresh token inválido ou expirado.");
    public static readonly Error RoleNotFound = new("Auth.RoleNotFound", "Role desconhecida.");
    public static readonly Error RoleAlreadyGranted = new("Auth.RoleAlreadyGranted", "O usuário já possui esta role neste escopo.");
    public static readonly Error GlobalRoleRequiresPlatformScope = new("Auth.GlobalRoleRequiresPlatformScope", "Roles globais só podem ser concedidas em contexto global.");
    public static readonly Error TenantRoleRequiresMembership = new("Auth.TenantRoleRequiresMembership", "Role de tenant exige membership ativa (R-05).");
}
