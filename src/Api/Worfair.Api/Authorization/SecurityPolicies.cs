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
}
