namespace Worfair.Modules.Recruitment.Domain.Errors;

using Worfair.BuildingBlocks.Domain.Errors;

/// <summary>Erros do agregado Interview.</summary>
public static class InterviewErrors
{
    public static readonly Error NotFound =
        new("Interview.NotFound", "Entrevista não encontrada para este tenant.");

    public static readonly Error RequisitionRequired =
        new("Interview.RequisitionRequired", "A entrevista exige a requisição de vaga vinculada.");

    public static readonly Error CandidateRequired =
        new("Interview.CandidateRequired", "A entrevista exige o candidato vinculado.");

    public static readonly Error InvalidDuration =
        new("Interview.InvalidDuration", "A duração deve ficar entre 15 e 480 minutos.");

    public static readonly Error ScheduledInPast =
        new("Interview.ScheduledInPast", "A entrevista não pode ser agendada no passado.");

    public static readonly Error InvalidStatusTransition =
        new("Interview.InvalidStatusTransition", "Transição de status não permitida.");

    public static readonly Error FeedbackRatingInvalid =
        new("Interview.FeedbackRatingInvalid", "A avaliação deve estar entre 1 e 5.");

    public static readonly Error FeedbackNotesRequired =
        new("Interview.FeedbackNotesRequired", "O feedback é obrigatório para concluir a entrevista.");

    public static readonly Error FeedbackAlreadySubmitted =
        new("Interview.FeedbackAlreadySubmitted", "Este entrevistador já registrou feedback nesta entrevista.");

    public static readonly Error FeedbackRequiredToComplete =
        new("Interview.FeedbackRequiredToComplete", "Registre ao menos um feedback antes de concluir a entrevista.");
}
