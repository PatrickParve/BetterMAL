using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AnimeTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEpisodeTotalSourceColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AniListTotalEpisodes",
                table: "AnimeMetadata",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MalTotalEpisodes",
                table: "AnimeMetadata",
                type: "integer",
                nullable: true);

            // Every existing TotalEpisodes value came from MAL, so it backfills
            // as MalTotalEpisodes — ResolveTotalEpisodes() then reproduces it
            // on the next write and immediately for every read.
            migrationBuilder.Sql(@"UPDATE ""AnimeMetadata"" SET ""MalTotalEpisodes"" = ""TotalEpisodes"";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AniListTotalEpisodes",
                table: "AnimeMetadata");

            migrationBuilder.DropColumn(
                name: "MalTotalEpisodes",
                table: "AnimeMetadata");
        }
    }
}
