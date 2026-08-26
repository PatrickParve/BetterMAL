using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AnimeTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAnimeUpdatesAndRelationDiscoveryProcessedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ProcessedAt",
                table: "RelationDiscoveries",
                type: "timestamp with time zone",
                nullable: true);

            // Baseline every pre-existing discovery as processed (design.md
            // D7): they predate the announcement feature and would otherwise
            // open it with a wall of backdated news, the same guard
            // activity-recording's baseline import already applies.
            migrationBuilder.Sql(
                "UPDATE \"RelationDiscoveries\" SET \"ProcessedAt\" = now() WHERE \"ProcessedAt\" IS NULL");

            migrationBuilder.CreateTable(
                name: "AnimeUpdates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AnimeId = table.Column<int>(type: "integer", nullable: false),
                    DetectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Kinds = table.Column<int>(type: "integer", nullable: false),
                    PreviousStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PreviousBroadcastDayOfWeek = table.Column<string>(type: "text", nullable: true),
                    PreviousBroadcastTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    MovedEpisode = table.Column<int>(type: "integer", nullable: true),
                    PreviousEpisodeDate = table.Column<DateOnly>(type: "date", nullable: true),
                    NewEpisodeDate = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnimeUpdates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnimeUpdates_AnimeMetadata_AnimeId",
                        column: x => x.AnimeId,
                        principalTable: "AnimeMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RelationDiscoveries_ProcessedAt",
                table: "RelationDiscoveries",
                column: "ProcessedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AnimeUpdates_AnimeId",
                table: "AnimeUpdates",
                column: "AnimeId");

            migrationBuilder.CreateIndex(
                name: "IX_AnimeUpdates_DetectedAt",
                table: "AnimeUpdates",
                column: "DetectedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnimeUpdates");

            migrationBuilder.DropIndex(
                name: "IX_RelationDiscoveries_ProcessedAt",
                table: "RelationDiscoveries");

            migrationBuilder.DropColumn(
                name: "ProcessedAt",
                table: "RelationDiscoveries");
        }
    }
}
