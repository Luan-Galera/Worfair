using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Worfair.Modules.Tenants.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "tenancy");

            migrationBuilder.CreateTable(
                name: "companies",
                schema: "tenancy",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    trade_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    document = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_companies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "tenancy",
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
                name: "tenant_memberships",
                schema: "tenancy",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    joined_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_memberships", x => new { x.tenant_id, x.user_id });
                });

            migrationBuilder.CreateTable(
                name: "tenant_settings",
                schema: "tenancy",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hiring_workflow = table.Column<string>(type: "jsonb", nullable: true),
                    branding = table.Column<string>(type: "jsonb", nullable: true),
                    feature_flags = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_settings", x => x.tenant_id);
                });

            migrationBuilder.CreateTable(
                name: "tenants",
                schema: "tenancy",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    tier = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    timezone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    locale = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "uq_companies_tenant_document",
                schema: "tenancy",
                table: "companies",
                columns: new[] { "tenant_id", "document" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_pending",
                schema: "tenancy",
                table: "outbox_messages",
                columns: new[] { "processed_on", "occurred_on" },
                filter: "processed_on IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_memberships_user",
                schema: "tenancy",
                table: "tenant_memberships",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "uq_tenants_slug",
                schema: "tenancy",
                table: "tenants",
                column: "slug",
                unique: true);
                        // ─────────────────────────────────────────────────────────────────────
        // SQL normativo (docs/database/02 R-01..R-09 e docs/database/04):
        // CHECKs, RLS ENABLE/FORCE + políticas, índices funcionais.
        // ─────────────────────────────────────────────────────────────────────
        migrationBuilder.Sql(@"
-- R-01: PK técnica já criada pelo EF; garantir UNIQUE (tenant_id, id)
CREATE UNIQUE INDEX IF NOT EXISTS uq_companies_tenant_id ON tenancy.companies (tenant_id, id);
CREATE UNIQUE INDEX IF NOT EXISTS uq_tenant_memberships_tenant_id ON tenancy.tenant_memberships (tenant_id, user_id);
CREATE UNIQUE INDEX IF NOT EXISTS uq_tenant_settings_tenant_id ON tenancy.tenant_settings (tenant_id);

-- R-02: RLS OBRIGATÓRIO + FORCE nas tabelas tenant-owned de tenancy

ALTER TABLE tenancy.companies ENABLE ROW LEVEL SECURITY;
ALTER TABLE tenancy.companies FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON tenancy.companies;
CREATE POLICY tenant_isolation ON tenancy.companies
    FOR ALL
    USING (tenant_id::text = current_setting('app.tenant_id', true))
    WITH CHECK (tenant_id::text = current_setting('app.tenant_id', true));

ALTER TABLE tenancy.tenant_memberships ENABLE ROW LEVEL SECURITY;
ALTER TABLE tenancy.tenant_memberships FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON tenancy.tenant_memberships;
CREATE POLICY tenant_isolation ON tenancy.tenant_memberships
    FOR ALL
    USING (tenant_id::text = current_setting('app.tenant_id', true))
    WITH CHECK (tenant_id::text = current_setting('app.tenant_id', true));

ALTER TABLE tenancy.tenant_settings ENABLE ROW LEVEL SECURITY;
ALTER TABLE tenancy.tenant_settings FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON tenancy.tenant_settings;
CREATE POLICY tenant_isolation ON tenancy.tenant_settings
    FOR ALL
    USING (tenant_id::text = current_setting('app.tenant_id', true))
    WITH CHECK (tenant_id::text = current_setting('app.tenant_id', true));
");
}

        /// <inheritdoc />

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "companies",
                schema: "tenancy");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "tenancy");

            migrationBuilder.DropTable(
                name: "tenant_memberships",
                schema: "tenancy");

            migrationBuilder.DropTable(
                name: "tenant_settings",
                schema: "tenancy");

            migrationBuilder.DropTable(
                name: "tenants",
                schema: "tenancy");
        }
    }
}
