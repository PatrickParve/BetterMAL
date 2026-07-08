using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AnimeTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class ExtendMetadataAndAddTopAnimeAndReconciliationDiff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Background",
                table: "AnimeMetadata",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "Genres",
                table: "AnimeMetadata",
                type: "text[]",
                nullable: true);

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

            migrationBuilder.AddColumn<string>(
                name: "Synopsis",
                table: "AnimeMetadata",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PendingReconciliationDiffs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ComputedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingReconciliationDiffs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TopAnimeSelections",
                columns: table => new
                {
                    AnimeId = table.Column<int>(type: "integer", nullable: false),
                    SelectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TopAnimeSelections", x => x.AnimeId);
                    table.ForeignKey(
                        name: "FK_TopAnimeSelections_AnimeMetadata_AnimeId",
                        column: x => x.AnimeId,
                        principalTable: "AnimeMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PendingReconciliationDiffEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PendingReconciliationDiffId = table.Column<int>(type: "integer", nullable: false),
                    AnimeId = table.Column<int>(type: "integer", nullable: false),
                    ChangeType = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    EpisodesWatched = table.Column<int>(type: "integer", nullable: false),
                    MyScore = table.Column<int>(type: "integer", nullable: true),
                    StartedAt = table.Column<DateOnly>(type: "date", nullable: true),
                    CompletedAt = table.Column<DateOnly>(type: "date", nullable: true),
                    RewatchCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingReconciliationDiffEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PendingReconciliationDiffEntries_PendingReconciliationDiffs~",
                        column: x => x.PendingReconciliationDiffId,
                        principalTable: "PendingReconciliationDiffs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PendingReconciliationDiffEntries_PendingReconciliationDiffId",
                table: "PendingReconciliationDiffEntries",
                column: "PendingReconciliationDiffId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PendingReconciliationDiffEntries");

            migrationBuilder.DropTable(
                name: "TopAnimeSelections");

            migrationBuilder.DropTable(
                name: "PendingReconciliationDiffs");

            migrationBuilder.DropColumn(
                name: "Background",
                table: "AnimeMetadata");

            migrationBuilder.DropColumn(
                name: "Genres",
                table: "AnimeMetadata");

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

            migrationBuilder.DropColumn(
                name: "Synopsis",
                table: "AnimeMetadata");
        }
    }
}
