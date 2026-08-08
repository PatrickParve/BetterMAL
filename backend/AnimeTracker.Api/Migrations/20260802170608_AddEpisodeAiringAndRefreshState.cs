using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AnimeTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEpisodeAiringAndRefreshState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiringRefreshStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LastSuccessfulPassAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    BackfillCompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastPassSeason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiringRefreshStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnimeAiringSyncs",
                columns: table => new
                {
                    AnimeId = table.Column<int>(type: "integer", nullable: false),
                    AniListId = table.Column<int>(type: "integer", nullable: true),
                    LastFetchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextAiringEpisodeAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    HasCompleteData = table.Column<bool>(type: "boolean", nullable: false),
                    NextRecheckAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnimeAiringSyncs", x => x.AnimeId);
                    table.ForeignKey(
                        name: "FK_AnimeAiringSyncs_AnimeMetadata_AnimeId",
                        column: x => x.AnimeId,
                        principalTable: "AnimeMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EpisodeAirings",
                columns: table => new
                {
                    AnimeId = table.Column<int>(type: "integer", nullable: false),
                    Episode = table.Column<int>(type: "integer", nullable: false),
                    AirsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FetchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EpisodeAirings", x => new { x.AnimeId, x.Episode });
                    table.ForeignKey(
                        name: "FK_EpisodeAirings_AnimeMetadata_AnimeId",
                        column: x => x.AnimeId,
                        principalTable: "AnimeMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EpisodeAirings_AnimeId_AirsAtUtc",
                table: "EpisodeAirings",
                columns: new[] { "AnimeId", "AirsAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiringRefreshStates");

            migrationBuilder.DropTable(
                name: "AnimeAiringSyncs");

            migrationBuilder.DropTable(
                name: "EpisodeAirings");
        }
    }
}
