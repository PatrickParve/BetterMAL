using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AnimeTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class StoreAnimeStudiosAsList : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "Studios",
                table: "AnimeMetadata",
                type: "text[]",
                nullable: true);

            // Carry each stored studio over as a list of one (design.md D8);
            // the rest of an anime's studios arrive with its next full-detail
            // fetch.
            migrationBuilder.Sql("""UPDATE "AnimeMetadata" SET "Studios" = ARRAY["Studio"] WHERE "Studio" IS NOT NULL;""");

            migrationBuilder.DropColumn(
                name: "Studio",
                table: "AnimeMetadata");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Studio",
                table: "AnimeMetadata",
                type: "text",
                nullable: true);

            // A single column can hold only the first studio.
            migrationBuilder.Sql("""UPDATE "AnimeMetadata" SET "Studio" = "Studios"[1] WHERE "Studios" IS NOT NULL;""");

            migrationBuilder.DropColumn(
                name: "Studios",
                table: "AnimeMetadata");
        }
    }
}
