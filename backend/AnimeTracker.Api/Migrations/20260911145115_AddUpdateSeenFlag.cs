using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AnimeTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUpdateSeenFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Seen",
                table: "AnimeUpdates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Starting point (store-seen-updates-on-server design.md D2):
            // every update recorded before this rule took effect counts as
            // seen, so existing rows don't all light up the indicator at
            // once. Migrations run at startup before any recording service
            // starts, so no row recorded after this ships is caught here.
            migrationBuilder.Sql("UPDATE \"AnimeUpdates\" SET \"Seen\" = true;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Seen",
                table: "AnimeUpdates");
        }
    }
}
