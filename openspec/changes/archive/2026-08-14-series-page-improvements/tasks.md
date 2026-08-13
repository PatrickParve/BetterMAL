## 1. Backend: episode/runtime lower bound uses aired count

- [x] 1.1 In `SeriesService.cs`, change `EpisodesAndRuntime` to accept the per-anime aired-episode figure (reuse whatever map/lookup `BuildStats`'s caller already built for `mainLineAiredEpisodes`) and, when `TotalEpisodes` is null, add `AiredEpisodes ?? 0` to `episodes` and `(AiredEpisodes ?? 0) * EpisodeSeconds(a)` to `runtimeSeconds` instead of contributing nothing, while still setting `hasUnknown = true`.
- [x] 1.2 Confirm `BuildStats`'s call sites for `EpisodesAndRuntime(mainLineAnime)` and `EpisodesAndRuntime(extraAnime)` both have the aired-episode figure available per anime (extras keep the same fallback behavior as main line, for consistency, even though extras aren't marked as a lower bound anywhere in the UI today).
- [x] 1.3 Add/update backend unit tests covering: an entry with unknown total and a known aired count contributes that aired count; an entry with unknown total and no aired count contributes zero (unchanged prior behavior); `HasUnknownEpisodeCounts` still flips in both cases.

## 2. Backend: "Most rewatched" stat

- [x] 2.1 Add `List<int> MostRewatchedAnimeIds` to `SeriesStatsDto` (`SeriesDto.cs`), documented the same way `HighestMalScoreAnimeIds`/`MyHighestScoreAnimeIds` are.
- [x] 2.2 In `SeriesService.BuildStats`, compute it via the existing `TiedTopIds` helper over `orderedMembers.Where(m => m.Anime.UserEntry?.RewatchCount is > 0)` keyed by `RewatchCount`, no tie-break key (defaults to watch order) — same call shape as the existing `highestMalIds`/`highestMineIds` lines.
- [x] 2.3 Add backend tests: no member rewatched → empty list; one member rewatched → that id only; two members tied for the max rewatch count → both ids, in watch order.

## 3. Frontend: rewatch indicators

- [x] 3.1 In `SeriesEntryRow.tsx`, render a small rewatch badge (e.g. `↻ {n}`) when `entry.entry?.rewatchCount` is truthy — no new prop needed, the field is already on the payload.
- [x] 3.2 Apply the same badge to `SeriesExtraTile.tsx`.
- [x] 3.3 In `SeriesPage.tsx`'s Series stats grid, add a "Most rewatched" `<dt>/<dd>` block using `stats.mostRewatchedAnimeIds` resolved via the existing `findEntry` helper, rendered as a `<ul className="series-page__tie-list">` (same pattern as Highest MAL score / My favourite) showing each entry's title and its `rewatchCount`; omit the whole block when the list is empty.
- [x] 3.4 Add `mostRewatchedAnimeIds: number[]` to the `SeriesStatsDto`/`SeriesStats` frontend type in `api/types.ts`.
- [x] 3.5 Style the badge and the new stats block (reuse `.series-page__tie-list` styling; add a small badge class shared by the row/tile CSS files).

## 4. Frontend: merge Timeline and Main series into one section

