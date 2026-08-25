using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AnimeTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRelationConfidenceSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RelationsFetchedAt",
                table: "AnimeAiringSyncs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AniListRelations",
                columns: table => new
                {
                    AnimeId = table.Column<int>(type: "integer", nullable: false),
                    RelatedAnimeId = table.Column<int>(type: "integer", nullable: false),
                    RelationType = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AniListRelations", x => new { x.AnimeId, x.RelatedAnimeId, x.RelationType });
                    table.ForeignKey(
                        name: "FK_AniListRelations_AnimeMetadata_AnimeId",
                        column: x => x.AnimeId,
                        principalTable: "AnimeMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RelationDiscoveries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AnimeId = table.Column<int>(type: "integer", nullable: false),
                    RelatedAnimeId = table.Column<int>(type: "integer", nullable: false),
                    RelationType = table.Column<string>(type: "text", nullable: false),
                    DiscoveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RelationDiscoveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RelationDiscoveries_AnimeMetadata_AnimeId",
                        column: x => x.AnimeId,
                        principalTable: "AnimeMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnimeRelatedAnime_RelatedAnimeId",
                table: "AnimeRelatedAnime",
                column: "RelatedAnimeId");

            migrationBuilder.CreateIndex(
                name: "IX_AniListRelations_AnimeId",
                table: "AniListRelations",
                column: "AnimeId");

            migrationBuilder.CreateIndex(
                name: "IX_AniListRelations_RelatedAnimeId",
                table: "AniListRelations",
                column: "RelatedAnimeId");

            migrationBuilder.CreateIndex(
                name: "IX_RelationDiscoveries_AnimeId",
                table: "RelationDiscoveries",
                column: "AnimeId");

            migrationBuilder.CreateIndex(
                name: "IX_RelationDiscoveries_DiscoveredAt",
                table: "RelationDiscoveries",
                column: "DiscoveredAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AniListRelations");

            migrationBuilder.DropTable(
                name: "RelationDiscoveries");

            migrationBuilder.DropIndex(
                name: "IX_AnimeRelatedAnime_RelatedAnimeId",
                table: "AnimeRelatedAnime");

            migrationBuilder.DropColumn(
                name: "RelationsFetchedAt",
                table: "AnimeAiringSyncs");
        }
    }
}
