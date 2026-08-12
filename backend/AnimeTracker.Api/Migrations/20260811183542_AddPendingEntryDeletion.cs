using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AnimeTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingEntryDeletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PendingEntryDeletions",
                columns: table => new
                {
                    AnimeId = table.Column<int>(type: "integer", nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingEntryDeletions", x => x.AnimeId);
                    table.ForeignKey(
                        name: "FK_PendingEntryDeletions_AnimeMetadata_AnimeId",
                        column: x => x.AnimeId,
                        principalTable: "AnimeMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PendingEntryDeletions");
        }
    }
}
