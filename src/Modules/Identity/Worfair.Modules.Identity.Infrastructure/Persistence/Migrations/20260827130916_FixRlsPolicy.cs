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
DO \$\$ BEGIN
    IF EXISTS (SELECT 1 FROM pg_policy WHERE polname = 'scope_isolation' AND polrelid = 'user_roles'::regclass) THEN
        ALTER TABLE identity.user_roles DROP POLICY scope_isolation ON identity.user_roles;
    END IF;
    CREATE POLICY scope_isolation ON identity.user_roles
        FOR ALL
        USING ((tenant_id::text = current_setting('app.tenant_id', true))
            OR (tenant_id IS NULL AND COALESCE(current_setting('app.tenant_id', true), '') = ''))
        WITH CHECK ((tenant_id::text = current_setting('app.tenant_id', true))
            OR (tenant_id IS NULL AND COALESCE(current_setting('app.tenant_id', true), '') = ''));
END \$\$;");

            migrationBuilder.Sql(@"
DO \$\$ BEGIN
    IF EXISTS (SELECT 1 FROM pg_policy WHERE polname = 'scope_isolation' AND polrelid = 'refresh_tokens'::regclass) THEN
        ALTER TABLE identity.refresh_tokens DROP POLICY scope_isolation ON identity.refresh_tokens;
    END IF;
    CREATE POLICY scope_isolation ON identity.refresh_tokens
        FOR ALL
        USING ((tenant_id::text = current_setting('app.tenant_id', true))
            OR (tenant_id IS NULL AND COALESCE(current_setting('app.tenant_id', true), '') = ''))
        WITH CHECK ((tenant_id::text = current_setting('app.tenant_id', true))
            OR (tenant_id IS NULL AND COALESCE(current_setting('app.tenant_id', true), '') = ''));
END \$\$;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO \$\$ BEGIN
    IF EXISTS (SELECT 1 FROM pg_policy WHERE polname = 'scope_isolation' AND polrelid = 'user_roles'::regclass) THEN
        ALTER TABLE identity.user_roles DROP POLICY scope_isolation ON identity.user_roles;
    END IF;
    CREATE POLICY scope_isolation ON identity.user_roles
        FOR ALL
        USING ((tenant_id::text = current_setting('app.tenant_id', true))
            OR (tenant_id IS NULL AND current_setting('app.tenant_id', true) IS NULL))
        WITH CHECK ((tenant_id::text = current_setting('app.tenant_id', true))
            OR (tenant_id IS NULL AND current_setting('app.tenant_id', true) IS NULL));
END \$\$;");

            migrationBuilder.Sql(@"
DO \$\$ BEGIN
    IF EXISTS (SELECT 1 FROM pg_policy WHERE polname = 'scope_isolation' AND polrelid = 'refresh_tokens'::regclass) THEN
        ALTER TABLE identity.refresh_tokens DROP POLICY scope_isolation ON identity.refresh_tokens;
    END IF;
    CREATE POLICY scope_isolation ON identity.refresh_tokens
        FOR ALL
        USING ((tenant_id::text = current_setting('app.tenant_id', true))
            OR (tenant_id IS NULL AND current_setting('app.tenant_id', true) IS NULL))
        WITH CHECK ((tenant_id::text = current_setting('app.tenant_id', true))
            OR (tenant_id IS NULL AND current_setting('app.tenant_id', true) IS NULL));
END \$\$;");
        }
    }
}
