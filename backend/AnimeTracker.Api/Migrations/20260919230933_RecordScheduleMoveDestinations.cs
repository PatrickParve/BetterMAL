using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AnimeTracker.Api.Migrations
{
    /// <summary>Adds the moved-to columns for the schedule-change kinds
    /// (record-both-ends-of-a-schedule-move design.md D1) and backfills
    /// <c>NewStartDate</c> on the rows whose destination is still recoverable
    /// from their anime's current premiere date — see the data statement's own
    /// comment for why that is provably true today.</summary>
    public partial class RecordScheduleMoveDestinations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NewBroadcastDayOfWeek",
                table: "AnimeUpdates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "NewBroadcastTime",
                table: "AnimeUpdates",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "NewStartDate",
                table: "AnimeUpdates",
                type: "date",
                nullable: true);

            // design.md D4: stamps NewStartDate from the anime's current
            // premiere date, but only on a row that is the *only*
            // StartDateChanged row for its anime (Kinds bit 8) — the NOT
            // EXISTS clause is what makes this sound rather than lucky.
            // Where an anime has exactly one recorded premiere move, the
            // anime's current date *is* that move's destination, by
            // definition of there having been no later recorded move; where
            // a second row exists, the chain is ambiguous and both rows are
            // left alone to fall back to the live value as before. So the
            // statement stays correct however the table has grown between
            // now and the deploy, which a bare "stamp every row" would not.
            //
            // AiredFrom <> PreviousStartDate skips a row whose anime has
            // since returned to the date it moved from: stamping there would
            // record a move to where it came from, which is not what the row
            // recorded. Those rows keep the fallback, and the equal-dates
            // guard in updateText.ts renders them without a direction.
            //
            // Verified against the live database before writing this (design.md
            // Context): 6 StartDateChanged rows across 6 distinct anime, none
            // sharing an anime, so this statement matches all 6 today.
            migrationBuilder.Sql(
                """
                UPDATE "AnimeUpdates" u
                SET "NewStartDate" = a."AiredFrom"
                FROM "AnimeMetadata" a
                WHERE a."Id" = u."AnimeId"
                  AND (u."Kinds" & 8) <> 0
                  AND u."NewStartDate" IS NULL
                  AND a."AiredFrom" IS NOT NULL
                  AND a."AiredFrom" <> u."PreviousStartDate"
                  AND NOT EXISTS (
                      SELECT 1 FROM "AnimeUpdates" o
                      WHERE o."AnimeId" = u."AnimeId" AND (o."Kinds" & 8) <> 0 AND o."Id" <> u."Id"
                  );
                """);

            // No equivalent statement for BroadcastSlotChanged: no such row
            // exists yet, and an empty backfill is better left unwritten than
            // written and unverifiable (design.md D4). Nothing to backfill for
            // EpisodesMoved either — it has always stored both ends.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drops the three columns, which takes the recorded destinations
            // with them — inherent to reverting a change whose point is to
            // record them, and symmetric with every other column-adding
            // migration here (design.md Risks/Trade-offs). Rows return to the
            // live-value fallback behaviour they have today; not an oversight.
            migrationBuilder.DropColumn(
                name: "NewBroadcastDayOfWeek",
                table: "AnimeUpdates");

            migrationBuilder.DropColumn(
                name: "NewBroadcastTime",
                table: "AnimeUpdates");

            migrationBuilder.DropColumn(
                name: "NewStartDate",
                table: "AnimeUpdates");
        }
    }
}
