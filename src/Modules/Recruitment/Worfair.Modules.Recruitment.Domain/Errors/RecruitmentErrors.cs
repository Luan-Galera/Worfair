namespace Worfair.Modules.Recruitment.Domain.Errors;

using Worfair.BuildingBlocks.Domain.Errors;

/// <summary>Erros do agregado JobRequisition e dos VOs compartilhados.</summary>
public static class RecruitmentErrors
{
    public static readonly Error TenantRequired =
        new("JobRequisition.TenantRequired", "A requisição exige um tenant ativo no contexto.");

    public static readonly Error JobTitleRequired =
        new("JobRequisition.JobTitleRequired", "O título da vaga é obrigatório.");

    public static readonly Error JobTitleTooLong =
        new("JobRequisition.JobTitleTooLong", $"O título da vaga aceita até {ValueObjects.JobTitle.MaxLength} caracteres.");

    public static readonly Error DescriptionRequired =
        new("JobRequisition.DescriptionRequired", "A descrição é obrigatória.");

    public static readonly Error NotFound =
        new("JobRequisition.NotFound", "Requisição de vaga não encontrada para este tenant.");

    public static readonly Error InvalidStatusTransition =
        new("JobRequisition.InvalidStatusTransition", "Transição de status não permitida.");

    public static readonly Error CannotPublishWithoutTeam =
        new("JobRequisition.CannotPublishWithoutTeam",
            "A requisição precisa de ao menos um membro do time de contratação.");

    public static readonly Error TeamLockedAfterPublish =
        new("JobRequisition.TeamLockedAfterPublish",
            "O time de contratação só pode ser alterado em rascunho.");

    public static readonly Error DuplicateTeamMember =
        new("JobRequisition.DuplicateTeamMember",
            "Um mesmo recrutador não pode se repetir no time de contratação.");

    public static readonly Error CannotChangeClosed =
        new("JobRequisition.CannotChangeClosed",
            "Uma requisição encerrada ou cancelada não pode ser alterada.");

    public static readonly Error CloseReasonRequired =
        new("JobRequisition.CloseReasonRequired", "Informe o motivo do encerramento.");

    public static readonly Error MoneyNegativeAmount =
        new("Money.NegativeAmount", "O valor não pode ser negativo.");

    public static readonly Error MoneyInvalidCurrency =
        new("Money.InvalidCurrency", "Moeda inválida: informe o código ISO 4217 com 3 letras (ex.: BRL).");

    public static readonly Error SalaryRangeMinimumExceedsMaximum =
        new("SalaryRange.MinimumExceedsMaximum", "O salário mínimo não pode exceder o máximo.");

    public static readonly Error SalaryRangeCurrencyMismatch =
        new("SalaryRange.CurrencyMismatch", "Mínimo e máximo devem usar a mesma moeda.");
}
