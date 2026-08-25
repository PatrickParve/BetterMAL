## 1. Schema and indexes

- [x] 1.1 Add `HasIndex(e => e.RelatedAnimeId)` to the `AnimeRelatedAnime` config in `AnimeTrackerDbContext.cs:145`; generate the migration. Ships before anything reads reverse edges on the detail path (design decision 3).
- [x] 1.2 Add `AniListRelation` model + DbSet: `AnimeId` (MAL id), `RelatedAnimeId` (far end's MAL id), `RelationType` (AniList's raw uppercase enum value). Key on all three; index `AnimeId` and `RelatedAnimeId`. Store nothing for AniList relations whose far media has no MAL id.
- [x] 1.3 Add `RelationsFetchedAt` (nullable `DateTimeOffset`) to `AnimeAiringSync`. Null = relations never fetched, which is what stops an unlooked-up anime from contradicting every edge pointing at it.
- [x] 1.4 Add `RelationDiscovery` model + DbSet: `AnimeId`, `RelatedAnimeId`, `RelationType`, `DiscoveredAt`. Index on `DiscoveredAt` for the future updates view.
- [x] 1.5 Generate and apply migrations for 1.1–1.4 (all additive — no drops, no data migration). Build via the sdk:10.0 Docker image, not the local 9.0 SDK.

## 2. Refresh model

- [x] 2.1 Delete `ScoreOnlyFields` from `MetadataRefreshService.cs:15`.
- [x] 2.2 Extract the tier ladder into a shared helper (e.g. `RefreshTiers.TtlFor(AnimeMetadata)`) usable both as a LINQ-translatable predicate and as an in-memory check: airing/not-yet-aired → 1 day; finished within 1 year → 3 days; 1–2 years → 14 days; older/unknown → 28 days.
- [x] 2.3 Re-key `RefreshStaleBatchAsync`'s `due` query from `LastScoreSyncedAt` to `LastSyncedAt` and onto the new ladder. Order most-stale-first with never-fetched (`default`) sorting first.
- [x] 2.4 Add `.Include(a => a.RelatedAnime)` to the `due` query — required tracked snapshot, same reason as `RefreshOneAsync:104`, or EF re-inserts existing relation rows instead of diffing.
- [x] 2.5 Replace the per-anime score assignment in the batch loop with a full-detail `GetAnimeDetailsAsync` + `ApplyTo`, so relations, airing status, episode counts and ranks refresh on the tiers.
- [x] 2.6 Verify `MetadataRefreshBackgroundService` is untouched: `TickInterval` 10 min, `NightlyCap` 500, 1 req/sec pacer. Request count per anime must be unchanged (1).
- [x] 2.7 Keep `LastScoreSyncedAt` and its `ApplyLeanTo` write; do **not** collapse it into `LastSyncedAt` (design decision 7 — collapsing lets season browsing suppress both the tiered refresh and the detail-page TTL fetch). Leave a comment saying so.
- [x] 2.8 Rewrite `AnimeDetailService.NeedsFullDetailFetch` as `anime is null || anime.LastSyncedAt == default || now - anime.LastSyncedAt > RefreshTiers.TtlFor(anime)`, using the same helper as 2.2.
- [x] 2.9 Delete `RelatedAnimeMigrationCutoff`, `RelatedAnimeMediaTypeMigrationCutoff`, the `Genres` completeness clause, and the `RelatedAnime.Any(r => r.MediaType is null)` clause, along with the stale comment block at `AnimeDetailService.cs:22-58`.
- [x] 2.10 Set `RefreshFailed` on the detail path when the `RefreshOneAsync` catch at `AnimeDetailService.cs:71-74` fires; keep serving cached data and keep the read succeeding. Confirm `LastSyncedAt` is untouched by a failed fetch so the next visit retries.
- [x] 2.11 Add `bool RefreshFailed` to `AnimeDetailDto` and thread it through `FromEntity`.

## 3. Relation discovery events

- [x] 3.1 In `RefreshOneAsync`, diff the incoming relation set against the tracked `Include`d snapshot before `ApplyTo` replaces it; write one `RelationDiscovery` row per edge present after and absent before.
- [x] 3.2 Emit nothing when the anime had no cached row at all — an initial import's whole relation set is not news.
- [x] 3.3 Emit nothing for removed edges, and nothing when the set is unchanged.
- [x] 3.4 Apply the same diff to the batch path from 2.5, so tiered refreshes record discoveries too.

## 4. Relation resolution (no adjudication yet)

