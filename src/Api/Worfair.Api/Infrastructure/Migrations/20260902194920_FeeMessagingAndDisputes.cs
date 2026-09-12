using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Worfair.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FeeMessagingAndDisputes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "platform_fee_amount",
                schema: "financial",
                table: "invoices",
                type: "numeric(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "total_amount",
                schema: "financial",
                table: "invoices",
                type: "numeric(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "messages",
                schema: "financial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sender_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "payment_disputes",
                schema: "financial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    opened_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    super_admin_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_disputes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_messages_tenant_id_invoice_id_created_at",
                schema: "financial",
                table: "messages",
                columns: new[] { "tenant_id", "invoice_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_disputes_tenant_id_invoice_id",
                schema: "financial",
                table: "payment_disputes",
                columns: new[] { "tenant_id", "invoice_id" });

            migrationBuilder.Sql("""
                UPDATE financial.invoices
                SET platform_fee_amount = ROUND(amount * 0.15, 2),
                    total_amount = ROUND(amount * 1.15, 2)
                WHERE platform_fee_amount = 0 AND total_amount = 0;

                ALTER TABLE financial.messages ENABLE ROW LEVEL SECURITY;
                ALTER TABLE financial.messages FORCE ROW LEVEL SECURITY;
                CREATE POLICY scope_isolation ON financial.messages
                USING (tenant_id::text = COALESCE(current_setting('app.tenant_id', true), ''))
                WITH CHECK (tenant_id::text = COALESCE(current_setting('app.tenant_id', true), ''));
                ALTER TABLE financial.payment_disputes ENABLE ROW LEVEL SECURITY;
                ALTER TABLE financial.payment_disputes FORCE ROW LEVEL SECURITY;
                CREATE POLICY scope_isolation ON financial.payment_disputes
                USING (tenant_id::text = COALESCE(current_setting('app.tenant_id', true), ''))
                WITH CHECK (tenant_id::text = COALESCE(current_setting('app.tenant_id', true), ''));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "messages",
                schema: "financial");

            migrationBuilder.DropTable(
                name: "payment_disputes",
                schema: "financial");

            migrationBuilder.DropColumn(
                name: "platform_fee_amount",
                schema: "financial",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "total_amount",
                schema: "financial",
                table: "invoices");
        }
    }
}
