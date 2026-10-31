using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AnimeTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAnimeThemesAndGuessTheTheme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnimeThemes",
                columns: table => new
                {
                    AnimeId = table.Column<int>(type: "integer", nullable: false),
                    ThemeId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "text", nullable: false),
                    SongId = table.Column<int>(type: "integer", nullable: true),
                    SongTitle = table.Column<string>(type: "text", nullable: true),
                    Artists = table.Column<List<string>>(type: "text[]", nullable: false),
                    Episodes = table.Column<string>(type: "text", nullable: true),
                    Nsfw = table.Column<bool>(type: "boolean", nullable: false),
                    Spoiler = table.Column<bool>(type: "boolean", nullable: false),
                    Clean = table.Column<bool>(type: "boolean", nullable: false),
                    VideoUrl = table.Column<string>(type: "text", nullable: true),
                    AudioUrl = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnimeThemes", x => new { x.AnimeId, x.ThemeId });
                });

            migrationBuilder.CreateTable(
                name: "AnimeThemesSyncStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnimeThemesSyncStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GuessTheThemeRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Moves = table.Column<int>(type: "integer", nullable: false),
                    Round = table.Column<int>(type: "integer", nullable: false),
                    Step = table.Column<int>(type: "integer", nullable: false),
                    Phase = table.Column<string>(type: "text", nullable: false),
                    RoundGuesses = table.Column<List<int>>(type: "integer[]", nullable: false),
                    PlayedSongIds = table.Column<List<int>>(type: "integer[]", nullable: false),
                    YearFrom = table.Column<int>(type: "integer", nullable: true),
                    YearTo = table.Column<int>(type: "integer", nullable: true),
                    MediaTypes = table.Column<List<string>>(type: "text[]", nullable: true),
                    ListScope = table.Column<string>(type: "text", nullable: false),
                    HideHentai = table.Column<bool>(type: "boolean", nullable: false),
                    Themes = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuessTheThemeRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GuessTheThemeRounds",
                columns: table => new
                {
                    RunId = table.Column<int>(type: "integer", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    AnimeId = table.Column<int>(type: "integer", nullable: false),
                    ThemeId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Label = table.Column<string>(type: "text", nullable: false),
                    SongId = table.Column<int>(type: "integer", nullable: false),
                    SongTitle = table.Column<string>(type: "text", nullable: true),
                    Artists = table.Column<List<string>>(type: "text[]", nullable: false),
                    AudioUrl = table.Column<string>(type: "text", nullable: false),
                    VideoUrl = table.Column<string>(type: "text", nullable: false),
                    AcceptedAnimeIds = table.Column<List<int>>(type: "integer[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuessTheThemeRounds", x => new { x.RunId, x.Number });
                    table.ForeignKey(
                        name: "FK_GuessTheThemeRounds_AnimeMetadata_AnimeId",
                        column: x => x.AnimeId,
                        principalTable: "AnimeMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GuessTheThemeRounds_GuessTheThemeRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "GuessTheThemeRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnimeThemes_SongId",
                table: "AnimeThemes",
                column: "SongId");

            migrationBuilder.CreateIndex(
                name: "IX_GuessTheThemeRounds_AnimeId",
                table: "GuessTheThemeRounds",
                column: "AnimeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnimeThemes");

            migrationBuilder.DropTable(
                name: "AnimeThemesSyncStates");

            migrationBuilder.DropTable(
                name: "GuessTheThemeRounds");

            migrationBuilder.DropTable(
                name: "GuessTheThemeRuns");
        }
    }
}
