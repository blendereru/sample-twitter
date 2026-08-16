using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SampleTwitter.API.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueConstraintsAndCascadeDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmailConfirmationTokens_UserId",
                table: "EmailConfirmationTokens");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailConfirmationTokens_TokenHash",
                table: "EmailConfirmationTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailConfirmationTokens_UserId_UsedAt",
                table: "EmailConfirmationTokens",
                columns: new[] { "UserId", "UsedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_EmailConfirmationTokens_TokenHash",
                table: "EmailConfirmationTokens");

            migrationBuilder.DropIndex(
                name: "IX_EmailConfirmationTokens_UserId_UsedAt",
                table: "EmailConfirmationTokens");

            migrationBuilder.CreateIndex(
                name: "IX_EmailConfirmationTokens_UserId",
                table: "EmailConfirmationTokens",
                column: "UserId");
        }
    }
}
