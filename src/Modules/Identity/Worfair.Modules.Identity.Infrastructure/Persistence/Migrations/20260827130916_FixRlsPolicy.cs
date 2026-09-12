using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Worfair.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixRlsPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP POLICY IF EXISTS scope_isolation ON identity.user_roles;
CREATE POLICY scope_isolation ON identity.user_roles
    FOR ALL
    USING ((tenant_id::text = current_setting('app.tenant_id', true))
        OR (tenant_id IS NULL AND COALESCE(current_setting('app.tenant_id', true), '') = ''))
    WITH CHECK ((tenant_id::text = current_setting('app.tenant_id', true))
        OR (tenant_id IS NULL AND COALESCE(current_setting('app.tenant_id', true), '') = ''));");

            migrationBuilder.Sql(@"
DROP POLICY IF EXISTS scope_isolation ON identity.refresh_tokens;
CREATE POLICY scope_isolation ON identity.refresh_tokens
    FOR ALL
    USING ((tenant_id::text = current_setting('app.tenant_id', true))
        OR (tenant_id IS NULL AND COALESCE(current_setting('app.tenant_id', true), '') = ''))
    WITH CHECK ((tenant_id::text = current_setting('app.tenant_id', true))
        OR (tenant_id IS NULL AND COALESCE(current_setting('app.tenant_id', true), '') = ''));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP POLICY IF EXISTS scope_isolation ON identity.user_roles;
CREATE POLICY scope_isolation ON identity.user_roles
    FOR ALL
    USING ((tenant_id::text = current_setting('app.tenant_id', true))
        OR (tenant_id IS NULL AND current_setting('app.tenant_id', true) IS NULL))
    WITH CHECK ((tenant_id::text = current_setting('app.tenant_id', true))
        OR (tenant_id IS NULL AND current_setting('app.tenant_id', true) IS NULL));");

            migrationBuilder.Sql(@"
DROP POLICY IF EXISTS scope_isolation ON identity.refresh_tokens;
CREATE POLICY scope_isolation ON identity.refresh_tokens
    FOR ALL
    USING ((tenant_id::text = current_setting('app.tenant_id', true))
        OR (tenant_id IS NULL AND current_setting('app.tenant_id', true) IS NULL))
    WITH CHECK ((tenant_id::text = current_setting('app.tenant_id', true))
        OR (tenant_id IS NULL AND current_setting('app.tenant_id', true) IS NULL));");
        }
    }
}
