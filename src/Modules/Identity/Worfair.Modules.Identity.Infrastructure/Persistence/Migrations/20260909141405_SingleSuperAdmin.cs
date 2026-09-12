using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Worfair.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SingleSuperAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Um ÚNICO super admin na plataforma: só SUPER_ADMIN é global
            // (tenant_id NULL), então unicidade de role global = unicidade de admin.
            // Vale para seed, psql manual e qualquer código futuro.
            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX IF NOT EXISTS uq_single_super_admin
                    ON identity.user_roles (role_id)
                    WHERE tenant_id IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS identity.uq_single_super_admin;");
        }
    }
}
