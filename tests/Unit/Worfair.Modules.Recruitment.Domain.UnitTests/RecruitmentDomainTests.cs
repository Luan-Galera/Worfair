using FluentAssertions;

namespace Worfair.Modules.Recruitment.Domain.UnitTests;

public class MoneyTests
{
    [Fact]
    public void Valor_negativo_e_rejeitado()
    {
        Money.Create(-1m, "BRL").IsFailure.Should().BeTrue();
        Money.Create(-1m, "BRL").Error!.Code.Should().Be(RecruitmentErrors.MoneyNegativeAmount.Code);
    }

    [Fact]
    public void Moeda_normaliza_para_maiusculas()
    {
        var money = Money.Create(1_000m, " brl ").Value;
        money.Currency.Should().Be("BRL");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("BR")]
    [InlineData("BRLS")]
    public void Moeda_invalida_e_rejeitada(string? currency)
    {
        Money.Create(100m, currency).IsFailure.Should().BeTrue();
        Money.Create(100m, currency).Error!.Code.Should().Be(RecruitmentErrors.MoneyInvalidCurrency.Code);
    }
}

public class SalaryRangeTests
{
    private static Money Brl(decimal amount) => Money.Create(amount, "BRL").Value;

    [Fact]
    public void Minimo_maior_que_maximo_e_rejeitado()
    {
        var result = SalaryRange.Create(Brl(5_000m), Brl(3_000m));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(RecruitmentErrors.SalaryRangeMinimumExceedsMaximum.Code);
    }

    [Fact]
    public void Moedas_diferentes_sao_rejeitadas()
    {
        var max = Money.Create(5_000m, "USD").Value;
        var result = SalaryRange.Create(Brl(3_000m), max);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(RecruitmentErrors.SalaryRangeCurrencyMismatch.Code);
    }

    [Fact]
    public void Sem_maximo_significa_a_combinar()
    {
        var range = SalaryRange.Create(Brl(3_000m)).Value;

        range.Maximum.Should().BeNull();
        range.Minimum.Amount.Should().Be(3_000m);
    }

    [Fact]
    public void Igualdade_por_valor()
    {
        var a = SalaryRange.Create(Brl(3_000m), Brl(5_000m)).Value;
        var b = SalaryRange.Create(Brl(3_000m), Brl(5_000m)).Value;

        a.Should().Be(b);
    }
}

public class JobTitleTests
{
    [Fact]
    public void Titulo_obrigatorio()
    {
        JobTitle.Create("  ").IsFailure.Should().BeTrue();
        JobTitle.Create(null).Error!.Code.Should().Be(RecruitmentErrors.JobTitleRequired.Code);
    }

    [Fact]
    public void Titulo_normaliza_espacos_e_limita_tamanho()
    {
        JobTitle.Create(new string('a', 121)).IsFailure.Should().BeTrue();
        JobTitle.Create(new string('a', 121)).Error!.Code.Should().Be(RecruitmentErrors.JobTitleTooLong.Code);

        var title = JobTitle.Create("  Engenheiro(a) de Software Sênior  ").Value;
        title.Value.Should().Be("Engenheiro(a) de Software Sênior");
    }
}

public class ContactEmailTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sem-arroba")]
    [InlineData("a@b")]
    [InlineData("a b@worfair.com")]
    public void Email_invalido_e_rejeitado(string? email)
    {
        ContactEmail.Create(email).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Email_normaliza_para_minusculas()
    {
        var email = ContactEmail.Create("  Candidata@Worfair.COM ").Value;

        email.Value.Should().Be("candidata@worfair.com");
    }
}

public class JobRequisitionTests
{
    private static readonly DateTime Now = new(2026, 8, 24, 12, 0, 0, DateTimeKind.Utc);

