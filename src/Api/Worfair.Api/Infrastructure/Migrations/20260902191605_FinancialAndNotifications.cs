using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Worfair.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FinancialAndNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "financial");

            migrationBuilder.CreateTable(
                name: "invoices",
                schema: "financial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_company_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                schema: "financial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    is_read = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "financial",
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

            migrationBuilder.CreateIndex(
                name: "IX_invoices_tenant_id_status",
                schema: "financial",
                table: "invoices",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_tenant_id_user_id_is_read",
                schema: "financial",
                table: "notifications",
                columns: new[] { "tenant_id", "user_id", "is_read" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_pending",
                schema: "financial",
                table: "outbox_messages",
                columns: new[] { "processed_on", "occurred_on" },
                filter: "processed_on IS NULL");

            migrationBuilder.Sql("""
                ALTER TABLE financial.invoices ENABLE ROW LEVEL SECURITY;
                ALTER TABLE financial.invoices FORCE ROW LEVEL SECURITY;
                CREATE POLICY scope_isolation ON financial.invoices
                USING (tenant_id::text = COALESCE(current_setting('app.tenant_id', true), ''))
                WITH CHECK (tenant_id::text = COALESCE(current_setting('app.tenant_id', true), ''));
                ALTER TABLE financial.notifications ENABLE ROW LEVEL SECURITY;
                ALTER TABLE financial.notifications FORCE ROW LEVEL SECURITY;
                CREATE POLICY scope_isolation ON financial.notifications
                USING (tenant_id::text = COALESCE(current_setting('app.tenant_id', true), ''))
                WITH CHECK (tenant_id::text = COALESCE(current_setting('app.tenant_id', true), ''));
                ALTER TABLE financial.outbox_messages ENABLE ROW LEVEL SECURITY;
                ALTER TABLE financial.outbox_messages FORCE ROW LEVEL SECURITY;
                CREATE POLICY scope_isolation ON financial.outbox_messages
                USING (tenant_id IS NULL OR tenant_id::text = COALESCE(current_setting('app.tenant_id', true), ''))
                WITH CHECK (tenant_id IS NULL OR tenant_id::text = COALESCE(current_setting('app.tenant_id', true), ''));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "invoices",
                schema: "financial");

            migrationBuilder.DropTable(
                name: "notifications",
                schema: "financial");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "financial");
        }
    }
}
