## 1. Dropped scores — shared predicates

- [x] 1.1 Add `isScoreRevealableStatus(status: WatchStatus | null | undefined): boolean` to `frontend/src/utils/anime.ts`, returning true for `'Completed'` and `'Dropped'` only, with a comment naming the `score-visibility` rule it encodes (design decision 1).
- [x] 1.2 Add the backend equivalent (a `WatchStatus` extension/static helper next to the model or in `Services/Profile`), returning true for `WatchStatus.Completed` and `WatchStatus.Dropped`.
- [x] 1.3 Update the Settings page label to "Always show MAL scores for completed and dropped shows" and reword the hint above it to mention dropped shows (`pages/SettingsPage.tsx`, the "Score display" box).
- [x] 1.4 Confirm `context/ScoreVisibilityContext.tsx` is unchanged — the `bettermal.alwaysShowCompletedScores` storage key and its values must be preserved so an enabled setting stays enabled (design decision 2). Update the file's comments only if they name "completed" as the sole covered status.

## 2. Dropped scores — client call sites

- [x] 2.1 Route `components/MyListRow.tsx`'s `ScoreValue` `completed` prop through `isScoreRevealableStatus`.
- [x] 2.2 Same for `components/SeriesEntryRow.tsx` and `components/SeriesExtraTile.tsx`.
- [x] 2.3 Same for `components/SeriesTimeline.tsx` (its local `completed` variable).
- [x] 2.4 Same for `pages/AnimeDetailPage.tsx`.
- [x] 2.5 Same for all three `ScoreValue` call sites in `pages/TopAnimePage.tsx` (rank 1–3 showcase, rank 4–10 row, and the list rows).
- [x] 2.6 Re-run `grep -rn "completed=" frontend/src` and confirm every remaining hit either routes through `isScoreRevealableStatus` or takes a server-computed flag (`malRevealed`, `isCompleted`) — no inline `=== 'Completed'` left (design risk 1).

## 3. Dropped scores — server-computed flags

- [x] 3.1 Widen `RecapService.ToRow`'s reveal flag and the hot-take projection's `MalRevealed` to the settled predicate from 1.2.
- [x] 3.2 Widen `ProfileService.ToDivergenceItem`'s `IsCompleted` flag to the settled predicate. Leave the DTO field name as is (it is the wire contract for `ScoreValue`'s `completed` prop) but update its doc comment to say completed-or-dropped.
- [x] 3.3 Rename `SeriesAverages.MainLineCompletedByMe` to `MainLineSettledByMe` and widen it: a group qualifies when it has at least one finished-airing member and every finished-airing member is Completed **or** Dropped. A member not in my list (`EntryStatus` null) must still fail.
- [x] 3.4 Update `SeriesRankingIndex`'s `malRevealed` computation for the rename, and check every other call site the rename surfaces.
- [x] 3.5 Widen `pages/SeriesPage.tsx`'s `isGroupCompleted` / `malGroupRevealed` to match 3.3 exactly, so the client and server group rules agree.
- [x] 3.6 Leave `pages/SeriesPage.tsx`'s `isCompletedAndScored` (the highest-MAL-score box) on completed-only and add a comment recording that this is deliberate (design Non-Goals).

## 4. Dropped scores — tests

- [x] 4.1 Extend `backend/AnimeTracker.Api.Tests/Services/Series/SeriesAveragesTests.cs`: a main line whose finished-airing members are a mix of Completed and Dropped is settled; one whose finished-airing member is Watching/On-hold/Plan-to-watch is not; one with a finished-airing member absent from my list is not.
- [x] 4.2 Add coverage that the series header's `Completed`/`Caught up` badge is still completed-only after the rename (design risk 2).
- [x] 4.3 Extend the recap tests so a dropped entry's row and hot take come back with the reveal flag set.
- [x] 4.4 Extend `ProfileServiceOpinionDivergenceTests.cs` so a dropped entry in either divergence list carries the reveal flag.

## 5. Navbar layout

- [x] 5.1 In `components/Navbar/Navbar.tsx`, move the Recap link so it renders directly after My List in the left group, keeping it built at render time (the current-year URL must not be hoisted into the module-scope `NAV_LINKS` array — design decision 4).
- [x] 5.2 Move `<SearchBar />` out of its own `navbar__search` wrapper and into `navbar__links--right`, as that group's first child, followed by the score toggle, Profile, then Settings. DOM order must equal visual order so tab order follows the layout.
- [x] 5.3 Update `Navbar.css`: drop the standalone centred `.navbar__search` flex child, give the search field a flexible basis with a floor inside the right group, and keep the left group / right group split. The right group keeps `justify-content: flex-end`.
- [x] 5.4 Retarget the `max-width: 900px` wrap rule so the search field still wraps onto its own row, with both groups keeping their internal order.
- [x] 5.5 Verify at desktop, ~1000px, and ~700px widths: no overflow, no horizontal document scrollbar (the `navigation-and-search` "Pages do not scroll or drift horizontally" requirement), the type-ahead dropdown anchored under the field and not clipped at the window's right edge, and hover/active states unchanged on every control.

## 6. Profile — episode progress (backend)

