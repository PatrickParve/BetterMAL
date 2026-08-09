using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AnimeTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAnimeRelatedAnime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrequelMalId",
                table: "AnimeMetadata");

            migrationBuilder.DropColumn(
                name: "PrequelTitle",
                table: "AnimeMetadata");

            migrationBuilder.DropColumn(
                name: "SequelMalId",
                table: "AnimeMetadata");

            migrationBuilder.DropColumn(
                name: "SequelTitle",
                table: "AnimeMetadata");

            migrationBuilder.CreateTable(
                name: "AnimeRelatedAnime",
                columns: table => new
                {
                    AnimeId = table.Column<int>(type: "integer", nullable: false),
                    RelatedAnimeId = table.Column<int>(type: "integer", nullable: false),
                    RelationType = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    PictureUrl = table.Column<string>(type: "text", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnimeRelatedAnime", x => new { x.AnimeId, x.RelatedAnimeId, x.RelationType });
                    table.ForeignKey(
                        name: "FK_AnimeRelatedAnime_AnimeMetadata_AnimeId",
                        column: x => x.AnimeId,
                        principalTable: "AnimeMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnimeRelatedAnime_AnimeId",
                table: "AnimeRelatedAnime",
                column: "AnimeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnimeRelatedAnime");

            migrationBuilder.AddColumn<int>(
                name: "PrequelMalId",
                table: "AnimeMetadata",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrequelTitle",
                table: "AnimeMetadata",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SequelMalId",
                table: "AnimeMetadata",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SequelTitle",
                table: "AnimeMetadata",
                type: "text",
                nullable: true);
        }
    }
}
