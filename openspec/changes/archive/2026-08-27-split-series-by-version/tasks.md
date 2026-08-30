## 1. Schema and model

- [x] 1.1 Add `RelationGroup` (nullable string) and `IsPrimary` (bool) to `Models/SeriesMember.cs`, documenting that a row is now per (series, anime) rather than per anime
- [x] 1.2 Change the `SeriesMember` key in `Data/AnimeTrackerDbContext.cs` from `AnimeId` to `(SeriesId, AnimeId)`, keep the `SeriesId` index, and add an index on `AnimeId` for the anime→series lookups that no longer hit the primary key
- [x] 1.3 Generate the EF migration; backfill `IsPrimary` to true for every existing row and `RelationGroup` to null
- [x] 1.4 Bump `SeriesGraphBuilder.ClassificationRevisedAt` to this change's date so every stored series rebuilds on its next read

## 2. Relation vocabulary

- [x] 2.1 In `Services/Series/SeriesRelations.cs`, add `VersionRelations` (`alternative_version`, `alternative_setting`) and a `StoryTraversalSet` (the current traversal set minus `alternative_version`); make `TraversalSet` the union used for component discovery
- [x] 2.2 Add the directional relation-group resolution: given an edge and which end the extra sits on, return one of Alternative version, Alternative setting, Prequel, Sequel, Parent story, Side story, Full story, Summary, Spin-off, Character, Adaptation, Other
- [x] 2.3 Add `Services/Series/SeriesRelationGroupOrder.cs` carrying the fixed display order from the `series-page` delta. **Deferred**: did not delete `SeriesMediaTypeOrder.cs` yet — `SeriesGraphBuilder.Classify` and `SeriesService.ProjectAsync` still group by media type until tasks 5.1/7.2 rewire them to relation groups; deleting it now would break that still-active pipeline. Delete it once those callers move over.
- [x] 2.4 Unit-test the directional resolution both ways for `summary`/`full_story`, `side_story`/`parent_story` and `sequel`/`prequel`

## 3. Component traversal

- [x] 3.1 Extend `SeriesGraphBuilder.TraverseAsync` to traverse `alternative_setting` alongside the existing set, keeping the existing contradicted-edge, companion-media and probe rules untouched
- [x] 3.2 Record, per traversed edge, which relation connected the two members, so the partition and the relation groups can both read it without re-querying
- [x] 3.3 Confirm traversal is breadth-first from the seed, so the seeded telling completes before budget reaches distant tellings; assert the fetch, probe and member-cap limits still bound the whole component
- [x] 3.4 Test: a component spanning three tellings is traversed once and stays within the member cap

## 4. Version partition

- [x] 4.1 Compute main-line eligibility over the whole undivided component and expose it to the partition (extract the predicate out of `ClassifyMainLineChain`)
- [x] 4.2 Identify anchors: members carrying a version relation to another member, restricted to eligible members
- [x] 4.3 Compute shortest-path distance from every member to every anchor over story relations only, with the all-edges fallback for members that reach no anchor
- [x] 4.4 Assign each member to every anchor at its minimum distance, producing one member set per telling, with equidistant members in several
- [x] 4.5 Add each telling's boundary members — anchors linked to one of its members by a version relation — marked so they are never main line and never traversed through
- [x] 4.6 Mark exactly one membership per anime as primary: nearest anchor, ties by the anchor's aired-from date then MAL id; a boundary member's primary is its own telling
- [x] 4.7 Tests: FMA-shaped fixture (exclusive film stays put, shared OVA in both, each telling roots on itself); an `alternative_setting` pair; an ineligible `alternative_version` pair that must not split; a component with no anchor producing exactly one series

