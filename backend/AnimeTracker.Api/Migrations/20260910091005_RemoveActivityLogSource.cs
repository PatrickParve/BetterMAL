using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AnimeTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveActivityLogSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // By value, not by "<> 'BetterMal'": that would also delete
            // MalHeldDecline rows, which are my own actions (design D2) and
            // must be kept. Not by id either — the second device's rows
            // differ, and deleting by value cleans its sync rows too.
            migrationBuilder.Sql(
                "DELETE FROM \"ActivityLogs\" WHERE \"Source\" IN ('MalStartupImport', 'MalReconciliation', 'MalResync')");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "ActivityLogs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restores the schema only — the rows deleted in Up come back only
            // from a database dump, and any MalHeldDecline row would read back
            // as BetterMal, since the distinction no longer exists to restore.
            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "ActivityLogs",
                type: "text",
                nullable: false,
                defaultValue: "BetterMal");
        }
    }
}
