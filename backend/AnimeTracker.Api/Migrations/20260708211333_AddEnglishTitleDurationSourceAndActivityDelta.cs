using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AnimeTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEnglishTitleDurationSourceAndActivityDelta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AverageEpisodeDurationSeconds",
                table: "AnimeMetadata",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EnglishTitle",
                table: "AnimeMetadata",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "AnimeMetadata",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PreviousEpisodesWatched",
                table: "ActivityLogs",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AverageEpisodeDurationSeconds",
                table: "AnimeMetadata");

            migrationBuilder.DropColumn(
                name: "EnglishTitle",
                table: "AnimeMetadata");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "AnimeMetadata");

            migrationBuilder.DropColumn(
                name: "PreviousEpisodesWatched",
                table: "ActivityLogs");
        }
    }
}
