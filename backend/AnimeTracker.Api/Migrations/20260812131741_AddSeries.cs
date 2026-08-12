using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AnimeTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSeries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Series",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RootAnimeId = table.Column<int>(type: "integer", nullable: false),
                    BuiltAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsPartial = table.Column<bool>(type: "boolean", nullable: false),
                    IsTruncated = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Series", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SeriesMembers",
                columns: table => new
                {
                    AnimeId = table.Column<int>(type: "integer", nullable: false),
                    SeriesId = table.Column<int>(type: "integer", nullable: false),
                    IsMainLine = table.Column<bool>(type: "boolean", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeriesMembers", x => x.AnimeId);
                    table.ForeignKey(
                        name: "FK_SeriesMembers_AnimeMetadata_AnimeId",
                        column: x => x.AnimeId,
                        principalTable: "AnimeMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SeriesMembers_Series_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Series_RootAnimeId",
                table: "Series",
                column: "RootAnimeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SeriesMembers_SeriesId",
                table: "SeriesMembers",
                column: "SeriesId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SeriesMembers");

            migrationBuilder.DropTable(
                name: "Series");
        }
    }
}
