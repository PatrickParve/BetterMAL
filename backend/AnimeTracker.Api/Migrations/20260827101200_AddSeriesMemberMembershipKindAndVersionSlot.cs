using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AnimeTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSeriesMemberMembershipKindAndVersionSlot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BranchHeadAnimeId",
                table: "SeriesMembers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MembershipKind",
                table: "SeriesMembers",
                type: "text",
                nullable: false,
                defaultValue: "Core");

            migrationBuilder.AddColumn<int>(
                name: "VersionSlotKey",
                table: "SeriesMembers",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BranchHeadAnimeId",
                table: "SeriesMembers");

            migrationBuilder.DropColumn(
                name: "MembershipKind",
                table: "SeriesMembers");

            migrationBuilder.DropColumn(
                name: "VersionSlotKey",
                table: "SeriesMembers");
        }
    }
}
