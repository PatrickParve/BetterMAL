using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AnimeTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class ReportJobsAndLostMalConnection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastRunError",
                table: "ReconciliationRunLogs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LastRunFailed",
                table: "ReconciliationRunLogs",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ConnectionLostAt",
                table: "OAuthTokens",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastRunError",
                table: "ReconciliationRunLogs");

            migrationBuilder.DropColumn(
                name: "LastRunFailed",
                table: "ReconciliationRunLogs");

            migrationBuilder.DropColumn(
                name: "ConnectionLostAt",
                table: "OAuthTokens");
        }
    }
}