    private static Result<JobRequisition> CreateDraft(string description = "Atuação no time de plataforma.")
    {
        var salary = SalaryRange.Create(
            Money.Create(3_000m, "BRL").Value,
            Money.Create(5_000m, "BRL").Value).Value;

        return JobRequisition.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Engenheiro(a) de Software Sênior",
            description,
            salary,
            Now);
    }

    [Fact]
    public void Create_nasce_em_draft_e_emite_evento()
    {
        var result = CreateDraft();

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(JobRequisitionStatus.Draft);
        result.Value.DomainEvents.Should().ContainSingle(e => e is Aggregates.JobRequisition.Events.JobRequisitionCreatedDomainEvent);
    }

    [Fact]
    public void Descricao_obrigatoria()
    {
        var salary = SalaryRange.Create(Money.Create(3_000m, "BRL").Value).Value;

        var result = JobRequisition.Create(null, null, "Dev", "   ", salary, Now);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(RecruitmentErrors.DescriptionRequired.Code);
    }

    [Fact]
    public void Publish_sem_time_de_contratacao_falha()
    {
        var requisition = CreateDraft().Value;

        var publish = requisition.Publish(Now);

        publish.IsFailure.Should().BeTrue();
        publish.Error!.Code.Should().Be(RecruitmentErrors.CannotPublishWithoutTeam.Code);
    }

    [Fact]
    public void Publish_com_time_sucede_e_emite_evento()
    {
        var requisition = CreateDraft().Value;
        requisition.AddTeamMember(Guid.NewGuid(), HiringRole.Recruiter, Now);

        var publish = requisition.Publish(Now.AddMinutes(5));

        publish.IsSuccess.Should().BeTrue();
        requisition.Status.Should().Be(JobRequisitionStatus.Published);
        requisition.PublishedAtUtc.Should().Be(Now.AddMinutes(5));
        requisition.DomainEvents.Should().ContainSingle(e => e is Aggregates.JobRequisition.Events.JobRequisitionPublishedDomainEvent);
    }

    [Fact]
    public void Recrutador_nao_se_repete_no_time()
    {
        var requisition = CreateDraft().Value;
        var recruiter = Guid.NewGuid();
        requisition.AddTeamMember(recruiter, HiringRole.Recruiter, Now);

        var duplicate = requisition.AddTeamMember(recruiter, HiringRole.Sourcer, Now);

        duplicate.IsFailure.Should().BeTrue();
        duplicate.Error!.Code.Should().Be(RecruitmentErrors.DuplicateTeamMember.Code);
    }

    [Fact]
    public void Time_fica_travado_fora_do_rascunho()
    {
        var requisition = CreateDraft().Value;
        requisition.AddTeamMember(Guid.NewGuid(), HiringRole.HiringManager, Now);
        requisition.Publish(Now).Should().NotBeNull();

        var add = requisition.AddTeamMember(Guid.NewGuid(), HiringRole.Sourcer, Now);

        add.IsFailure.Should().BeTrue();
        add.Error!.Code.Should().Be(RecruitmentErrors.TeamLockedAfterPublish.Code);
    }

    [Fact]
    public void Fluxo_publish_pause_resume()
    {
        var requisition = CreateDraft().Value;
        requisition.AddTeamMember(Guid.NewGuid(), HiringRole.Recruiter, Now);
        requisition.Publish(Now);

        requisition.Pause(Now).IsSuccess.Should().BeTrue();
        requisition.Status.Should().Be(JobRequisitionStatus.Paused);
        requisition.Resume(Now).IsSuccess.Should().BeTrue();
        requisition.Status.Should().Be(JobRequisitionStatus.Published);

        requisition.Publish(Now).IsFailure.Should().BeTrue(); // Published → Publish proibido
    }

    [Fact]
    public void Faixa_salarial_imutavel_apos_encerramento()
    {
        var requisition = CreateDraft().Value;
        requisition.Close("Vaga interna preenchida", Now);

        var novaFaixa = SalaryRange.Create(Money.Create(6_000m, "BRL").Value).Value;
        var change = requisition.ChangeSalaryRange(novaFaixa, Now);

        change.IsFailure.Should().BeTrue();
        change.Error!.Code.Should().Be(RecruitmentErrors.CannotChangeClosed.Code);
    }

    [Fact]
    public void Faixa_salarial_alteravel_no_rascunho()
    {
        var requisition = CreateDraft().Value;
        var novaFaixa = SalaryRange.Create(Money.Create(4_500m, "BRL").Value).Value;

        requisition.ChangeSalaryRange(novaFaixa, Now).IsSuccess.Should().BeTrue();
        requisition.SalaryRange.Minimum.Amount.Should().Be(4_500m);
    }

    [Fact]
    public void Close_exige_motivo()
    {
        var requisition = CreateDraft().Value;

        var close = requisition.Close(" ", Now);

        close.IsFailure.Should().BeTrue();
        close.Error!.Code.Should().Be(RecruitmentErrors.CloseReasonRequired.Code);
    }

    [Fact]
    public void Close_de_publicada_sucede_e_emite_evento()
    {
        var requisition = CreateDraft().Value;
        requisition.AddTeamMember(Guid.NewGuid(), HiringRole.Interviewer, Now);
        requisition.Publish(Now);

        var close = requisition.Close("Posição preenchida", Now.AddHours(1));

        close.IsSuccess.Should().BeTrue();
        requisition.Status.Should().Be(JobRequisitionStatus.Closed);
        requisition.ClosedAtUtc.Should().Be(Now.AddHours(1));
        requisition.DomainEvents.Should().ContainSingle(e => e is Aggregates.JobRequisition.Events.JobRequisitionClosedDomainEvent);
    }

    [Fact]
    public void Estados_terminais_nao_permitem_transicoes()
    {
        var closed = CreateDraft().Value;
        closed.Close("fim", Now);
        closed.Cancel("de novo", Now).IsFailure.Should().BeTrue(); // Closed → Cancelled proibido
        closed.Close("outro", Now).IsFailure.Should().BeTrue();
        closed.Pause(Now).IsFailure.Should().BeTrue();

        var cancelled = CreateDraft().Value;
        cancelled.Cancel("cancelada", Now);
        cancelled.Publish(Now).IsFailure.Should().BeTrue();
        cancelled.Cancel("de novo", Now).IsFailure.Should().BeTrue();
    }
}