- [x] 4.1 Add `RelationInverse` map: `sequel`↔`prequel`, `parent_story`↔`side_story`, `summary`↔`full_story`; `alternative_version`, `alternative_setting`, `character`, `other` self-inverse; `spin_off`, `adaptation` and unrecognized strings have no inverse.
- [x] 4.2 Add a `RelationResolver` service. Load an anime's outgoing edges plus its incoming edges (indexed reverse query from 1.1), invert the incoming ones, de-duplicate by far-end anime id keeping the outgoing edge's type and `SortOrder`, and sort reverse-derived entries after all outgoing ones.
- [x] 4.3 Carry no-inverse relations across with their raw type, flagged as ineligible for a dedicated button (More overlay only).
- [x] 4.4 Classify confidence: Confirmed (far end stores the inverse), Unconfirmed (far end has `LastSyncedAt != default` and stores no match), Unknown (far end never full-fetched). Batch the far-end lookup — one query over the candidate ids, not one per edge. No-inverse relations are never Confirmed by symmetry.
- [x] 4.5 Implement the ranked prequel/sequel pick in the resolver: discard recaps/side content (reusing the `FindRecapIds`/`FindSideContentIds` rules — extract them from `SeriesGraphBuilder` to a shared place), falling back to the undiscarded set if that empties it; discard Contradicted; prefer higher confidence; prefer outgoing over reverse-derived; prefer matching media type then `tv`; nearest preceding/following aired-from; lowest MAL id.
- [x] 4.6 Add the series-neighbour fallback: where a direction has no candidate edge, use the anime's immediate main-line neighbour in its stored series. A direct edge always outranks it.
- [x] 4.7 Add resolved `prequel` / `sequel` / `parentStory` references to `AnimeDetailDto`, keeping the full `relatedAnime` list unchanged so the More overlay is unaffected. Include each entry's confidence and whether it was reverse-derived.
- [x] 4.8 Wire `AnimeDetailService.GetDetailAsync` to the resolver, replacing the direct `anime.RelatedAnime` projection.

## 5. AniList adjudication

