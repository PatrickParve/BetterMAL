using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AnimeTracker.Api.Migrations
{
    /// <summary>Data-only: no schema change, so the model snapshot is
    /// deliberately untouched. Retires the premiere-date reveals recorded
    /// before the two rules that make them impossible — a lean row is not a
    /// prior observation, and a premiere date is news only while a show has
    /// not finished airing (spec "Reveals recorded against already-finished
    /// anime are retired").</summary>
    public partial class RetireFalseStartDateReleasedUpdates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Kinds bit 4 is StartDateReleased. Cleared, not deleted: Kinds is
            // a flags column, and the same row may also carry a legitimate
            // episode-count release detected in the same pass (design.md D1) —
            // deleting the row would take that with it. The row is deleted
            // below only if clearing left it covering nothing at all.
            //
            // Bounded by DetectedAt, not by today's airing status: a reveal
            // recorded while its show had not finished airing is legitimate and
            // must survive that show finishing, since the gate is evaluated
            // once at write time and never re-evaluated on read. Comparing the
            // anime's end of airing against the moment of detection retires
            // exactly the rows the new rules would never have written.
            // AiredTo falls back to AiredFrom for the finished anime MAL
            // publishes no end date for; neither can be null on a row that had
            // just revealed a premiere date.
            //
            // These rows do not come back: the anime hold a real AiredFrom by
            // now, so the next diff sees known -> known rather than a reveal.
            migrationBuilder.Sql(
                """
                UPDATE "AnimeUpdates" u SET "Kinds" = u."Kinds" & ~4
                FROM "AnimeMetadata" a
                WHERE a."Id" = u."AnimeId" AND (u."Kinds" & 4) <> 0
                  AND a."AiringStatus" = 'finished_airing'
                  AND COALESCE(a."AiredTo", a."AiredFrom") < (u."DetectedAt" AT TIME ZONE 'UTC')::date;
                """);

            migrationBuilder.Sql("""DELETE FROM "AnimeUpdates" WHERE "Kinds" = 0;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Deliberately empty: the retirement is irreversible. A cleared bit
            // cannot be told apart from one that was never set, and the deleted
            // rows are gone — the same one-way baseline
            // AddAnimeUpdatesAndRelationDiscoveryProcessedAt took when it
            // stamped every pre-existing RelationDiscovery as processed. The
            // data destroyed is exactly the data this change declares was never
            // news, and a code revert would not re-record it either.
        }
    }
}
