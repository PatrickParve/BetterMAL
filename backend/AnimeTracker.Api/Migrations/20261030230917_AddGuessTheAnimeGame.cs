using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AnimeTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddGuessTheAnimeGame : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GuessTheAnimeAppearances",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AnimeId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuessTheAnimeAppearances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuessTheAnimeAppearances_AnimeMetadata_AnimeId",
                        column: x => x.AnimeId,
                        principalTable: "AnimeMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GuessTheAnimeRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AnswerAnimeId = table.Column<int>(type: "integer", nullable: false),
                    GuessedAnimeIds = table.Column<List<int>>(type: "integer[]", nullable: false),
                    GuessCount = table.Column<int>(type: "integer", nullable: false),
                    YearFrom = table.Column<int>(type: "integer", nullable: true),
                    YearTo = table.Column<int>(type: "integer", nullable: true),
                    MediaTypes = table.Column<List<string>>(type: "text[]", nullable: true),
                    ListScope = table.Column<string>(type: "text", nullable: false),
                    HideHentai = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuessTheAnimeRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuessTheAnimeRuns_AnimeMetadata_AnswerAnimeId",
                        column: x => x.AnswerAnimeId,
                        principalTable: "AnimeMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuessTheAnimeAppearances_AnimeId",
                table: "GuessTheAnimeAppearances",
                column: "AnimeId");

            migrationBuilder.CreateIndex(
                name: "IX_GuessTheAnimeRuns_AnswerAnimeId",
                table: "GuessTheAnimeRuns",
                column: "AnswerAnimeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuessTheAnimeAppearances");

            migrationBuilder.DropTable(
                name: "GuessTheAnimeRuns");
        }
    }
}
