using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Worfair.Modules.Recruitment.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "recruitment");

            migrationBuilder.CreateTable(
                name: "candidates",
                schema: "recruitment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    source = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    resume_url = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "interviews",
                schema: "recruitment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_requisition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scheduled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    duration_minutes = table.Column<short>(type: "smallint", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_interviews", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "job_requisitions",
                schema: "recruitment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    salary_min = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    salary_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    salary_max = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    salary_max_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    closed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    close_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_requisitions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "recruitment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processed_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "candidate_stage_history",
                schema: "recruitment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<int>(type: "integer", nullable: false),
                    to_status = table.Column<int>(type: "integer", nullable: false),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    changed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_stage_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_candidate_stage_history_candidates_candidate_id",
                        column: x => x.candidate_id,
                        principalSchema: "recruitment",
                        principalTable: "candidates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "interview_feedbacks",
                schema: "recruitment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    interviewer_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rating = table.Column<short>(type: "smallint", nullable: false),
                    notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    interview_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_interview_feedbacks", x => x.id);
                    table.ForeignKey(
                        name: "FK_interview_feedbacks_interviews_interview_id",
                        column: x => x.interview_id,
                        principalSchema: "recruitment",
                        principalTable: "interviews",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "hiring_team_members",
                schema: "recruitment",
                columns: table => new
                {
                    recruiter_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_requisition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<int>(type: "integer", nullable: false),
                    added_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hiring_team_members", x => new { x.job_requisition_id, x.recruiter_user_id });
                    table.ForeignKey(
                        name: "FK_hiring_team_members_job_requisitions_job_requisition_id",
                        column: x => x.job_requisition_id,
                        principalSchema: "recruitment",
                        principalTable: "job_requisitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_candidate_stage_history_candidate",
                schema: "recruitment",
                table: "candidate_stage_history",
                column: "candidate_id");

            migrationBuilder.CreateIndex(
                name: "ix_candidates_tenant_status",
                schema: "recruitment",
                table: "candidates",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_candidates_tenant_user",
                schema: "recruitment",
                table: "candidates",
                columns: new[] { "tenant_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "uq_interview_feedbacks_interview_interviewer",
                schema: "recruitment",
                table: "interview_feedbacks",
                columns: new[] { "interview_id", "interviewer_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_interviews_tenant_candidate",
                schema: "recruitment",
                table: "interviews",
                columns: new[] { "tenant_id", "candidate_id" });

            migrationBuilder.CreateIndex(
                name: "ix_interviews_tenant_date",
                schema: "recruitment",
                table: "interviews",
                columns: new[] { "tenant_id", "scheduled_at" });

            migrationBuilder.CreateIndex(
                name: "ix_job_requisitions_tenant_company",
                schema: "recruitment",
                table: "job_requisitions",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "ix_job_requisitions_tenant_status",
                schema: "recruitment",
                table: "job_requisitions",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_pending",
                schema: "recruitment",
                table: "outbox_messages",
                columns: new[] { "processed_on", "occurred_on" },
                filter: "processed_on IS NULL");

            // ─────────────────────────────────────────────────────────────────────
            // SQL normativo (docs/database/02 R-01..R-09 e docs/database/03 §5):
            // UNIQUEs técnicas, CHECKs, índice funcional de e-mail e
            // RLS ENABLE/FORCE + políticas nas tabelas tenant-owned.
            // ─────────────────────────────────────────────────────────────────────
            migrationBuilder.Sql(@"
-- R-01: PK técnica já criada pelo EF; garantir UNIQUE (tenant_id, id)
CREATE UNIQUE INDEX IF NOT EXISTS uq_job_requisitions_tenant_id ON recruitment.job_requisitions (tenant_id, id);
CREATE UNIQUE INDEX IF NOT EXISTS uq_candidates_tenant_id ON recruitment.candidates (tenant_id, id);
CREATE UNIQUE INDEX IF NOT EXISTS uq_interviews_tenant_id ON recruitment.interviews (tenant_id, id);

-- ContactEmail é único POR TENANT (não global) — docs/architecture/03 §3.2
CREATE UNIQUE INDEX IF NOT EXISTS uq_candidates_tenant_email ON recruitment.candidates (tenant_id, lower(email));

-- CHECKs de domínio (docs/database/03 §5)
ALTER TABLE recruitment.job_requisitions
    ADD CONSTRAINT chk_job_requisitions_status CHECK (status IN (1, 2, 3, 4, 5));
ALTER TABLE recruitment.job_requisitions
    ADD CONSTRAINT chk_job_requisitions_salary_range CHECK (salary_max IS NULL OR salary_min <= salary_max);

ALTER TABLE recruitment.hiring_team_members
    ADD CONSTRAINT chk_hiring_team_members_role CHECK (role IN (1, 2, 3, 4));

ALTER TABLE recruitment.candidates
    ADD CONSTRAINT chk_candidates_source CHECK (source IN (1, 2));
ALTER TABLE recruitment.candidates
    ADD CONSTRAINT chk_candidates_status CHECK (status IN (1, 2, 3, 4, 5, 6, 7));

ALTER TABLE recruitment.interviews
    ADD CONSTRAINT chk_interviews_type CHECK (type IN (1, 2, 3, 4));
ALTER TABLE recruitment.interviews
    ADD CONSTRAINT chk_interviews_status CHECK (status IN (1, 2, 3));
ALTER TABLE recruitment.interviews
    ADD CONSTRAINT chk_interviews_duration CHECK (duration_minutes > 0);

ALTER TABLE recruitment.interview_feedbacks
    ADD CONSTRAINT chk_interview_feedbacks_rating CHECK (rating BETWEEN 1 AND 5);

-- R-02: RLS OBRIGATÓRIO + FORCE nas tabelas tenant-owned de recruitment
ALTER TABLE recruitment.job_requisitions ENABLE ROW LEVEL SECURITY;
ALTER TABLE recruitment.job_requisitions FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON recruitment.job_requisitions;
CREATE POLICY tenant_isolation ON recruitment.job_requisitions
    FOR ALL
    USING (tenant_id::text = current_setting('app.tenant_id', true))
    WITH CHECK (tenant_id::text = current_setting('app.tenant_id', true));

ALTER TABLE recruitment.hiring_team_members ENABLE ROW LEVEL SECURITY;
ALTER TABLE recruitment.hiring_team_members FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON recruitment.hiring_team_members;
CREATE POLICY tenant_isolation ON recruitment.hiring_team_members
    FOR ALL
    USING (tenant_id::text = current_setting('app.tenant_id', true))
    WITH CHECK (tenant_id::text = current_setting('app.tenant_id', true));

ALTER TABLE recruitment.candidates ENABLE ROW LEVEL SECURITY;
ALTER TABLE recruitment.candidates FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON recruitment.candidates;
CREATE POLICY tenant_isolation ON recruitment.candidates
    FOR ALL
    USING (tenant_id::text = current_setting('app.tenant_id', true))
    WITH CHECK (tenant_id::text = current_setting('app.tenant_id', true));

ALTER TABLE recruitment.candidate_stage_history ENABLE ROW LEVEL SECURITY;
ALTER TABLE recruitment.candidate_stage_history FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON recruitment.candidate_stage_history;
CREATE POLICY tenant_isolation ON recruitment.candidate_stage_history
    FOR ALL
    USING (tenant_id::text = current_setting('app.tenant_id', true))
    WITH CHECK (tenant_id::text = current_setting('app.tenant_id', true));

ALTER TABLE recruitment.interviews ENABLE ROW LEVEL SECURITY;
ALTER TABLE recruitment.interviews FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON recruitment.interviews;
CREATE POLICY tenant_isolation ON recruitment.interviews
    FOR ALL
    USING (tenant_id::text = current_setting('app.tenant_id', true))
    WITH CHECK (tenant_id::text = current_setting('app.tenant_id', true));

ALTER TABLE recruitment.interview_feedbacks ENABLE ROW LEVEL SECURITY;
ALTER TABLE recruitment.interview_feedbacks FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON recruitment.interview_feedbacks;
CREATE POLICY tenant_isolation ON recruitment.interview_feedbacks
    FOR ALL
    USING (tenant_id::text = current_setting('app.tenant_id', true))
    WITH CHECK (tenant_id::text = current_setting('app.tenant_id', true));
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TABLE IF EXISTS recruitment.interview_feedbacks CASCADE;
DROP TABLE IF EXISTS recruitment.candidate_stage_history CASCADE;
DROP TABLE IF EXISTS recruitment.hiring_team_members CASCADE;
DROP TABLE IF EXISTS recruitment.interviews CASCADE;
DROP TABLE IF EXISTS recruitment.candidates CASCADE;
DROP TABLE IF EXISTS recruitment.job_requisitions CASCADE;
DROP TABLE IF EXISTS recruitment.outbox_messages CASCADE;
");
        }
    }
}
