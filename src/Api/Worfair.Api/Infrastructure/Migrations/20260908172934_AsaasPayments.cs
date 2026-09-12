using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Worfair.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AsaasPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "asaas_payments",
                schema: "financial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    asaas_payment_id = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    asaas_customer_id = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    billing_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    charged_value = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    checkout_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_synced_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_asaas_payments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "asaas_webhook_events",
                schema: "financial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    @event = table.Column<string>(name: "event", type: "character varying(60)", maxLength: 60, nullable: false),
                    asaas_payment_id = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    payment_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payload = table.Column<string>(type: "text", nullable: false),
                    received_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_asaas_webhook_events", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_asaas_payments_asaas_payment_id",
                schema: "financial",
                table: "asaas_payments",
                column: "asaas_payment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_asaas_payments_invoice_id",
                schema: "financial",
                table: "asaas_payments",
                column: "invoice_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_asaas_webhook_events_event_asaas_payment_id_payment_status",
                schema: "financial",
                table: "asaas_webhook_events",
                columns: new[] { "event", "asaas_payment_id", "payment_status" },
                unique: true);

            migrationBuilder.Sql("""
                ALTER TABLE financial.asaas_payments ENABLE ROW LEVEL SECURITY;
                ALTER TABLE financial.asaas_payments FORCE ROW LEVEL SECURITY;
                CREATE POLICY scope_isolation ON financial.asaas_payments
                USING (tenant_id::text = COALESCE(current_setting('app.tenant_id', true), ''))
                WITH CHECK (tenant_id::text = COALESCE(current_setting('app.tenant_id', true), ''));
                ALTER TABLE financial.asaas_webhook_events ENABLE ROW LEVEL SECURITY;
                ALTER TABLE financial.asaas_webhook_events FORCE ROW LEVEL SECURITY;
                CREATE POLICY scope_isolation ON financial.asaas_webhook_events
                USING (tenant_id::text = COALESCE(current_setting('app.tenant_id', true), ''))
                WITH CHECK (tenant_id::text = COALESCE(current_setting('app.tenant_id', true), ''));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "asaas_payments",
                schema: "financial");

            migrationBuilder.DropTable(
                name: "asaas_webhook_events",
                schema: "financial");
        }
    }
}
