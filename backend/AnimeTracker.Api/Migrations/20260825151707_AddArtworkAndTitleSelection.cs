using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AnimeTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddArtworkAndTitleSelection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SelectedPictureUrl",
                table: "Series",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SelectedTitle",
                table: "Series",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MalPictureUrl",
                table: "AnimeMetadata",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "PictureUrls",
                table: "AnimeMetadata",
                type: "text[]",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PicturesSyncedAt",
                table: "AnimeMetadata",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("""UPDATE "AnimeMetadata" SET "MalPictureUrl" = "PictureUrl";""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SelectedPictureUrl",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "SelectedTitle",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "MalPictureUrl",
                table: "AnimeMetadata");

            migrationBuilder.DropColumn(
                name: "PictureUrls",
                table: "AnimeMetadata");

            migrationBuilder.DropColumn(
                name: "PicturesSyncedAt",
                table: "AnimeMetadata");
        }
    }
}