- [x] 4.1 Rewrite `SeriesTimeline.tsx`'s `TimelineBlock` into a full info card: watch-order rank, picture, linked title, meta line (media type · year · episode count), and the existing `airedFigureLabel`/`watchedFigureLabel` helpers imported from `SeriesEntryRow.tsx` (do not duplicate their logic).
- [x] 4.2 Add my list status text and an Edit button to the card, wired to the same `onEdit` callback `SeriesPage.tsx` already passes to `SeriesEntryRow`/`SeriesExtraTile`.
- [x] 4.3 Replace the per-entry `scoreBarHeight`/`.series-timeline__score-bar` vertical tick bars with a numeric MAL/mine score chip pair styled like `.series-extra-tile__chip--mal`/`--mine` (same colour variables, same visual treatment), keeping the existing `ScoreValue` component for the MAL side's hide-scores/reveal behavior.
- [x] 4.4 Add the rewatch badge (task 3.1's pattern) to the timeline card too.
- [x] 4.5 Redesign the undated-entries treatment: replace the current second "No air date" row with cards in a trailing lane sharing the axis container, each carrying a visually distinct (e.g. dashed border) "No air date" tag in place of the year.
- [x] 4.6 Keep the existing flex-grow-on-day-count axis math (`dayNumber`, `entryEndDay`, gap segments, longest-gap marker) unchanged — only the block/card content and undated-entries layout change.
- [x] 4.7 In `SeriesPage.tsx`, remove the "Main series" `<section>`'s `<ol>` of `<SeriesEntryRow>` items; the "Timeline" section (renamed heading, e.g. "Main series") becomes the sole main-line presentation. Update the section heading/copy accordingly.
- [x] 4.8 Verify `SeriesEntryRow.tsx` and its exported `airedFigureLabel`/`watchedFigureLabel` are still used (by `SeriesExtraTile.tsx` and the new timeline card) and not dead-code-eligible for removal.
- [x] 4.9 Update `SeriesTimeline.css` for the new card layout; check horizontal scroll behavior and minimum card width against a large real franchise (e.g. One Piece) locally.

## 5. Frontend: Highest MAL score title gating

- [x] 5.1 In `SeriesPage.tsx`'s Highest MAL score tie-list rendering, branch each `<li>` on `isCompletedAndScored(entry)`: when true, render the existing title `<Link>` + score as today; when false, render a neutral placeholder (no title text, no link, no picture) in its place, independent of the `hidden` (hide-scores) flag.
- [x] 5.2 Style the placeholder state (e.g. muted "Not yet watched" text) in `SeriesPage.css`.

## 6. Frontend: relocate Rebuild control

- [x] 6.1 Remove the standalone `.series-page__rebuild-row` section between the hero header and Series stats.
- [x] 6.2 Move the Rebuild `<button>` and the partial/truncated `<span className="series-page__notice">` elements into the Series stats section's heading row, aligned as a secondary action next to "Series stats" — no change to `handleRebuild`, `rebuildCount`, or `MAX_REBUILD_ROUNDS` logic.
- [x] 6.3 Update `SeriesPage.css` (`.series-page__rebuild-row` rules move/adapt to the stats-section heading; add a heading-row flex layout if the section doesn't already have one).

## 7. Frontend: More-section collapse suppression and wording

- [x] 7.1 In `SeriesPage.tsx`, compute `allExtrasCompleted = series.extras.length > 0 && series.extras.every((e) => e.entry?.status === 'Completed')`.
- [x] 7.2 When `allExtrasCompleted` is true: skip rendering the per-group toggle button/caret and the "+N more" hint (always show `group.items` in full), and don't render the all-groups control at all.
- [x] 7.3 Update the all-groups control's label logic: `extrasGroups.length > 1 ? (anyExtrasGroupOpen ? 'Collapse all' : 'Expand all') : (anyExtrasGroupOpen ? 'Collapse' : 'Expand')`, only rendered when `!allExtrasCompleted`.
- [x] 7.4 Confirm the >12-extras auto-collapse initialization (`EXTRAS_COLLAPSE_THRESHOLD`) is skipped/irrelevant when `allExtrasCompleted` (groups render expanded regardless of the stored `collapsedGroups` state in that case).

## 8. Verification

- [x] 8.1 Run backend tests (`dotnet test`) covering the new `EpisodesAndRuntime` and `MostRewatchedAnimeIds` behavior.
- [x] 8.2 Manually load the series page for: a franchise with an unpublished-total currently-airing entry (verify episode/runtime totals now show a real lower bound), a franchise with at least one rewatched entry and one with none (verify the stat and badges appear/disappear correctly), a franchise with an undated main-line entry (verify the trailing-lane treatment), a series where the highest-MAL entry is unwatched (verify title withheld) and one where it's watched+scored (verify shown), a series with one extras group fully completed (verify no collapse control) and one with several groups partially watched (verify "all" wording).
- [x] 8.3 Verify the Rebuild control still works from its new location and the partial/truncated notices still render correctly beside it.
- [x] 8.4 Run frontend type-checks/build (`npm run build` or equivalent) to confirm the `SeriesStatsDto` type change and component rewrites are consistent end-to-end.