public class CandidateTests
{
    private static readonly DateTime Now = new(2026, 8, 24, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Recruiter = Guid.NewGuid();

    private static Candidate Sourced() =>
        Candidate.Create("Maria Souza", "maria@email.com", null, CandidateSource.Sourced, utcNow: Now).Value;

    [Fact]
    public void Create_sourced_nasce_sem_evento()
    {
        var candidate = Sourced();

        candidate.Status.Should().Be(CandidateStatus.Sourced);
        candidate.Source.Should().Be(CandidateSource.Sourced);
        candidate.Email.Value.Should().Be("maria@email.com");
        candidate.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Create_applied_emite_candidate_applied()
    {
        var candidate = Candidate.Create(
            "João Lima", "joao@email.com", null, CandidateSource.Applied, utcNow: Now).Value;

        candidate.Status.Should().Be(CandidateStatus.Applied);
        candidate.Source.Should().Be(CandidateSource.Applied);
        candidate.DomainEvents.Should().ContainSingle(e => e is Aggregates.Candidate.Events.CandidateAppliedDomainEvent);
    }

    [Fact]
    public void Nome_e_email_sao_obrigatorios()
    {
        var semNome = Candidate.Create(" ", "a@b.com", null, CandidateSource.Sourced, utcNow: Now);
        var semEmail = Candidate.Create("Maria", " ", null, CandidateSource.Sourced, utcNow: Now);

        semNome.Error!.Code.Should().Be(CandidateErrors.FullNameRequired.Code);
        semEmail.Error!.Code.Should().Be(CandidateErrors.ContactEmailRequired.Code);
    }

    [Theory]
    [InlineData("maria sem arroba")]
    [InlineData("maria@sem-dominio")]
    public void Email_invalido_e_rejeitado(string email)
    {
        Candidate.Create("Maria", email, null, CandidateSource.Sourced, utcNow: Now)
            .Error!.Code.Should().Be(CandidateErrors.ContactEmailInvalid.Code);
    }

    [Fact]
    public void Maquina_de_estados_e_sequencial_estrita()
    {
        var candidate = Sourced();

        // Sourced só avança para Applied (não pula para Screened).
        candidate.Advance(CandidateStatus.Screened, hasScheduledInterview: true, Recruiter, Now)
            .Error!.Code.Should().Be(CandidateErrors.InvalidStatusTransition.Code);

        candidate.Advance(CandidateStatus.Applied, false, Recruiter, Now).IsSuccess.Should().BeTrue();
        candidate.Advance(CandidateStatus.Screened, false, Recruiter, Now).IsSuccess.Should().BeTrue();
        candidate.Advance(CandidateStatus.Offered, false, Recruiter, Now)
            .Error!.Code.Should().Be(CandidateErrors.InvalidStatusTransition.Code); // precisa passar por Interviewing
    }

    [Fact]
    public void Avanco_para_interviewing_exige_entrevista_agendada()
    {
        var candidate = Sourced();
        candidate.Advance(CandidateStatus.Applied, false, Recruiter, Now);
        candidate.Advance(CandidateStatus.Screened, false, Recruiter, Now);

        var semEntrevista = candidate.Advance(CandidateStatus.Interviewing, hasScheduledInterview: false, Recruiter, Now);
        semEntrevista.IsFailure.Should().BeTrue();
        semEntrevista.Error!.Code.Should().Be(CandidateErrors.InterviewRequiredForInterviewing.Code);

        var comEntrevista = candidate.Advance(CandidateStatus.Interviewing, hasScheduledInterview: true, Recruiter, Now);
        comEntrevista.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Cada_transicao_registra_historico()
    {
        var candidate = Sourced();
        candidate.Advance(CandidateStatus.Applied, false, Recruiter, Now);
        candidate.Reject(Recruiter, Now.AddMinutes(2));

        candidate.History.Should().HaveCount(2);
        candidate.History[0].FromStatus.Should().Be(CandidateStatus.Sourced);
        candidate.History[0].ToStatus.Should().Be(CandidateStatus.Applied);
        candidate.History[1].ToStatus.Should().Be(CandidateStatus.Rejected);
        candidate.Status.Should().Be(CandidateStatus.Rejected);
    }

    [Fact]
    public void Contratacao_so_parte_de_offered_e_emite_evento()
    {
        var candidate = Sourced();
        candidate.Advance(CandidateStatus.Applied, false, Recruiter, Now);
        candidate.Advance(CandidateStatus.Screened, false, Recruiter, Now);
        candidate.Advance(CandidateStatus.Interviewing, true, Recruiter, Now);

        // De Interviewing não pode pular direto para Hired.
        candidate.Advance(CandidateStatus.Hired, false, Recruiter, Now)
            .Error!.Code.Should().Be(CandidateErrors.InvalidStatusTransition.Code);

        candidate.Advance(CandidateStatus.Offered, false, Recruiter, Now);
        var hired = candidate.Advance(CandidateStatus.Hired, false, Recruiter, Now);

        hired.IsSuccess.Should().BeTrue();
        candidate.Status.Should().Be(CandidateStatus.Hired);
        candidate.DomainEvents.Should().ContainSingle(e => e is Aggregates.Candidate.Events.CandidateHiredDomainEvent);
    }

    [Fact]
    public void Estados_terminais_nao_avancam_nem_rejeitam()
    {
        var hired = Sourced();
        for (var stage = CandidateStatus.Applied; stage <= CandidateStatus.Hired; stage++)
            hired.Advance(stage, stage == CandidateStatus.Interviewing, Recruiter, Now);

        hired.Advance(CandidateStatus.Offered, false, Recruiter, Now).IsFailure.Should().BeTrue();
        hired.Reject(Recruiter, Now).IsFailure.Should().BeTrue();

        var rejected = Sourced();
        rejected.Reject(Recruiter, Now);
        rejected.Reject(Recruiter, Now).IsFailure.Should().BeTrue();
    }
}

public class InterviewTests
{
    private static readonly DateTime Now = new(2026, 8, 24, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Tomorrow = Now.AddDays(1);

    private static Interview Scheduled() => Interview.Schedule(
        Guid.NewGuid(), Guid.NewGuid(), Tomorrow, 60, InterviewType.Technical, Now).Value;

    [Theory]
    [InlineData(14)]
    [InlineData(481)]
    [InlineData(0)]
    public void Duracao_invalida_e_rejeitada(int minutes)
    {
        var result = Interview.Schedule(Guid.NewGuid(), Guid.NewGuid(), Tomorrow, minutes, InterviewType.Phone, Now);

        result.Error!.Code.Should().Be(InterviewErrors.InvalidDuration.Code);
    }

    [Fact]
    public void Agendamento_no_passado_e_rejeitado()
    {
        var result = Interview.Schedule(
            Guid.NewGuid(), Guid.NewGuid(), Now.AddMinutes(-30), 60, InterviewType.Phone, Now);

        result.Error!.Code.Should().Be(InterviewErrors.ScheduledInPast.Code);
    }

    [Fact]
    public void Requisicao_e_candidato_sao_obrigatorios()
    {
        var semRequisicao = Interview.Schedule(Guid.Empty, Guid.NewGuid(), Tomorrow, 60, InterviewType.Final, Now);
        var semCandidato = Interview.Schedule(Guid.NewGuid(), Guid.Empty, Tomorrow, 60, InterviewType.Final, Now);

        semRequisicao.Error!.Code.Should().Be(InterviewErrors.RequisitionRequired.Code);
        semCandidato.Error!.Code.Should().Be(InterviewErrors.CandidateRequired.Code);
    }

    [Fact]
    public void Feedback_invalido_e_rejeitado()
    {
        var interview = Scheduled();

        interview.AddFeedback(Guid.NewGuid(), 0, "ok", Now)
            .Error!.Code.Should().Be(InterviewErrors.FeedbackRatingInvalid.Code);
        interview.AddFeedback(Guid.NewGuid(), 6, "ok", Now)
            .Error!.Code.Should().Be(InterviewErrors.FeedbackRatingInvalid.Code);
        interview.AddFeedback(Guid.NewGuid(), 5, "  ", Now)
            .Error!.Code.Should().Be(InterviewErrors.FeedbackNotesRequired.Code);
    }

    [Fact]
    public void Mesmo_entrevistador_nao_feedback_duplo()
    {
        var interview = Scheduled();
        var interviewer = Guid.NewGuid();

        interview.AddFeedback(interviewer, 5, "Excelente", Now).IsSuccess.Should().BeTrue();
        interview.AddFeedback(interviewer, 3, "Revisando", Now)
            .Error!.Code.Should().Be(InterviewErrors.FeedbackAlreadySubmitted.Code);
    }

    [Fact]
    public void Complete_sem_feedback_falha_e_com_feedback_sucede()
    {
        var interview = Scheduled();

        interview.Complete(Now)
            .Error!.Code.Should().Be(InterviewErrors.FeedbackRequiredToComplete.Code);

        interview.AddFeedback(Guid.NewGuid(), 4, "Boa comunicação", Now);
        interview.Complete(Tomorrow).IsSuccess.Should().BeTrue();

        interview.Status.Should().Be(InterviewStatus.Completed);
        interview.CompletedAtUtc.Should().Be(Tomorrow);
        interview.AddFeedback(Guid.NewGuid(), 4, "após concluir", Now).IsFailure.Should().BeTrue();
        interview.Cancel(Now).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Cancel_somente_enquanto_agendada()
    {
        var interview = Scheduled();

        interview.Cancel(Now).IsSuccess.Should().BeTrue();
        interview.Status.Should().Be(InterviewStatus.Cancelled);
        interview.CancelledAtUtc.Should().Be(Now);
        interview.Cancel(Now).IsFailure.Should().BeTrue();
        interview.Complete(Now).IsFailure.Should().BeTrue();
    }
}
