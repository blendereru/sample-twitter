using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SampleTwitter.API.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageReadAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReadAt",
                table: "Messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Messages" AS m
                SET "ReadAt" = CASE
                    WHEN m."SenderId" = c."ParticipantAId" THEN c."ParticipantBLastReadAt"
                    ELSE c."ParticipantALastReadAt"
                END
                FROM "Conversations" AS c
                WHERE m."ConversationId" = c."Id"
                  AND m."ReadAt" IS NULL
                  AND (
                    (m."SenderId" = c."ParticipantAId" AND c."ParticipantBLastReadAt" IS NOT NULL AND m."SentAt" <= c."ParticipantBLastReadAt")
                    OR
                    (m."SenderId" = c."ParticipantBId" AND c."ParticipantALastReadAt" IS NOT NULL AND m."SentAt" <= c."ParticipantALastReadAt")
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReadAt",
                table: "Messages");
        }
    }
}