- [x] 5.1 Extend `AniListClient.LookupQuery` (`AniListClient.cs:9-11`) with `relations { edges { relationType node { idMal } } }` and surface them on `AniListMediaLookup`.
- [x] 5.2 Add a batched `Page.media(idMal_in: [...])` relations query. Start at 25 per page and confirm against a live response — nested `relations` counts against AniList's complexity budget (design open question).
- [x] 5.3 Map AniList's enum to MAL values: `PREQUEL`→`prequel`, `SEQUEL`→`sequel`, `PARENT`→`parent_story`, `SIDE_STORY`→`side_story`, `SUMMARY`→ satisfies both `summary` and `full_story`, `SPIN_OFF`→`spin_off`, `CHARACTER`→`character`, `OTHER`→`other`, `ALTERNATIVE`→ satisfies either `alternative_version` or `alternative_setting`. Ignore `ADAPTATION`. An unmapped type still counts as "an edge exists".
- [x] 5.4 Persist fetched relations to `AniListRelation` and stamp `AnimeAiringSync.RelationsFetchedAt`. Skip relations whose far media has no `idMal`.
- [x] 5.5 Have `EpisodeScheduleRefreshService` store relations from the lookups it already makes — free, same request.
- [x] 5.6 Add background work that fetches relations for anime holding Unconfirmed edges and nothing else, batched under the existing 4.5s pacer. Do not look up Confirmed or Unknown edges.
- [x] 5.7 Implement the four verdicts in the resolver: Confirmed (mapped type matches), Corroborated (some edge, different type — traverse but no button), Contradicted (both ends known to AniList, no edge at all — neither traverse nor button), Unknown (either end unfetched or absent from AniList, or lookup failed — today's behaviour).
- [x] 5.8 Guard the contradiction path on all three states: `RelationsFetchedAt` non-null **and** `AniListId` non-null for both ends. Never-looked-up and absent-from-AniList must both yield Unknown.

## 6. Series ordering and traversal

- [x] 6.1 Filter Contradicted edges out of `SeriesGraphBuilder.TraverseAsync`'s outgoing and incoming edge sets (`SeriesGraphBuilder.cs:182-187` and the outgoing block above it). Unconfirmed-but-unadjudicated edges must still traverse.
- [x] 6.2 Replace the `OrderBy(OrderKey)` main-line sort at `SeriesGraphBuilder.cs:219` with a stable topological sort: Kahn's algorithm over `sequel`/`prequel` edges among main-line members, ready-set held in a priority queue keyed by the existing `OrderKey`.
- [x] 6.3 Handle cycles without throwing — when the queue empties with nodes remaining, emit the remainder in `OrderKey` order.
- [x] 6.4 Confirm the series root is still the first entry of the resulting order, and that extras keep air-date ordering within their media-type group.
- [x] 6.5 Bump `SeriesGraphBuilder.ClassificationRevisedAt` (`SeriesGraphBuilder.cs:36`) to the ship date so stored series re-derive on next read.

## 7. Frontend

- [x] 7.1 Replace the `find(r => r.relationType === ...)` picks at `AnimeDetailPage.tsx:261-264` with the server-resolved `prequel`/`sequel`/`parentStory` references.
- [x] 7.2 Keep `moreRelations` as everything not selected for a button, so the overlay still lists Contradicted and non-selected entries.
- [x] 7.3 Render a refresh-failure notice with a retry control when `refreshFailed` is set; retry re-reads the anime via the normal detail read.
- [x] 7.4 Confirm the `Series` link logic still works against the two-directional relation set (`hasSeriesRelation` plus `inSeries`).

## 8. Tests

- [x] 8.1 Create `Tests/Services/Detail/` — there is no coverage for `AnimeDetailService` today.
- [x] 8.2 Resolver: outgoing/incoming union; inverse map both ways; de-duplication of a symmetric edge into one entry; reverse-derived entries sorting last; no-inverse relations keeping their raw type and getting no button.
- [x] 8.3 Confidence matrix: Confirmed / Unconfirmed / Unknown, including that a never-full-fetched far end yields Unknown rather than Unconfirmed.
- [x] 8.4 Tier TTL matrix for `NeedsFullDetailFetch`: never-fetched refetches; each of the four tiers at just-inside and just-outside its TTL; an anime with genres and genuinely zero relations does **not** refetch on every visit (regression guard for the 130 live rows, e.g. 27775, 48556).
- [x] 8.5 Refresh: a tiered pass updates relations and advances `LastSyncedAt`; an anime full-fetched earlier the same day is skipped; a lean listing refresh does not postpone a tiered refresh.
- [x] 8.6 Ranked pick — Made in Abyss Movie 3 (36862) resolves its prequel to 34599 (`tv`) rather than 37515, with the recap rule doing the work; assert it still resolves correctly when 37515 holds the *lower* id, so the test cannot pass on `SortOrder` by accident.
- [x] 8.7 Ranked pick — an anime with two incoming `sequel` edges (one from a recap, one from a real season) resolves its prequel to the real season.
- [x] 8.8 Ranked pick — a reverse-derived candidate never displaces a correct outgoing pick at equal confidence; a Confirmed candidate beats an Unconfirmed one.
- [x] 8.9 Adjudication verdicts: Confirmed, Corroborated (traversed, no button), Contradicted (neither), Unknown on an unfetched or AniList-absent far end; AniList failure degrades every edge to Unknown.
- [x] 8.10 Series — Shinsengumi (39360) is not a member of the Astro Boy series once AniList contradicts `17965 --sequel--> 39360`, and the series keeps its other members.
- [x] 8.11 Series ordering — Jujutsu Kaisen 0 (48561) orders before Jujutsu Kaisen (40748) despite airing later; an unconstrained pair falls back to air date; a chain-edge-less member is placed by date; a cyclic chain still renders.
- [x] 8.12 Relation events — a newly-appearing edge writes one row; an unchanged set writes none; a first-time cache writes none; a removed edge writes none.
- [x] 8.13 Detail read flags `RefreshFailed` when the live fetch throws, still returns cached data, and leaves `LastSyncedAt` untouched so the next read retries.

## 9. Verification against live data

- [x] 9.1 Confirmed all 4 (39360, 33566, 900, 37348) now show a prequel button. Revised outcome for 39360: AniList has no entry at all for Shinsengumi under any MAL id mapping (confirmed directly against AniList's API — it exists there as id 188941 with `idMal: null`), so the edge can never be adjudicated to Contradicted; it stays Unconfirmed. Accepted by user — the point of this change was making the one-sided connection visible, and it now is.
- [x] 9.2 Confirmed Series 170 (Astro Boy) still has all 15 members, including 39360. Accepted per 9.1: since AniList can't adjudicate this specific pair, 39360 legitimately stays — it's a real MAL-asserted edge nothing has actually contradicted.
- [x] 9.3 Confirm the 130 genres-but-no-relations rows (103 synced after 2026-08-09) are treated as fetched and do not refetch per visit.
- [x] 9.4 Confirm the 92 relation rows with null `MediaType` from the 2026-08-09 cutoff backfill on their next tiered refresh.
- [x] 9.5 Confirm one full tiered catch-up pass over 609 my-list anime stays within `NightlyCap` per run and completes in roughly a day and a half, with no change to request pacing.
- [x] 9.6 Spot-check the 20 anime with 2+ prequel edges and the 20 with 2+ sequel edges for pick regressions against their current buttons.
