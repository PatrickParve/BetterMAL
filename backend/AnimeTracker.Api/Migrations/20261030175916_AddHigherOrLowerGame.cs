using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AnimeTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddHigherOrLowerGame : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HigherOrLowerAppearances",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AnimeId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HigherOrLowerAppearances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HigherOrLowerAppearances_AnimeMetadata_AnimeId",
                        column: x => x.AnimeId,
                        principalTable: "AnimeMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HigherOrLowerRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Round = table.Column<int>(type: "integer", nullable: false),
                    Streak = table.Column<int>(type: "integer", nullable: false),
                    LeftAnimeId = table.Column<int>(type: "integer", nullable: false),
                    RightAnimeId = table.Column<int>(type: "integer", nullable: false),
                    LeftStays = table.Column<int>(type: "integer", nullable: false),
                    ShownAnimeIds = table.Column<List<int>>(type: "integer[]", nullable: false),
                    YearFrom = table.Column<int>(type: "integer", nullable: true),
                    YearTo = table.Column<int>(type: "integer", nullable: true),
                    MediaTypes = table.Column<List<string>>(type: "text[]", nullable: true),
                    ListScope = table.Column<string>(type: "text", nullable: false),
                    HideHentai = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HigherOrLowerRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HigherOrLowerRuns_AnimeMetadata_LeftAnimeId",
                        column: x => x.LeftAnimeId,
                        principalTable: "AnimeMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HigherOrLowerRuns_AnimeMetadata_RightAnimeId",
                        column: x => x.RightAnimeId,
                        principalTable: "AnimeMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HigherOrLowerAppearances_AnimeId",
                table: "HigherOrLowerAppearances",
                column: "AnimeId");

            migrationBuilder.CreateIndex(
                name: "IX_HigherOrLowerRuns_LeftAnimeId",
                table: "HigherOrLowerRuns",
                column: "LeftAnimeId");

            migrationBuilder.CreateIndex(
                name: "IX_HigherOrLowerRuns_RightAnimeId",
                table: "HigherOrLowerRuns",
                column: "RightAnimeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HigherOrLowerAppearances");

            migrationBuilder.DropTable(
                name: "HigherOrLowerRuns");
        }
    }
}