**Note on tasks 5-7's completion**: `SeriesGraphBuilder.BuildAsync` now partitions the traversed component (`SeriesVersionPartitioner`), classifies and groups each telling independently (`ClassifyTelling`/`ResolveExtraGroups`), and persists every telling in one `PersistAsync` call via greedy overlap matching. `SeriesService.ProjectAsync` resolves through `IsPrimary`, groups extras by `RelationGroup`, and merges in read-time-projected related entries. Callers still assuming one membership per anime (`RelationResolver`, `AnimeDetailService`, `ArtworkSelectionService`/`PictureRefreshService`, `SeriesSearchLookup`/`SeriesRankingIndex`, `SeriesBulkBuildBackgroundService`) are unchanged and remain tasks 8.1-8.5; the frontend (tasks 10.*) hasn't been touched.

## 5. Per-telling classification and grouping

- [x] 5.1 Run `Classify` per telling over that telling's own member set, with boundary members forced ineligible
- [x] 5.2 Resolve each extra's relation group: direct edge to a main-line member by precedence, else breadth-first inheritance from the extra that reaches it, else Other
- [x] 5.3 Assign `Order` within each relation group by aired-from ascending, nulls last, MAL id as tiebreak
- [x] 5.4 Tests: a boundary member carrying both a version and a sequel edge groups as Alternative version; a special hanging off a side story inherits Side story; nothing lands in two groups

## 6. Persistence

- [x] 6.1 Rewrite `PersistAsync` to write every telling the partition produced in one `SaveChangesAsync`
- [x] 6.2 Implement greedy identity matching: rank (new telling, overlapping stored series) pairs by overlap descending, claim each stored series at most once, create new rows for unmatched tellings
- [x] 6.3 Delete unclaimed overlapping stored series, adopting their chosen title and picture into a claiming telling that has none, as today
- [x] 6.4 Write `IsMainLine`, `Order`, `RelationGroup` and `IsPrimary` per membership; preserve `FavouriteRank` on a membership that survives
- [x] 6.5 Mark partial/truncated on every telling a budget-exhausted or capped build produced
- [x] 6.6 Tests: a stored combined series splits, larger telling keeps the id/title/picture; a rebuild with no membership change shuffles no identities; favourite ranks survive a split

## 7. Read path and projection

- [x] 7.1 Resolve `FindSeriesAsync`/`FindSeriesIdAsync` through `IsPrimary`
- [x] 7.2 Project extras grouped by `RelationGroup` in the fixed display order, replacing the media-type grouping in `SeriesService.ProjectAsync`
- [x] 7.3 Project related entries: collect the untraversed relations of the series' main-line members in both directions, drop ids already members, group and order them as extras are, and render each from `AnimeRelatedAnime.Title`/`PictureUrl`/`MediaType` enriched from `AnimeMetadata` and `UserAnimeEntry` where present
- [x] 7.4 Exclude related entries from every average, stat, episode and runtime total, and member count
- [x] 7.5 Extend `SeriesDto`/`SeriesEntryDto` with the relation group and a related-entry flag (no series id for boundary members — the tile links by anime id, per design D10)
- [x] 7.6 Tests: related entries appear without a fetch and do not mark the series partial; a member is never duplicated as a related entry; averages ignore related entries

## 8. Callers of the old single-membership assumption

- [x] 8.1 `Services/Relations/RelationResolver.cs` — series-neighbour fallback resolves through the primary membership; any "same series" check becomes "shares any series"
- [x] 8.2 `Services/Detail/AnimeDetailService.cs` — the in-a-series check and the series link resolve through the primary membership
- [x] 8.3 `Services/Artwork/ArtworkSelectionService.cs` and `Services/Artwork/PictureRefreshService.cs` — scope their member queries by series id rather than assuming one row per anime. **Already correct**: both take a `seriesId` parameter and scope every query by it (never by a bare `AnimeId ==` lookup), so an anime's multiple memberships were never a hazard here — no code change needed.
- [x] 8.4 `Services/Search/SeriesSearchLookup.cs` and `Services/Series/SeriesRankingIndex.cs` — group by series so an anime shared between tellings contributes to each, and no series is listed twice. **Already correct**: both load the whole `SeriesMembers ⋈ Series` join and key every downstream computation (`ToLookup`/`Dictionary` by `SeriesId`) — a shared anime's two rows (one per series) already land in two separate groups, and grouping by `SeriesId` already rules out listing one series twice. No code change needed.
- [x] 8.5 `Services/Series/SeriesBulkBuildBackgroundService.cs` — target anime with no up-to-date **primary** membership; count a target processed when a build in the same run gave it one