- [x] 6.1 Add `EpisodeProgressDto(int EpisodesWatched, int EpisodesTotal, int EntriesCounted, int TotalEntries)` to `Services/Profile/ProfileDto.cs` and a field for it on `ProfileDto`.
- [x] 6.2 Build it in `ProfileService` from the entry list `GetProfileAsync` already loads: skip entries whose `Anime.TotalEpisodes` is null; each counted entry contributes `Min(EpisodesWatched, TotalEpisodes)` watched and `TotalEpisodes` total; `EntriesCounted` is the number not skipped and `TotalEntries` is the full list count.
- [x] 6.3 Add tests: unknown-total entries excluded from all three counted figures but present in `TotalEntries`; plan-to-watch entries contribute total and no watched; a stored over-count clamps; a rewatched entry contributes at most its total.

## 7. Profile — favourite seasons and years (backend)

- [x] 7.1 Move `RecapService.ScoredMean` to a shared location beside `RecapRankingBuilder` so `ProfileService` calls the same function (design decision 6); update `RecapService` to use it from there.
- [x] 7.2 In `ProfileService`, build the whole-list ranking inputs: the entries whose `Anime.AiredFrom` is non-null, and a `RecapPeriod.MultiYear(minYear, maxYear)` spanning the earliest to the latest of their air years.
- [x] 7.3 Call `RecapRankingBuilder.BuildSeasonRanking` and `BuildYearRanking` with that period, the air-dated entries, and the whole-list scored mean. Return empty lists when there are no air-dated entries or the mean is null.
- [x] 7.4 Add `List<RecapSeasonRankingDto> FavouriteSeasons` and `List<RecapYearRankingDto> FavouriteYears` to `ProfileDto` and populate them in `GetProfileAsync`.
- [x] 7.5 Add tests: a season's weighted score from the profile equals the score `RecapRankingBuilder` produces for the same season under a recap period covering it; groups with no scored anime are omitted; anime with no air date contribute to neither ranking; rank 1 carries top-three posters and later ranks carry none; empty list and nothing-scored cases return empty.

## 8. Profile — client wiring

- [x] 8.1 Mirror the new DTO fields in `frontend/src/api/types.ts` (`EpisodeProgressDto`-equivalent plus the two ranking arrays on `ProfileDto`), reusing the existing `RecapSeasonRankingDto` / `RecapYearRankingDto` types.
- [x] 8.2 Extract `describeSeasonRanking` and `describeYearRanking`, the `VISIBLE_RANK_COUNT = 5` slice, the inline row `<ol>` renderer, the poster renderer, and the "See all N …" control out of `pages/RecapPage.tsx` into a shared `components/RankingSection.tsx` beside `RankingOverlay` (design decision 8).
- [x] 8.3 Rewire `RecapPage` onto the extracted component — including both time-watched rankings — and confirm its rendered output, class names, and overlay behaviour are unchanged by the move.

## 9. Profile — new sections (UI)

- [x] 9.1 Render the episode-progress section as its own full-width `profile-box` beneath the top row of boxes, using `components/ProgressBar` with no `onIncrement` and no `onSetWatched` so it stays read-only.
- [x] 9.2 State the counted-entries figure alongside the bar ("N of M entries counted", wording to match the spec's intent), and render the plain "nothing to measure" message instead of a bar when `EpisodesTotal` is zero.
- [x] 9.3 Render "Favourite seasons" and "Favourite years" as a side-by-side pair using the extracted ranking component, each capped at five with its own "See all" overlay, wired into the existing `RankingOverlay` single-slot overlay state pattern.
- [x] 9.4 Add the pair's two-column grid to `ProfilePage.css` under its own class name, collapsing to one column at the page's existing narrow breakpoint with Favourite seasons first.
- [x] 9.5 Render the section's empty state when both rankings are empty, instead of two empty lists.
- [x] 9.6 Decide and apply the section's position on the page (below "Most rewatched", above the divergence row) and confirm no existing section's layout shifts.

## 10. Verification

- [x] 10.1 Build the backend and run the test suite (local SDK is 9.0 — compile via the `sdk:10.0` Docker image per the project's build note).
- [x] 10.2 Build the frontend with Node 22 via nvm (the default v16 cannot run the Vite build) and confirm no type errors.
- [x] 10.3 Manual pass with the hide-scores toggle on and the setting on: a dropped anime shows its MAL score in my list, on its detail page, on Top anime, in series entry rows/extras/timeline, in a recap's top ten and hot takes, and in both profile divergence lists; a watching/on-hold/plan-to-watch anime still shows the reveal control in all of them.
- [x] 10.4 Manual pass with the setting off: dropped anime show the reveal control exactly as before.
- [x] 10.5 Manual pass on a series with a mix of completed and dropped finished-airing main-line entries: both MAL averages reveal, and the profile's Top series tile for it reveals too.
- [x] 10.6 Manual pass on the profile page: the progress bar reads correctly against a hand-checked figure, and a favourite season's weighted score matches the same season's score on the recap page.
- [x] 10.7 Run `openspec validate profile-navbar-and-dropped-scores` and confirm the change is still valid.
