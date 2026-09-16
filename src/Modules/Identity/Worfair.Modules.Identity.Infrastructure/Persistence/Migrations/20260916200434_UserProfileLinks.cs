using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Worfair.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UserProfileLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvatarUrl",
                schema: "identity",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PortfolioUrl",
                schema: "identity",
                table: "users",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvatarUrl",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "PortfolioUrl",
                schema: "identity",
                table: "users");
        }
    }
}
