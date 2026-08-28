namespace Worfair.Modules.Recruitment.Domain.Errors;

using Worfair.BuildingBlocks.Domain.Errors;

/// <summary>Erros do agregado Candidate.</summary>
public static class CandidateErrors
{
    public static readonly Error TenantRequired =
        new("Candidate.TenantRequired", "O candidato exige um tenant ativo no contexto.");

    public static readonly Error NotFound =
        new("Candidate.NotFound", "Candidato não encontrado para este tenant.");

    public static readonly Error FullNameRequired =
        new("Candidate.FullNameRequired", "O nome do candidato é obrigatório.");

    public static Error FieldTooLong(string field, int maxLength) =>
        new("Candidate.FieldTooLong", $"{field} aceita até {maxLength} caracteres.");

    public static readonly Error ContactEmailRequired =
        new("Candidate.ContactEmailRequired", "O e-mail de contato é obrigatório.");

    public static readonly Error ContactEmailInvalid =
        new("Candidate.ContactEmailInvalid", "E-mail de contato inválido.");

    public static readonly Error ContactEmailDuplicated =
        new("Candidate.ContactEmailDuplicated", "Já existe candidato com este e-mail neste tenant.");

    public static readonly Error InvalidStatusTransition =
        new("Candidate.InvalidStatusTransition", "Transição de estágio não permitida pela máquina de estados.");

    public static readonly Error InterviewRequiredForInterviewing =
        new("Candidate.InterviewRequiredForInterviewing",
            "Não é possível avançar para Entrevista sem entrevista agendada.");
}
