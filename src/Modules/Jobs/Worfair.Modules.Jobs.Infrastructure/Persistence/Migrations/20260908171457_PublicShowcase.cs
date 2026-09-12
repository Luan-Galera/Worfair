using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Worfair.Modules.Jobs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PublicShowcase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Vitrine pública (docs/database/02): tabelas tenant-scoped ganham
            // RLS + FORCE. Leitura cruza tenants SOMENTE para linhas públicas
            // (vaga Published=2, projeto Open=2); escrita segue restrita ao tenant.
            migrationBuilder.Sql("""
                ALTER TABLE jobs.job_postings ENABLE ROW LEVEL SECURITY;
                ALTER TABLE jobs.job_postings FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS showcase_read ON jobs.job_postings;
                CREATE POLICY showcase_read ON jobs.job_postings FOR SELECT
                USING (tenant_id::text = COALESCE(current_setting('app.tenant_id', true), '')
                    OR status = 2);
                DROP POLICY IF EXISTS tenant_write_ins ON jobs.job_postings;
                CREATE POLICY tenant_write_ins ON jobs.job_postings FOR INSERT
                WITH CHECK (tenant_id::text = COALESCE(current_setting('app.tenant_id', true), ''));
                DROP POLICY IF EXISTS tenant_write_upd ON jobs.job_postings;
                CREATE POLICY tenant_write_upd ON jobs.job_postings FOR UPDATE
                USING (tenant_id::text = COALESCE(current_setting('app.tenant_id', true), ''))
                WITH CHECK (tenant_id::text = COALESCE(current_setting('app.tenant_id', true), ''));
                DROP POLICY IF EXISTS tenant_write_del ON jobs.job_postings;
                CREATE POLICY tenant_write_del ON jobs.job_postings FOR DELETE
                USING (tenant_id::text = COALESCE(current_setting('app.tenant_id', true), ''));

                ALTER TABLE jobs.service_projects ENABLE ROW LEVEL SECURITY;
                ALTER TABLE jobs.service_projects FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS showcase_read ON jobs.service_projects;
                CREATE POLICY showcase_read ON jobs.service_projects FOR SELECT
                USING (tenant_id::text = COALESCE(current_setting('app.tenant_id', true), '')
                    OR status = 2);
                DROP POLICY IF EXISTS tenant_write_ins ON jobs.service_projects;
                CREATE POLICY tenant_write_ins ON jobs.service_projects FOR INSERT
                WITH CHECK (tenant_id::text = COALESCE(current_setting('app.tenant_id', true), ''));
                DROP POLICY IF EXISTS tenant_write_upd ON jobs.service_projects;
                CREATE POLICY tenant_write_upd ON jobs.service_projects FOR UPDATE
                USING (tenant_id::text = COALESCE(current_setting('app.tenant_id', true), ''))
                WITH CHECK (tenant_id::text = COALESCE(current_setting('app.tenant_id', true), ''));
                DROP POLICY IF EXISTS tenant_write_del ON jobs.service_projects;
                CREATE POLICY tenant_write_del ON jobs.service_projects FOR DELETE
                USING (tenant_id::text = COALESCE(current_setting('app.tenant_id', true), ''));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP POLICY IF EXISTS showcase_read ON jobs.job_postings;
                DROP POLICY IF EXISTS tenant_write_ins ON jobs.job_postings;
                DROP POLICY IF EXISTS tenant_write_upd ON jobs.job_postings;
                DROP POLICY IF EXISTS tenant_write_del ON jobs.job_postings;
                ALTER TABLE jobs.job_postings DISABLE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS showcase_read ON jobs.service_projects;
                DROP POLICY IF EXISTS tenant_write_ins ON jobs.service_projects;
                DROP POLICY IF EXISTS tenant_write_upd ON jobs.service_projects;
                DROP POLICY IF EXISTS tenant_write_del ON jobs.service_projects;
                ALTER TABLE jobs.service_projects DISABLE ROW LEVEL SECURITY;
                """);
        }
    }
}
