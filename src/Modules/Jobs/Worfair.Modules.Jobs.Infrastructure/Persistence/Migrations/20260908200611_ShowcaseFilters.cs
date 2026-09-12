using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Worfair.Modules.Jobs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ShowcaseFilters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "category",
                schema: "jobs",
                table: "service_projects",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "company_name",
                schema: "jobs",
                table: "service_projects",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "category",
                schema: "jobs",
                table: "job_postings",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "company_name",
                schema: "jobs",
                table: "job_postings",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_service_projects_tenant_category",
                schema: "jobs",
                table: "service_projects",
                columns: new[] { "tenant_id", "category" });

            migrationBuilder.CreateIndex(
                name: "ix_job_postings_tenant_category",
                schema: "jobs",
                table: "job_postings",
                columns: new[] { "tenant_id", "category" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_service_projects_tenant_category",
                schema: "jobs",
                table: "service_projects");

            migrationBuilder.DropIndex(
                name: "ix_job_postings_tenant_category",
                schema: "jobs",
                table: "job_postings");

            migrationBuilder.DropColumn(
                name: "category",
                schema: "jobs",
                table: "service_projects");

            migrationBuilder.DropColumn(
                name: "company_name",
                schema: "jobs",
                table: "service_projects");

            migrationBuilder.DropColumn(
                name: "category",
                schema: "jobs",
                table: "job_postings");

            migrationBuilder.DropColumn(
                name: "company_name",
                schema: "jobs",
                table: "job_postings");
        }
    }
}
