using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AnimeTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAnimeThemesAnimeNameAndCatalogueFormat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CatalogueFormat",
                table: "AnimeThemesSyncStates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "AnimeName",
                table: "AnimeThemes",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CatalogueFormat",
                table: "AnimeThemesSyncStates");

            migrationBuilder.DropColumn(
                name: "AnimeName",
                table: "AnimeThemes");
        }
    }
}
