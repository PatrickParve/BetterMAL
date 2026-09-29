using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AnimeTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSetupStateAndDropAiringBackfill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BackfillCompletedAtUtc",
                table: "AiringRefreshStates");

            migrationBuilder.CreateTable(
                name: "SetupStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SetupStates", x => x.Id);
                });

            // An install that already holds a login and at least one list
            // entry is set up already: mark it finished now, so it opens
            // straight into the app and never sees the first-run setup
            // (add-first-run-setup design D3). Any other install — a fresh
            // one, or a connected one with an empty list — gets no row, so
            // it goes through setup. Unlike the older raw statements here, it
            // ends in ";", so a script of this migration runs on its own.
            migrationBuilder.Sql(
                "INSERT INTO \"SetupStates\" (\"Id\", \"CompletedAt\") SELECT 1, now() " +
                "WHERE EXISTS (SELECT 1 FROM \"OAuthTokens\") AND EXISTS (SELECT 1 FROM \"UserAnimeEntries\");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SetupStates");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "BackfillCompletedAtUtc",
                table: "AiringRefreshStates",
                type: "timestamp with time zone",
                nullable: true);
        }
    }
}
