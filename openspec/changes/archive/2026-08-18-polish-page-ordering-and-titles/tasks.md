## 1. Backend: orderings

- [x] 1.1 In `MainDashboardService.cs`, extract the currently-watching ordering into `internal static IEnumerable<UserAnimeEntry> OrderCurrentlyWatching(IEnumerable<UserAnimeEntry> entries)` returning `OrderByDescending(e => e.EpisodesWatched).ThenBy(e => e.Anime.Title, StringComparer.OrdinalIgnoreCase)`, and call it in place of the existing `.OrderBy(e => e.Anime.Title, …)` on the `Status == WatchStatus.Watching` filter (design decision 1/2). Leave the `airingToday` and `currentSeason` orderings untouched.
- [x] 1.2 Add `backend/AnimeTracker.Api.Tests/Services/Dashboard/MainDashboardServiceOrderingTests.cs` covering `OrderCurrentlyWatching`: most-watched first; equal counts fall back to case-insensitive title order; ordering ignores `TotalEpisodes` (a 100-of-unknown entry precedes a 40-of-50 entry). No fakes required — construct `UserAnimeEntry`/`AnimeMetadata` directly.
- [x] 1.3 In `ProfileService.BuildRewatchedSection`, delete the `.ThenByDescending(e => e.MyScore ?? -1)` tie-break so ordering is rewatch count descending, then `Anime.Title` case-insensitively (design decision 3).
- [x] 1.4 Update the comment block above `BuildRewatchedSection` — it currently documents "Ties break by score (unscored last), then title", which the change makes false.
- [x] 1.5 Add a rewatched-ordering test alongside the existing profile tests (reusing that file's fake repositories) asserting that two entries with equal rewatch counts order alphabetically regardless of `MyScore`.

## 2. Backend: series status and stats

- [x] 2.1 In `SeriesService.ComputeStatus`, change the final line to `return anyUpcoming ? "Ongoing" : "Finished";` so an announced-but-unaired member reads `Ongoing`. Leave the `currently_airing` and `Upcoming` branches unchanged (design decision 5).
- [x] 2.2 Update the `SeriesDto.cs` XML doc on `SeriesDto.Status`, which enumerates `"Finished · sequel upcoming"` as a possible value.
- [x] 2.3 Add `int ExtrasCompleted` to `SeriesStatsDto` (place it directly after `EntriesCompleted`) and extend the record's XML doc to say it counts extras marked Completed.
- [x] 2.4 In `SeriesService.BuildStats`, compute `extrasCompleted` as `extraAnime.Count(a => a.UserEntry?.Status == WatchStatus.Completed)` beside the existing `entriesCompleted` line, and pass it in the `new SeriesStatsDto(...)` positional argument list (design decision 6).
- [x] 2.5 Add a `SeriesServiceBuildStatsTests` case asserting `ExtrasCompleted` counts only completed extras and is 0 for a series with no extras, while `EntriesCompleted` still counts only the main line.
- [x] 2.6 Grep the backend for any remaining `"Finished · sequel upcoming"` occurrence and confirm none remain.

## 3. Frontend: types

- [x] 3.1 In `frontend/src/api/types.ts`, narrow `SeriesStatus` to `'Ongoing' | 'Upcoming' | 'Finished'`.
- [x] 3.2 In the same file, add `extrasCompleted: number` to the series stats type, beside `entriesCompleted`.

## 4. Frontend: home page

- [x] 4.1 In `CurrentSeasonSection.tsx`, replace `useState<SortKey>('popularity')` with `useRestorableState<SortKey>('currentSeasonSort', 'popularity')` and import the hook from `../hooks/useRestorableState.ts` (design decision 4). No other change to the component.
- [x] 4.2 Verify by hand: set the sort to MAL score, open a card, press back — the sort is still MAL score; then reach Home via the navbar link — the sort is back on Popularity.
- [x] 4.3 Verify by hand that "Currently watching" leads with the highest episodes-watched entry on a fresh load.
- [x] 4.4 Verify the reorder timing by hand: increment a card enough times that it should lead the row and confirm it does **not** move (nor does the scroll offset) while you stay on the page; then reload — it leads the row. Repeat via the navbar, and via back from the anime's detail page, where it must paint in the old order with no loading state and then settle into the new order in place.
- [x] 4.5 Confirm no client-side sort was added to `CurrentlyWatchingCarousel` or `HomePage` — `handleEpisodesWatchedChange` must keep patching with `.map()` so order is preserved, and ordering stays solely in `MainDashboardService` (design decision 1).

## 5. Frontend: series page — status, scores, stats

- [x] 5.1 In `SeriesPage.tsx`, remove the `'Finished · sequel upcoming'` entry from `SERIES_STATUS_CLASS`. The `Record<SeriesStatus, string>` typing means this must land together with task 3.1 for the build to pass.
- [x] 5.2 Delete the `score-chip__count` span from `MalScoreChip`, leaving the `ScoreValue` alone inside the chip.
- [x] 5.3 Change `formatAverage` to return just the two-decimal value (or `'No score'`), dropping the `· N of M scored` suffix, so `MineScoreChip` shows the average alone.
- [x] 5.4 If `.score-chip__count` in `ScoreChip.css` has no remaining consumer after 5.2/5.3, remove the rule; check with a grep across `frontend/src` first.
- [x] 5.5 Replace the "Entries completed" `<dd>` with two labelled figures — main line (`entriesCompleted` of `mainLineCount`) and extras (`extrasCompleted` of `extrasCount`) — rendering the extras row only when `stats.extrasCount > 0` (design decision 8). Add the CSS needed for the two-row figure inside one `<dd>`.
- [x] 5.6 Compute `timeLeft` and `runtimeUnknown` once before the stats grid and wrap both the "Time watched" and "Time left" cells in a single `{(timeLeft > 0 || runtimeUnknown) && (…)}` guard, per design decision 7's formula. Both must appear and disappear together.
- [x] 5.7 Verify by hand across three series: one finished and fully watched (no time stats, `Finished` pill), one with an announced sequel (`Ongoing` pill in the green treatment, `Caught up` badge rather than `Completed`), and one part-watched (both time stats present).

## 6. Frontend: series page — title placement

- [x] 6.1–6.5 **Reverted.** Implemented (extracted the `Series` label and `<h1>` into a standalone header block above the hero, renaming `series-page__header` → `series-page__hero`), then reverted after live review: with real data, the standalone block read worse than the original — it separates the title from the poster it names and adds a redundant row. The title stays exactly where it was, beside the poster inside `series-page__header-info`, unchanged. `design.md` decision 9 and the `page-header-design`/`series-page` spec deltas were updated to match — the Series page is no longer part of this change's header-block work.

## 7. Frontend: anime detail title placement

- [x] 7.1 In `AnimeDetailPage.tsx`, wrap the existing `anime-detail-page__top` row in a new `anime-detail-page__header` block and drop the now-redundant bare `<div>` around the `<h1>` (design decision 9).
- [x] 7.2 In `AnimeDetailPage.css`, add the `.anime-detail-page__header` rule owning the block's spacing above the title, and move the `h1` rule to `.anime-detail-page__header h1` with `margin: 0`, `font-size: 24px`, `font-weight: 600`, `letter-spacing: normal` — the controls-sharing-row treatment used by My List and Search.
- [x] 7.3a Live-review fix: `.anime-detail-page__top` switched from `align-items: flex-start` to `align-items: center`, and the hand-tuned `padding-top: 8px` on `.anime-detail-page__related` removed — that offset was calibrated for the previous 28px title and misaligned the related-entry links against the now-24px title.
- [x] 7.3b Live-review fix, round 1: `.anime-detail-page__header`'s `padding-top` raised from `8px` to `16px`. Round 2, after further review: too much — removed entirely, so the title's spacing above it is just the page's own uniform padding, matching how My List and Search carry no extra padding on their own header rule either. `.anime-detail-page`'s section gap also trimmed from `24px` to `16px`, tightening the space between the title row and the body below it (its only two flex children, since the related overlay renders as a modal outside the flex flow).
- [x] 7.3 Verify by hand that the title no longer sits flush against the top of the content area, that it lines up with the related-entry links (Series, Main series, More, Prequel, Sequel) on its row, and that the body below is unmoved.

## 8. Verification

- [x] 8.1 Backend: `dotnet test` (compile via the `sdk:10.0` Docker image — the local SDK is 9.0 and a `~/Documents` bind-mount fails).
- [x] 8.2 Frontend: `npm run build` for the type-check under nvm's Node 22 (the default Node 16 cannot run Vite), confirming the narrowed `SeriesStatus` union and the new `extrasCompleted` field both type-check.
- [x] 8.3 Frontend: `npm run lint`.
- [x] 8.4 Walk the four pages against the change's spec deltas: Home (carousel order, sort restoration), Profile (most-rewatched tie order), Series (pill, chips, stats, title), anime detail (title).
- [x] 8.5 Run `openspec validate polish-page-ordering-and-titles` and confirm every task above is checked before archiving.

## 9. Live-review fixes outside the original task list

- [x] 9.1 Backend: `SeriesService.YearSpan` computed the series' last year as the latest member's *start* year (`AiredFrom.Max().Year`) instead of its *end* year — a multi-cour or long-running latest entry understated how far the franchise runs. Fixed to take the max of each member's `AiredTo` year, falling back to `AiredFrom` when `AiredTo` isn't known yet (currently airing/not yet aired).
- [x] 9.2 Frontend: `AnimeCardMeta` (season/search browse cards) and `AnimeDetailPage`'s Type field rendered the raw MAL media-type value uppercased (e.g. `TV_SPECIAL`) instead of going through `mediaTypeLabel`, which every other media-type display in the app already uses. Both now call `mediaTypeLabel`.
- [x] 9.3 Backend re-tested (104/104 passing) and the `backend` Docker container rebuilt + restarted so the year-span fix is live for manual verification.
