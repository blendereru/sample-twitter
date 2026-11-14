using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SampleTwitter.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPostQuoteRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "QuoteId",
                table: "Posts",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Posts_QuoteId",
                table: "Posts",
                column: "QuoteId");

            migrationBuilder.AddForeignKey(
                name: "FK_Posts_Posts_QuoteId",
                table: "Posts",
                column: "QuoteId",
                principalTable: "Posts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Posts_Posts_QuoteId",
                table: "Posts");

            migrationBuilder.DropIndex(
                name: "IX_Posts_QuoteId",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "QuoteId",
                table: "Posts");
        }
    }
}