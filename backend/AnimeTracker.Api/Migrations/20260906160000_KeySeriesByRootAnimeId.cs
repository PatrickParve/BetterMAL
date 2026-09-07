using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AnimeTracker.Api.Migrations
{
    /// <summary>Rekeys `Series` on its root entry's MAL id in place, so the
    /// 105 stored chosen titles/pictures survive with no re-picking
    /// (proposal.md "The stored data is rewritten, not rebuilt", design.md
    /// D9/D10). Hand-written rather than scaffolded: EF's default diff for
    /// dropping the `RootAnimeId` column and un-identifying `Id` would drop
    /// and re-add `Id`, discarding every row.</summary>
    public partial class KeySeriesByRootAnimeId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1 (task 2.2): the FK must go first — it would reject the
            // very first renumbering statement below, since that statement
            // temporarily points every SeriesMembers.SeriesId at a negative,
            // nonexistent Series.Id. The unique index on RootAnimeId is
            // dropped too: the column it indexes is dropped in step 3, and
            // the uniqueness it guaranteed becomes the primary key's job.
            migrationBuilder.DropForeignKey(
                name: "FK_SeriesMembers_Series_SeriesId",
                table: "SeriesMembers");

            migrationBuilder.DropIndex(
                name: "IX_Series_RootAnimeId",
                table: "Series");

            // Step 2 (task 2.3, design D9): renumber through negative space.
            // A naive `UPDATE "Series" SET "Id" = "RootAnimeId"` collides
            // mid-statement — 12 stored series have a RootAnimeId equal to a
            // *different* series' current Id (roots run as low as 1), and
            // Postgres checks a non-deferrable unique index per row, not per
            // statement. Negating every existing key first moves it into a
            // range no final value occupies, so every intermediate state is
            // unique: the negated ids are distinct because the originals
            // were, each remapped value is positive while everything not yet
            // remapped is still negative, and the final values are unique
            // because RootAnimeId carried a unique index and no
            // (RootAnimeId, AnimeId) pair collides on the live data.
            migrationBuilder.Sql(
                """UPDATE "SeriesMembers" SET "SeriesId" = -"SeriesId";""");

            migrationBuilder.Sql(
                """UPDATE "Series" SET "Id" = -"Id";""");

            migrationBuilder.Sql(
                """
                UPDATE "SeriesMembers" sm SET "SeriesId" = s."RootAnimeId"
                FROM "Series" s
                WHERE sm."SeriesId" = s."Id";
                """);

            migrationBuilder.Sql(
                """UPDATE "Series" SET "Id" = "RootAnimeId";""");

            // Step 3 (task 2.4): drop identity generation (the builder
            // supplies Id now, design D2), drop the now-redundant
            // RootAnimeId column, and re-add the cascade FK. The re-added FK
            // is also the migration's own integrity check: an orphaned
            // membership — one whose remapped SeriesId names no Series row —
            // would fail it loudly here rather than surviving unnoticed (the
            // task 7.1 pre-flight confirms there are none on the live
            // database).
            migrationBuilder.Sql(
                """ALTER TABLE "Series" ALTER COLUMN "Id" DROP IDENTITY IF EXISTS;""");

            migrationBuilder.DropColumn(
                name: "RootAnimeId",
                table: "Series");

            migrationBuilder.AddForeignKey(
                name: "FK_SeriesMembers_Series_SeriesId",
                table: "SeriesMembers",
                column: "SeriesId",
                principalTable: "Series",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restores a working schema with root-derived numbering, not the
            // original identity values (design D10) — those are gone once
            // overwritten by Up, and restoring them means restoring the
            // `postgres-data` volume backup taken before this migration was
            // applied (task 7.2), not running this method.
            migrationBuilder.AddColumn<int>(
                name: "RootAnimeId",
                table: "Series",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """UPDATE "Series" SET "RootAnimeId" = "Id";""");

            migrationBuilder.CreateIndex(
                name: "IX_Series_RootAnimeId",
                table: "Series",
                column: "RootAnimeId",
                unique: true);

            migrationBuilder.Sql(
                """ALTER TABLE "Series" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY;""");

            // Seed the identity sequence past the highest MAL-derived id Up
            // left behind, so the next insert that relies on identity
            // generation (there should be none — the builder always
            // supplies Id — but this keeps the column's own invariant
            // honest) does not collide with one of those values.
            migrationBuilder.Sql(
                """SELECT setval(pg_get_serial_sequence('"Series"', 'Id'), (SELECT COALESCE(MAX("Id"), 1) FROM "Series"));""");
        }
    }
}
