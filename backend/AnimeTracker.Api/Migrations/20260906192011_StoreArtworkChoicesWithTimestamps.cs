using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AnimeTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class StoreArtworkChoicesWithTimestamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SelectedPictureModifiedAt",
                table: "Series",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SelectedTitleModifiedAt",
                table: "Series",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SelectedPictureModifiedAt",
                table: "AnimeMetadata",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SelectedPictureUrl",
                table: "AnimeMetadata",
                type: "text",
                nullable: true);

            // IS DISTINCT FROM is the exact SQL reading of the C# `!=` this
            // retires (Services/Artwork/AnimePicture.IsOverridden) — it
            // leaves the rows where both columns are null untouched, as `!=`
            // did. Copying PictureUrl into SelectedPictureUrl re-derives the
            // same value the row already displays, so PictureUrl itself
            // needs no write here. Expected: 190 rows.
            migrationBuilder.Sql(@"UPDATE ""AnimeMetadata"" SET ""SelectedPictureUrl"" = ""PictureUrl"", ""SelectedPictureModifiedAt"" = now() WHERE ""PictureUrl"" IS DISTINCT FROM ""MalPictureUrl"";");

            // Every choice that exists at migration time is stamped with the
            // migration's own run time, not left null, so null keeps meaning
            // exactly one thing — never chosen, never cleared — and every
            // pre-existing choice sorts before anything chosen afterwards on
            // this database. Expected: 22 and 96 rows respectively.
            migrationBuilder.Sql(@"UPDATE ""Series"" SET ""SelectedTitleModifiedAt"" = now() WHERE ""SelectedTitle"" IS NOT NULL;");
            migrationBuilder.Sql(@"UPDATE ""Series"" SET ""SelectedPictureModifiedAt"" = now() WHERE ""SelectedPictureUrl"" IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Lossless for artwork: PictureUrl still holds the displayed
            // picture and MalPictureUrl still holds MAL's, so the retired
            // comparison reads exactly as it does today. Only the recorded
            // times are gone.
            migrationBuilder.DropColumn(
                name: "SelectedPictureModifiedAt",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "SelectedTitleModifiedAt",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "SelectedPictureModifiedAt",
                table: "AnimeMetadata");

            migrationBuilder.DropColumn(
                name: "SelectedPictureUrl",
                table: "AnimeMetadata");
        }
    }
}
