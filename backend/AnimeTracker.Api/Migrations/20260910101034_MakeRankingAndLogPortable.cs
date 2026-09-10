using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AnimeTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class MakeRankingAndLogPortable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Create the singleton ranking-state table before anything
            // else touches TopAnimeSelections, so its last-modified time can
            // be backfilled from SelectedAt while that column still exists.
            migrationBuilder.CreateTable(
                name: "RankingStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RankingStates", x => x.Id);
                });

            // 2. Every existing row holds the same SelectedAt (one write
            // stamps every position with one "now"), so max() is that value.
            // HAVING count(*) > 0 means an empty ranking inserts no row at
            // all, rather than a row holding null — an absent row and a null
            // ModifiedAt are both "never arranged", but only the former is
            // reachable from a ranking that has never had any positions.
            // Expected on this device: one row, 2026-09-09 07:59:51.127889+00.
            migrationBuilder.Sql(
                """
                INSERT INTO "RankingStates" ("ModifiedAt")
                SELECT max("SelectedAt") FROM "TopAnimeSelections"
                HAVING count(*) > 0;
                """);

            // 3. Only now that its value has been carried forward can the
            // per-position column go.
            migrationBuilder.DropColumn(
                name: "SelectedAt",
                table: "TopAnimeSelections");

            // 4. Nullable and defaultless for now: a database default would
            // never fire for the app (EF always sends the object's EventId),
            // and an EF-scaffolded zero-GUID default would turn a forgotten
            // value into a unique-index collision on the second row.
            migrationBuilder.AddColumn<Guid>(
                name: "EventId",
                table: "ActivityLogs",
                type: "uuid",
                nullable: true);

            // 5. Backfill every existing row with a fresh, distinct identity.
            // gen_random_uuid() has been built into Postgres since 13; the
            // server runs 17. Expected: 913 rows.
            migrationBuilder.Sql(
                """
                UPDATE "ActivityLogs" SET "EventId" = gen_random_uuid();
                """);

            // 6. Every row now holds a value, so the column can be required.
            migrationBuilder.AlterColumn<Guid>(
                name: "EventId",
                table: "ActivityLogs",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            // 7. Enforced only once every row is guaranteed non-null.
            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_EventId",
                table: "ActivityLogs",
                column: "EventId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ActivityLogs_EventId",
                table: "ActivityLogs");

            migrationBuilder.DropColumn(
                name: "EventId",
                table: "ActivityLogs");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SelectedAt",
                table: "TopAnimeSelections",
                type: "timestamp with time zone",
                nullable: true);

            // Every position ends up holding the one shared value it held
            // before Up ran. The now() fallback only covers a database whose
            // ranking was never arranged but somehow still has positions —
            // unreachable from Up, but Down must not fail if it happens.
            migrationBuilder.Sql(
                """
                UPDATE "TopAnimeSelections"
                SET "SelectedAt" = coalesce((SELECT "ModifiedAt" FROM "RankingStates" LIMIT 1), now());
                """);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "SelectedAt",
                table: "TopAnimeSelections",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.DropTable(
                name: "RankingStates");

            // The GUIDs assigned by Up are discarded here. Harmless until 03
            // ships an export; once a device has exported, rolling back this
            // migration would orphan GUIDs held in export files.
        }
    }
}
