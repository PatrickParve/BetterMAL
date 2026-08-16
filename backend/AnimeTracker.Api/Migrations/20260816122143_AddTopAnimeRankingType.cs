using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AnimeTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTopAnimeRankingType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_TopAnimeRankingEntries",
                table: "TopAnimeRankingEntries");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TopAnimeFetchLogs",
                table: "TopAnimeFetchLogs");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "TopAnimeFetchLogs");

            migrationBuilder.AddColumn<string>(
                name: "RankingType",
                table: "TopAnimeRankingEntries",
                type: "text",
                nullable: false,
                defaultValue: "all");

            migrationBuilder.AddColumn<string>(
                name: "RankingType",
                table: "TopAnimeFetchLogs",
                type: "text",
                nullable: false,
                defaultValue: "all");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TopAnimeRankingEntries",
                table: "TopAnimeRankingEntries",
                columns: new[] { "RankingType", "AnimeId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_TopAnimeFetchLogs",
                table: "TopAnimeFetchLogs",
                column: "RankingType");

            migrationBuilder.CreateIndex(
                name: "IX_TopAnimeRankingEntries_AnimeId",
                table: "TopAnimeRankingEntries",
                column: "AnimeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_TopAnimeRankingEntries",
                table: "TopAnimeRankingEntries");

            migrationBuilder.DropIndex(
                name: "IX_TopAnimeRankingEntries_AnimeId",
                table: "TopAnimeRankingEntries");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TopAnimeFetchLogs",
                table: "TopAnimeFetchLogs");

            migrationBuilder.DropColumn(
                name: "RankingType",
                table: "TopAnimeRankingEntries");

            migrationBuilder.DropColumn(
                name: "RankingType",
                table: "TopAnimeFetchLogs");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "TopAnimeFetchLogs",
                type: "integer",
                nullable: false,
                defaultValue: 0)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddPrimaryKey(
                name: "PK_TopAnimeRankingEntries",
                table: "TopAnimeRankingEntries",
                column: "AnimeId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TopAnimeFetchLogs",
                table: "TopAnimeFetchLogs",
                column: "Id");
        }
    }
}