## 9. Refresh triggers a build

- [x] 9.1 At the point a relation discovery is recorded on a metadata write, enqueue the owning anime on `ISeriesBuildTrigger`
- [x] 9.2 Verify the enqueue is non-blocking and deduplicated, so one refresh pass over a franchise produces one build
- [x] 9.3 Tests: a discovered relation enqueues; a rewrite with no new edge enqueues nothing; a discovered `alternative_version` results in two stored tellings

## 10. Frontend

- [x] 10.1 Update the series API types with the relation group and the related-entry flag
- [x] 10.2 Replace `groupExtras` in `pages/SeriesPage.tsx` with relation-group bucketing in the fixed display order, merging extras and related entries into the same groups
- [x] 10.3 Add the media-type button row above the More section: one button per media type present, multi-select, none selected by default, stating which are selected
- [x] 10.4 Apply the type filter across every group alongside the "in my list" filter and the per-group state; make heading counts report what the type filter admits; hide groups left empty while a type is selected
- [x] 10.5 Register the type filter in the page's restorable state beside `moreCollapsedGroups`/`moreUnfilteredGroups`, ignoring restored types no extra carries
- [x] 10.6 Show media type on every More tile, since groups no longer share one (`components/SeriesExtraTile.tsx`). **Already correct**: the tile's meta line has unconditionally rendered `mediaTypeLabel(entry.mediaType)` since before this change — no code change needed.
- [x] 10.7 Link Alternative version and Alternative setting tiles to `/series/{animeId}` for that anchor; confirm an unstored telling is built by the visit rather than erroring
- [x] 10.8 Leave the timeline untouched by the type filter; verify watch-order numbering is unaffected

## 11. Verification

- [x] 11.1 Build the backend (sdk:10.0 image) and the frontend (node 22), and run the backend test suite — 747/747 passing
- [x] 11.2 Walk a real alternative-version-shaped franchise end to end. Used the dev DB's real cached Fate/stay night graph (far messier than a two-telling FMA case: ~60 relation edges, 9+ version anchors, several tellings sharing a common eligible prequel) via the running dev stack, rebuilt live, and confirmed in the browser: 13 distinct series, each with its own root/title/card, exclusive entries unshared, shared/boundary entries correctly placed, Alternative version/setting tiles linking through to the sibling telling's own page. **Found and fixed a real bug in the process**: a member equidistant (and chronologically earliest) across multiple sibling tellings — e.g. a prequel common to several alternate versions — could become *every one* of those tellings' `RootAnimeId` at once, crashing on the column's unique index. Fixed by having root selection prefer a main-line member not shared with a sibling telling (falling back to the plain earliest member only when none exists); added a regression test (`ASharedEarliestPrequelDoesNotBecomeRootOfMoreThanOneTelling`) and re-verified live.
- [x] 11.3 Walked a real franchise with no *disruptive* version relations (One Piece) via the dev stack: its own main line, stats and card were unaffected; a newly-announced remake tagged `alternative_version` split off as its own telling and appeared only as an Alternative version tile, exactly as designed.
- [x] 11.4 Confirmed live: a real stored series (One Piece, built the day before `ClassificationRevisedAt`) rebuilt automatically on a plain page visit (no Rebuild click), advancing `BuiltAt` and completing without going partial. "Build all series from my list" is covered by `SeriesBulkBuildBackgroundServiceTests` (including the boundary-membership-doesn't-count-as-covered case) — not re-run live against the full list to avoid a long, MAL-rate-limited bulk run against real account data.
