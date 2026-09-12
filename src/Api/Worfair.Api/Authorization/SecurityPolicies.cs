namespace Worfair.Api.Authorization;

/// <summary>Constantes das policies do host (docs/security/03 §4).</summary>
public static class SecurityPolicies
{
    public const string PlatformManageTenants = "platform.tenants.manage";
    public const string PlatformManageUsers = "platform.users.manage";
    public const string MembersManage = "tenants.members.manage";
    public const string SettingsRead = "tenants.settings.read";

    // ── Recruitment (catálogo v1.2 — DB-07) ──
    public const string RequisitionsCreate = "recruitment.requisition.create";
    public const string RequisitionsPublish = "recruitment.requisition.publish";
    public const string RequisitionsClose = "recruitment.requisition.close";
    public const string TeamManage = "recruitment.requisition.team.manage";
    public const string CandidateRead = "recruitment.candidate.read";
    public const string CandidateAdvance = "recruitment.candidate.advance";
    public const string CandidateHire = "recruitment.candidate.hire";
    public const string InterviewSchedule = "recruitment.interview.schedule";
    public const string InterviewFeedback = "recruitment.interview.feedback";

    // ── Marketplace, contratação e financeiro ──
    public const string ProposalSubmit = "proposals.submit";
    public const string ProposalDecide = "proposals.decide";
    public const string ProposalRead = "proposals.read";
    public const string InvoiceIssue = "financial.invoice.issue";
    public const string NotificationRead = "notifications.read";
    public const string MarketplaceRead = "jobs.marketplace.read";
    public const string JobApply = "jobs.project.apply";
    public const string MessageRead = "messages.read";
    public const string MessageSend = "messages.send";
    public const string DisputeOpen = "financial.dispute.open";
    public const string DisputeMediate = "financial.dispute.mediate";
    /// <summary>Gestão de conflitos: exclusiva do SUPER_ADMIN global.</summary>
    public const string DisputeAdmin = "financial.dispute.admin";
    /// <summary>
    /// Leitura de suporte/mediação: só autenticado aqui; cada endpoint confere
    /// no banco se é parte ou super admin (o global não passa em TenantScope).
    /// </summary>
    public const string SupportRead = "support.read";
}
