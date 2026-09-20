using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SampleTwitter.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPostRepliesRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Posts_Posts_ReplyId",
                table: "Posts");

            migrationBuilder.AddForeignKey(
                name: "FK_Posts_Posts_ReplyId",
                table: "Posts",
                column: "ReplyId",
                principalTable: "Posts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Posts_Posts_ReplyId",
                table: "Posts");

            migrationBuilder.AddForeignKey(
                name: "FK_Posts_Posts_ReplyId",
                table: "Posts",
                column: "ReplyId",
                principalTable: "Posts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
