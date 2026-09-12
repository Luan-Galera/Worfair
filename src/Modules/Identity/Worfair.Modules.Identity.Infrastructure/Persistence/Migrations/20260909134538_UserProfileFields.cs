using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Worfair.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UserProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "document",
                schema: "identity",
                table: "users",
                type: "character varying(14)",
                maxLength: 14,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "phone",
                schema: "identity",
                table: "users",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "user_type",
                schema: "identity",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "avatar_url",
                schema: "identity",
                table: "users",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "portfolio_url",
                schema: "identity",
                table: "users",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "document",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "phone",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "user_type",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "avatar_url",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "portfolio_url",
                schema: "identity",
                table: "users");
        }
    }
}
