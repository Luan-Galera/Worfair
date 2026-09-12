namespace Worfair.Modules.Jobs.Domain.Errors;

using Worfair.BuildingBlocks.Domain.Errors;

public static class JobsErrors
{
    public static readonly Error TitleRequired =
        new("Jobs.TitleRequired", "O título é obrigatório.");

    public static readonly Error TitleTooLong =
        new("Jobs.TitleTooLong", "O título deve ter até 200 caracteres.");

    public static readonly Error DescriptionRequired =
        new("Jobs.DescriptionRequired", "A descrição é obrigatória.");

    public static readonly Error CategoryTooLong =
        new("Jobs.CategoryTooLong", "A categoria deve ter até 60 caracteres.");

    public static readonly Error CompanyNameTooLong =
        new("Jobs.CompanyNameTooLong", "O nome da empresa deve ter até 120 caracteres.");

    public static readonly Error CompanyRequired =
        new("Jobs.CompanyRequired", "Vagas de emprego exigem uma empresa do espaço (usuários sem empresa publicam trabalhos freelancer).");

    public static readonly Error InvalidStatusTransition =
        new("Jobs.InvalidStatusTransition", "Transição de status não permitida.");

    public static readonly Error InvalidBudget =
        new("Jobs.InvalidBudget", "O orçamento mínimo não pode ser negativo.");

    public static readonly Error InvalidBudgetRange =
        new("Jobs.InvalidBudgetRange", "O orçamento máximo deve ser maior ou igual ao mínimo.");

    public static readonly Error InvalidCurrency =
        new("Jobs.InvalidCurrency", "Informe uma moeda válida com 3 letras.");

    public static readonly Error NotFound =
        new("Jobs.NotFound", "Registro de Jobs não foi encontrado para este tenant.");

    public static readonly Error JobPostingNotOpen =
        new("Jobs.JobPostingNotOpen", "A vaga não está aberta para candidaturas.");

    public static readonly Error ApplicationMessageRequired =
        new("Jobs.ApplicationMessageRequired", "A mensagem da candidatura é obrigatória.");

    public static readonly Error ApplicationAlreadyExists =
        new("Jobs.ApplicationAlreadyExists", "Já existe uma candidatura deste usuário para esta vaga.");

    public static readonly Error ProposalTargetRequired =
        new("Jobs.ProposalTargetRequired", "Informe uma vaga ou um projeto de serviço.");

    public static readonly Error ProposalSingleTargetRequired =
        new("Jobs.ProposalSingleTargetRequired", "A proposta deve apontar para uma única oportunidade.");

    public static readonly Error ProposalMessageRequired =
        new("Jobs.ProposalMessageRequired", "A mensagem da proposta é obrigatória.");

    public static readonly Error InvalidProposalAmount =
        new("Jobs.InvalidProposalAmount", "O valor da proposta deve ser maior que zero.");

    public static readonly Error InvalidProposalStatus =
        new("Jobs.InvalidProposalStatus", "A proposta não pode mais ser decidida.");

    public static readonly Error ProposalAlreadyExists =
        new("Jobs.ProposalAlreadyExists", "Já existe uma proposta para esta oportunidade.");

    public static readonly Error MessageTargetRequired =
        new("Jobs.MessageTargetRequired", "A mensagem deve estar vinculada a uma vaga ou projeto.");
    public static readonly Error MessageBodyRequired =
        new("Jobs.MessageBodyRequired", "O conteúdo da mensagem é obrigatório.");
    public static readonly Error MessageTooLong =
        new("Jobs.MessageTooLong", "A mensagem deve ter até 2000 caracteres.");
    public static readonly Error DisputeTargetRequired =
        new("Jobs.DisputeTargetRequired", "O conflito deve estar vinculado a uma vaga ou projeto.");
    public static readonly Error DisputeReasonRequired =
        new("Jobs.DisputeReasonRequired", "O motivo do conflito é obrigatório.");
    public static readonly Error InvalidDisputeStatus =
        new("Jobs.InvalidDisputeStatus", "O conflito não pode ser resolvido neste estado.");
}
