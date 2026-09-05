using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AnimeTracker.Api.Migrations
{
    /// <summary>Data-only: no schema change, so the model snapshot is
    /// deliberately untouched. Retires the rows recorded before the
    /// relevance-gate rules made them impossible to write again
    /// (proposal.md "The junk already recorded is deleted", design.md D6):
    /// announcements about anime already tracked at the moment they were
    /// announced, updates for anime neither a non-Dropped list entry nor
    /// linked to one, and relation discoveries not originating on a
    /// non-Dropped list entry.</summary>
    public partial class RetireUpdatesOutsideMyList : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Statement 1 (task 5.2, corrected during task 7.1's live-database
            // verification): an Announced row is a mistake only when the
            // anime was *already* tracked at the moment the announcement
            // fired — that is what removes the Yani Neko row (70), a
            // Watching entry since 16 Aug announced on 5 Sep. It is
            // deliberately not "is the anime tracked now": an anime that was
            // genuinely unknown when announced, and added to the list only
            // afterward — perhaps because of the announcement — was real
            // news at the time and stays recorded. The first draft of this
            // statement joined "UserAnimeEntries" with no timing check, which
            // over-deleted exactly that second case (rows 9 and 42 in the
            // verification run) before the mistake was caught.
            //
            // "Already tracked when announced" is read from ActivityLogs
            // (Added/StatusChanged/episode/score events all count — any of
            // them proves the anime was already a list entry): an Announced
            // row is deleted only where some log entry for its anime
            // predates the row's own DetectedAt. An anime with no such
            // earlier entry — including one added after the announcement, or
            // one never logged at all before the log table existed — is left
            // alone; the row's own history is the only source of truth, not
            // whether the anime happens to be on the list at migration time.
            //
            // Kinds bit 1 is Announced. Announced is never merged with
            // another kind (the resolver records it alone, per AnimeUpdate's
            // "two families" comment), so deleting the row takes nothing
            // else with it and no bit-clearing is needed, unlike the
            // StartDateReleased retirement above it.
            migrationBuilder.Sql(
                """
                DELETE FROM "AnimeUpdates" u
                WHERE (u."Kinds" & 1) <> 0
                AND EXISTS (
                    SELECT 1 FROM "ActivityLogs" a
                    WHERE a."AnimeId" = u."AnimeId" AND a."Timestamp" < u."DetectedAt"
                );
                """);

            // Statement 2 (tasks 5.3-5.6): delete every remaining update row
            // whose anime fails the relevance test ("Nothing is recorded for
            // an anime outside my list and its direct relations") — not a
            // non-Dropped UserAnimeEntries row, and no non-Contradicted edge
            // (either direction, any relation type) to one.
            //
            // The contradicted_edges CTE mirrors RelationResolver.Adjudicate
            // exactly, per relation-confidence spec and design.md D6: an edge
            // is Contradicted only when both anime are known to AniList
            // (AnimeAiringSyncs rows with AniListId and RelationsFetchedAt
            // non-null), no AniListRelations row exists between them in
            // either direction, the edge is eligible for adjudication, and
            // MAL's own far side does not already confirm it by storing the
            // inverse type back (in which case Adjudicate is never reached
            // at all — Confirmed short-circuits it).
            //
            // "Eligible for adjudication" is RelationType NOT IN the ten
            // RelationInverse.Map keys (no confident inverse — spin_off,
            // adaptation and unrecognized types are always eligible) OR the
            // other side (the end that does not own this particular row) has
            // been fully fetched (LastSyncedAt <> the DateTimeOffset default
            // — stored as Postgres '-infinity', not '0001-01-01', since
            // Npgsql maps DateTimeOffset.MinValue to the timestamptz
            // -infinity value on write; matching AnimeMetadata's own "never
            // fully fetched" test, `LastSyncedAt != default`). An earlier
            // draft of this statement compared against the literal
            // '0001-01-01T00:00:00Z' instead, which no row can ever equal —
            // making this OR-branch a tautology and eligibility always true.
            // It happened to change nothing for this database's actual
            // deletions (verified during task 7.1/7.4 follow-up: none of the
            // rows this migration removed had any relation edge — contradicted
            // or not — to a non-Dropped entry to begin with), but was wrong
            // and is corrected here.
            //
            // AniList's relation *type* is deliberately absent from this
            // rule (task 5.5): Adjudicate reaches Contradicted only when
            // matchingEdges is empty — no AniList edge between the pair at
            // all — and where an edge does exist, AniListRelationTypeMapper.
            // Matches only ever chooses between Confirmed and Corroborated,
            // neither of which is Contradicted. The type mapping cannot
            // change this rule's outcome, so it is not reproduced here.
            //
            // Duplicating RelationResolver's confidence logic in SQL is
            // exactly what design.md D2 forbids in live code, but is safe
            // here (task 5.6): this migration runs once against a known
            // database and is then frozen — it cannot drift out of step with
            // RelationResolver, because it never runs again. The live-code
            // rule against duplication is about two implementations that
            // both keep changing; this one stops changing the moment it
            // ships.
            //
            // The two EXISTS blocks below mirror RelationResolver.
            // GetEdgesAsync's dedup: an outgoing edge (the anime's own
            // AnimeRelatedAnime row) always counts, but an incoming edge (a
            // non-Dropped entry's own row naming this anime) counts only
            // where this anime stores no outgoing edge to that same entry at
            // all — the same rule that makes a MAL-confirmed inverse
            // impossible on a surviving incoming row by construction.
            migrationBuilder.Sql(
                """
                WITH relation_inverse(from_type, to_type) AS (
                    VALUES
                        ('sequel', 'prequel'),
                        ('prequel', 'sequel'),
                        ('parent_story', 'side_story'),
                        ('side_story', 'parent_story'),
                        ('summary', 'full_story'),
                        ('full_story', 'summary'),
                        ('alternative_version', 'alternative_version'),
                        ('alternative_setting', 'alternative_setting'),
                        ('character', 'character'),
                        ('other', 'other')
                ),
                contradicted_edges AS (
                    SELECT r."AnimeId" AS owner_id, r."RelatedAnimeId" AS far_id, r."RelationType" AS relation_type
                    FROM "AnimeRelatedAnime" r
                    JOIN "AnimeAiringSyncs" owner_sync ON owner_sync."AnimeId" = r."AnimeId"
                        AND owner_sync."AniListId" IS NOT NULL AND owner_sync."RelationsFetchedAt" IS NOT NULL
                    JOIN "AnimeAiringSyncs" far_sync ON far_sync."AnimeId" = r."RelatedAnimeId"
                        AND far_sync."AniListId" IS NOT NULL AND far_sync."RelationsFetchedAt" IS NOT NULL
                    WHERE NOT EXISTS (
                        SELECT 1 FROM "AniListRelations" al
                        WHERE (al."AnimeId" = r."AnimeId" AND al."RelatedAnimeId" = r."RelatedAnimeId")
                           OR (al."AnimeId" = r."RelatedAnimeId" AND al."RelatedAnimeId" = r."AnimeId")
                    )
                    AND (
                        NOT EXISTS (SELECT 1 FROM relation_inverse ri WHERE ri.from_type = r."RelationType")
                        OR EXISTS (
                            SELECT 1 FROM "AnimeMetadata" far
                            WHERE far."Id" = r."RelatedAnimeId" AND far."LastSyncedAt" <> '-infinity'
                        )
                    )
                    AND NOT EXISTS (
                        SELECT 1 FROM "AnimeRelatedAnime" back
                        JOIN relation_inverse ri ON ri.from_type = r."RelationType"
                        WHERE back."AnimeId" = r."RelatedAnimeId" AND back."RelatedAnimeId" = r."AnimeId"
                          AND back."RelationType" = ri.to_type
                    )
                )
                DELETE FROM "AnimeUpdates" u
                WHERE NOT EXISTS (
                    SELECT 1 FROM "UserAnimeEntries" e WHERE e."AnimeId" = u."AnimeId" AND e."Status" <> 'Dropped'
                )
                AND NOT EXISTS (
                    -- an outgoing edge from this anime to a non-Dropped entry, not contradicted
                    SELECT 1 FROM "AnimeRelatedAnime" r
                    JOIN "UserAnimeEntries" e ON e."AnimeId" = r."RelatedAnimeId" AND e."Status" <> 'Dropped'
                    WHERE r."AnimeId" = u."AnimeId"
                    AND NOT EXISTS (
                        SELECT 1 FROM contradicted_edges ce
                        WHERE ce.owner_id = r."AnimeId" AND ce.far_id = r."RelatedAnimeId" AND ce.relation_type = r."RelationType"
                    )
                )
                AND NOT EXISTS (
                    -- an incoming edge owned by a non-Dropped entry, considered only
                    -- where this anime stores no outgoing edge to that same entry
                    SELECT 1 FROM "AnimeRelatedAnime" r
                    JOIN "UserAnimeEntries" e ON e."AnimeId" = r."AnimeId" AND e."Status" <> 'Dropped'
                    WHERE r."RelatedAnimeId" = u."AnimeId"
                    AND NOT EXISTS (
                        SELECT 1 FROM "AnimeRelatedAnime" out2
                        WHERE out2."AnimeId" = u."AnimeId" AND out2."RelatedAnimeId" = r."AnimeId"
                    )
                    AND NOT EXISTS (
                        SELECT 1 FROM contradicted_edges ce
                        WHERE ce.owner_id = r."AnimeId" AND ce.far_id = r."RelatedAnimeId" AND ce.relation_type = r."RelationType"
                    )
                );
                """);

            // Statement 3 (task 5.7): discoveries are read only by the
            // announcement resolver (RelationDiscovery's own summary), so
            // clearing every one not rooted on a non-Dropped list entry is
            // safe outright — nothing else reads this table, and the
            // relation edges the series page is built from (AnimeRelatedAnime)
            // are a separate table entirely, untouched by this statement.
            migrationBuilder.Sql(
                """
                DELETE FROM "RelationDiscoveries" d
                WHERE NOT EXISTS (
                    SELECT 1 FROM "UserAnimeEntries" e WHERE e."AnimeId" = d."AnimeId" AND e."Status" <> 'Dropped'
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Deliberately empty: the retirement is irreversible, the same
            // one-way baseline RetireFalseStartDateReleasedUpdates and
            // AddAnimeUpdatesAndRelationDiscoveryProcessedAt both took. The
            // rows deleted here are exactly the rows the recording gate now
            // prevents from ever being written again, so a code revert would
            // not re-record them either.
        }
    }
}
