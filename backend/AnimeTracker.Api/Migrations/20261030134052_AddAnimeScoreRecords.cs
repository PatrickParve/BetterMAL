using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AnimeTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAnimeScoreRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnimeScoreRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AnimeId = table.Column<int>(type: "integer", nullable: false),
                    Score = table.Column<double>(type: "double precision", nullable: false),
                    DetectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnimeScoreRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnimeScoreRecords_AnimeMetadata_AnimeId",
                        column: x => x.AnimeId,
                        principalTable: "AnimeMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnimeScoreRecords_AnimeId_DetectedAt",
                table: "AnimeScoreRecords",
                columns: new[] { "AnimeId", "DetectedAt" },
                unique: true);

            // Every aired anime starts from what is already stored
            // (add-score-history-graph design D5): without a starting record,
            // the first change after this ships would only create a first
            // record, and a graph would wait for a second change, up to 56
            // days for an old show. Dated the last time that score was
            // actually written, so nothing is dated later than it was
            // observed; an anime with no such time gets the migration's own
            // moment. A row with no score, no status or one not yet aired gets
            // none, and a fresh install copies nothing. Ends in ";" so a
            // script of this migration runs on its own.
            migrationBuilder.Sql(
                "INSERT INTO \"AnimeScoreRecords\" (\"AnimeId\", \"Score\", \"DetectedAt\") " +
                "SELECT \"Id\", \"MalScore\", COALESCE(\"LastScoreSyncedAt\", now()) " +
                "FROM \"AnimeMetadata\" " +
                "WHERE \"MalScore\" IS NOT NULL AND \"AiringStatus\" IN ('currently_airing', 'finished_airing');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnimeScoreRecords");
        }
    }
}
