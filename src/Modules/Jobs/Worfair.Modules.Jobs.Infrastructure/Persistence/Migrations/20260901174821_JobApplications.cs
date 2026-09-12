using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Worfair.Modules.Jobs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class JobApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "job_applications",
                schema: "jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_posting_id = table.Column<Guid>(type: "uuid", nullable: false),
                    applicant_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    applicant_company_id = table.Column<Guid>(type: "uuid", nullable: true),
                    message = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_applications", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_job_applications_tenant_applicant",
                schema: "jobs",
                table: "job_applications",
                columns: new[] { "tenant_id", "applicant_user_id" });

            migrationBuilder.CreateIndex(
                name: "ux_job_applications_tenant_posting_applicant",
                schema: "jobs",
                table: "job_applications",
                columns: new[] { "tenant_id", "job_posting_id", "applicant_user_id" },
                unique: true);

            migrationBuilder.Sql("""
                ALTER TABLE jobs.job_applications ENABLE ROW LEVEL SECURITY;
                ALTER TABLE jobs.job_applications FORCE ROW LEVEL SECURITY;
                CREATE POLICY scope_isolation ON jobs.job_applications
                USING (tenant_id::text = COALESCE(current_setting('app.tenant_id', true), ''))
                WITH CHECK (tenant_id::text = COALESCE(current_setting('app.tenant_id', true), ''));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "job_applications",
                schema: "jobs");
        }
    }
}
