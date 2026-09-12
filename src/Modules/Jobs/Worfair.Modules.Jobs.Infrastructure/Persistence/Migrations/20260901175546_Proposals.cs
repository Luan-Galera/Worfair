using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Worfair.Modules.Jobs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Proposals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "proposals",
                schema: "jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_posting_id = table.Column<Guid>(type: "uuid", nullable: true),
                    service_project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    provider_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_company_id = table.Column<Guid>(type: "uuid", nullable: true),
                    message = table.Column<string>(type: "text", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_proposals", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_proposals_tenant_provider",
                schema: "jobs",
                table: "proposals",
                columns: new[] { "tenant_id", "provider_user_id" });

            migrationBuilder.CreateIndex(
                name: "ux_proposals_tenant_posting_provider",
                schema: "jobs",
                table: "proposals",
                columns: new[] { "tenant_id", "job_posting_id", "provider_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_proposals_tenant_project_provider",
                schema: "jobs",
                table: "proposals",
                columns: new[] { "tenant_id", "service_project_id", "provider_user_id" },
                unique: true);

            migrationBuilder.Sql("""
                ALTER TABLE jobs.proposals ENABLE ROW LEVEL SECURITY;
                ALTER TABLE jobs.proposals FORCE ROW LEVEL SECURITY;
                CREATE POLICY scope_isolation ON jobs.proposals
                USING (tenant_id::text = COALESCE(current_setting('app.tenant_id', true), ''))
                WITH CHECK (tenant_id::text = COALESCE(current_setting('app.tenant_id', true), ''));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "proposals",
                schema: "jobs");
        }
    }
}
