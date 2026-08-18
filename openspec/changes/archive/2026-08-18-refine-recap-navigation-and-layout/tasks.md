## 1. Backend: stat block

- [x] 1.1 Add `Dropped` to `RecapStatsDto` (`Services/Recap/RecapDto.cs`), positioned next to `Completed`, with the doc comment naming what it counts.
- [x] 1.2 Compute it in `RecapStatsBuilder.Build` as `included.Count(e => e.Status == WatchStatus.Dropped)`, mirroring the existing `Completed` line.
- [x] 1.3 Raise `RecapStatsBuilder.HotTakeCount` from 3 to 5 (D9).
- [x] 1.4 Extract `EpisodeSeconds` out of `RecapStatsBuilder` into a shared internal helper both it and the ranking builder call (D7), so per-group time can never drift from the headline **Time spent** stat.
- [x] 1.5 Extend `RecapStatsBuilderTests`: dropped count over a mixed-status set; five hot takes when many are eligible; fewer than five when only some are.

## 2. Backend: time-watched ranking

- [x] 2.1 Add `RecapTimeRankingDto(int Year, string? Season, long TimeSpentSeconds, int EpisodesWatched, List<RecapRankingPosterDto> TopPosters)` to `RecapDto.cs`, documenting that `Season` is null at the year level (D7).
- [x] 2.2 Add `SeasonTimeRanking` and `YearTimeRanking` lists to `RecapDto`, documenting that both are empty wherever the score rankings are.
- [x] 2.3 Implement `RecapRankingBuilder.BuildSeasonTimeRanking(airedIncluded, period, ...)`: group by `SeasonCalendar.GetSeasonFor(e.Anime.AiredFrom!.Value)` over the period's `SeasonPoints`, sum `EpisodesWatched * EpisodeSeconds(anime)` per group, drop groups with zero time, order by time desc → episodes desc → chronological.
- [x] 2.4 Implement `BuildYearTimeRanking` the same way keyed on `AiredFrom!.Value.Year` over `period.Years`.
- [x] 2.5 Attach top-three posters to the leading row only, picked by episodes watched then title (D7) — not by score, unlike the score rankings.
- [x] 2.6 Restructure `RecapService.BuildRankings` so the `ScoredMean(wholeList)` guard gates only the score rankings, and the time rankings are built whenever `rankingEligible` holds (D8) — a user who has scored nothing must still get time rankings.
- [x] 2.7 Gate the year-level time ranking to `RecapMode.MultiYear`, matching the score year ranking.
- [x] 2.8 Extend `RecapRankingBuilderTests`: ordering by time; a group with unscored anime still ranked; a group with zero episodes watched omitted; posters on the leader only and chosen by episodes; per-group times summing to the stat block's `TimeSpentSeconds`.
- [x] 2.9 Extend `RecapControllerTests` to assert the new fields are present under an aired multi-year recap and empty under a season recap and under `filter=watched`.

## 3. Frontend: API types

- [x] 3.1 Add `dropped` to `RecapStatsDto` and `RecapTimeRankingDto` plus `seasonTimeRanking`/`yearTimeRanking` to `RecapDto` in `api/types.ts`, matching the server's casing and nullability.

## 4. Nav bar entry

- [x] 4.1 Build the Recap nav entry inside `Navbar` from `new Date().getFullYear()` rather than in the module-level `NAV_LINKS` constant (D1), targeting `/recap?mode=yearly&year=<year>&filter=aired`.
- [x] 4.2 Place it in the primary link row and confirm `NavLink` marks it active on `/recap` regardless of the query string.
- [x] 4.3 Verify the recap page's existing empty-filter fallback still fires from this entry when the current year has nothing aired.

## 5. My list: scope instead of navigate

- [x] 5.1 Add optional `title` and `confirmLabel` props to `RecapPickerOverlay` (defaults preserving today's "Recap a period" / "Show recap"), so My list can present it as an apply-to-list action (D2).
- [x] 5.2 Replace `MyListPage`'s `onConfirm` navigate with a `setSearchParams` that translates the picker's `mode`/`from`/`to`/`year`/`season`/`filter` into the `recap`-prefixed scope vocabulary (D2). Do not change the overlay's output contract.
- [x] 5.3 Confirm the existing scope path (`recapScopeKey` → `getRecap` → `scopedAnimeIds` intersection) handles a picker-applied scope with no changes; fix only what the new entry point exposes.
- [x] 5.4 Add a `recapSearchFromScope()` helper in `MyListPage` — the inverse of `RecapPage`'s `myListScopeSearch` — and render it as a "View recap" link on the scope chip (D3), carrying period, filter, and media type.
- [x] 5.5 Restyle `.my-list-page__recap-button` to match the status-tab chrome and separate it from the tabs with `margin-left: auto` or a divider (D4); no structural change to the tab row.
- [x] 5.6 Verify by hand: applying a period keeps every other filter/sort/grouping control working over the scope, and dismissing the chip restores the unscoped list with those controls untouched.

## 6. Recap page: layout reorder

- [x] 6.1 Reorder `RecapPage`'s render to: period controls → lead section (stats + top anime) → season/year rankings → time-watched ranking → hot takes.
- [x] 6.2 Wrap the stat block and the top-anime section in a two-column grid, stats first in DOM order, collapsing to one column below the breakpoint (D5).
- [x] 6.3 Convert `.recap-page__stats` from a horizontal strip to a 1–2 column grid sized for the narrow column.
- [x] 6.4 Relabel the entry-count tile "Anime" → "In this period" and add the Dropped tile after Completed; order the tiles per D5.
- [x] 6.5 Confirm the top-10 section's own controls (ranking basis, media type, "See all N in my list") still render correctly inside the narrower right column.

## 7. Recap page: rankings

- [x] 7.1 Generalise `YearRankingOverlay` into `RankingOverlay` (`components/RankingOverlay.tsx` + `.css`) taking `title` and normalised rows `{ key, label, meta, to, posters }` (D6); delete the old component and its stylesheet.
- [x] 7.2 Add a per-ranking `describe()` mapping used by both the inline five-row list and the overlay, so the two representations of one ranking cannot drift (D6).
- [x] 7.3 Cap the season ranking at 5 rows and add its "See all N seasons" control opening `RankingOverlay`, matching the year ranking's existing treatment.
- [x] 7.4 Lay the season and year rankings side by side when both are present, stacking season-first below the breakpoint.
- [x] 7.5 Render the time-watched ranking as a full-width block below the pair (D-risks), titled to make its basis unambiguous, capped at 5 with its own "See all" overlay.
- [x] 7.6 Format each time row as time watched (via `formatRuntime`) plus episode count; link rows to that season's/year's own recap, consistent with the score rankings.
- [x] 7.7 Show up to three posters on the leading time row only, from the server's `topPosters`.
- [x] 7.8 Render up to five hot takes and confirm the existing empty-state note still shows when none are eligible.

## 8. Verification

- [x] 8.1 Run the backend test suite (per the project's Docker `sdk:10.0` build path) and confirm the recap tests pass.
- [x] 8.2 Build the frontend (nvm Node v22) and fix any type errors from the new DTO fields and the overlay rename.
- [x] 8.3 Walk the spec scenarios by hand: nav default lands on the current year aired; My list scopes in place and links back to the recap; a multi-year aired recap shows paired capped rankings with both overlays; a yearly recap shows a season ranking and a season-level time ranking; a `watched`-filter recap and a season recap show no rankings at all.
- [x] 8.4 Check the narrow-display fallbacks: stats above top anime, season ranking above year ranking.
