using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AnimeTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUserAnimeEntryConcurrencyTokenAndReconciliationRunLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // xmin is a Postgres system column present on every table already —
            // it cannot be added (Postgres rejects a user column named "xmin"),
            // it only needs to be mapped in the EF model (see
            // AnimeTrackerDbContext), which the generator can't tell apart from a
            // genuinely new column.
            migrationBuilder.CreateTable(
                name: "ReconciliationRunLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LastRunAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReconciliationRunLogs", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReconciliationRunLogs");
        }
    }
}
