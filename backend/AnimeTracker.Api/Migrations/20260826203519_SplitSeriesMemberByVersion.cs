using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AnimeTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class SplitSeriesMemberByVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_SeriesMembers",
                table: "SeriesMembers");

            // Backfilled true for every existing row (design.md Migration
            // Plan step 1): today, before the version partition is wired into
            // persistence (tasks 5-6), every anime has exactly one series
            // membership, so it's trivially the primary one. New rows always
            // set this explicitly; the column default only backfills history.
            migrationBuilder.AddColumn<bool>(
                name: "IsPrimary",
                table: "SeriesMembers",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "RelationGroup",
                table: "SeriesMembers",
                type: "text",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_SeriesMembers",
                table: "SeriesMembers",
                columns: new[] { "SeriesId", "AnimeId" });

            migrationBuilder.CreateIndex(
                name: "IX_SeriesMembers_AnimeId",
                table: "SeriesMembers",
                column: "AnimeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_SeriesMembers",
                table: "SeriesMembers");

            migrationBuilder.DropIndex(
                name: "IX_SeriesMembers_AnimeId",
                table: "SeriesMembers");

            migrationBuilder.DropColumn(
                name: "IsPrimary",
                table: "SeriesMembers");

            migrationBuilder.DropColumn(
                name: "RelationGroup",
                table: "SeriesMembers");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SeriesMembers",
                table: "SeriesMembers",
                column: "AnimeId");
        }
    }
}
