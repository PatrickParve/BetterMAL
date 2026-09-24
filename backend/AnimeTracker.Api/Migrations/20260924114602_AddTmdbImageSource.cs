using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AnimeTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTmdbImageSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnimeIdMappings",
                columns: table => new
                {
                    AnimeId = table.Column<int>(type: "integer", nullable: false),
                    TmdbTvId = table.Column<int>(type: "integer", nullable: true),
                    TmdbSeasonNumber = table.Column<int>(type: "integer", nullable: true),
                    TmdbMovieIds = table.Column<List<int>>(type: "integer[]", nullable: false),
                    ImdbIds = table.Column<List<string>>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnimeIdMappings", x => x.AnimeId);
                });

            migrationBuilder.CreateTable(
                name: "AnimeIdMappingSyncStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnimeIdMappingSyncStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TmdbMovieImageSets",
                columns: table => new
                {
                    MovieId = table.Column<int>(type: "integer", nullable: false),
                    FetchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TmdbMovieImageSets", x => x.MovieId);
                });

            migrationBuilder.CreateTable(
                name: "TmdbSeasonImageSets",
                columns: table => new
                {
                    TvId = table.Column<int>(type: "integer", nullable: false),
                    SeasonNumber = table.Column<int>(type: "integer", nullable: false),
                    FetchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TmdbSeasonImageSets", x => new { x.TvId, x.SeasonNumber });
                });

            migrationBuilder.CreateTable(
                name: "TmdbTvImageSets",
                columns: table => new
                {
                    TvId = table.Column<int>(type: "integer", nullable: false),
                    FetchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TmdbTvImageSets", x => x.TvId);
                });

            migrationBuilder.CreateTable(
                name: "TmdbMovieImages",
                columns: table => new
                {
                    FilePath = table.Column<string>(type: "text", nullable: false),
                    MovieId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Language = table.Column<string>(type: "text", nullable: true),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TmdbMovieImages", x => new { x.MovieId, x.FilePath });
                    table.ForeignKey(
                        name: "FK_TmdbMovieImages_TmdbMovieImageSets_MovieId",
                        column: x => x.MovieId,
                        principalTable: "TmdbMovieImageSets",
                        principalColumn: "MovieId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TmdbSeasonImages",
                columns: table => new
                {
                    FilePath = table.Column<string>(type: "text", nullable: false),
                    TvId = table.Column<int>(type: "integer", nullable: false),
                    SeasonNumber = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Language = table.Column<string>(type: "text", nullable: true),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TmdbSeasonImages", x => new { x.TvId, x.SeasonNumber, x.FilePath });
                    table.ForeignKey(
                        name: "FK_TmdbSeasonImages_TmdbSeasonImageSets_TvId_SeasonNumber",
                        columns: x => new { x.TvId, x.SeasonNumber },
                        principalTable: "TmdbSeasonImageSets",
                        principalColumns: new[] { "TvId", "SeasonNumber" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TmdbTvImages",
                columns: table => new
                {
                    FilePath = table.Column<string>(type: "text", nullable: false),
                    TvId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Language = table.Column<string>(type: "text", nullable: true),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TmdbTvImages", x => new { x.TvId, x.FilePath });
                    table.ForeignKey(
                        name: "FK_TmdbTvImages_TmdbTvImageSets_TvId",
                        column: x => x.TvId,
                        principalTable: "TmdbTvImageSets",
                        principalColumn: "TvId",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnimeIdMappings");

            migrationBuilder.DropTable(
                name: "AnimeIdMappingSyncStates");

            migrationBuilder.DropTable(
                name: "TmdbMovieImages");

            migrationBuilder.DropTable(
                name: "TmdbSeasonImages");

            migrationBuilder.DropTable(
                name: "TmdbTvImages");

            migrationBuilder.DropTable(
                name: "TmdbMovieImageSets");

            migrationBuilder.DropTable(
                name: "TmdbSeasonImageSets");

            migrationBuilder.DropTable(
                name: "TmdbTvImageSets");
        }
    }
}
