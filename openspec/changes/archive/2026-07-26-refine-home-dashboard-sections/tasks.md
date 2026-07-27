## 1. Backend: dashboard payload fields

- [x] 1.1 Add `int? EpisodeNumber` to `AiringTodayItemDto` in `backend/AnimeTracker.Api/Services/Dashboard/MainDashboardDto.cs`
- [x] 1.2 Add `bool FinishedAiring` to `CurrentSeasonItemDto` in the same file, fed from `e.Anime.AiringStatus == "finished_airing"` (design decision 3)
- [x] 1.3 In `MainDashboardService.GetDashboardAsync`, pass `x.Episode!.EpisodeNumber` through to the airing-today DTO instead of discarding it, and populate `FinishedAiring` on each current-season item
- [x] 1.4 Mirror both fields in `frontend/src/api/types.ts`: `episodeNumber: number | null` on `AiringTodayItemDto`, `finishedAiring: boolean` on `CurrentSeasonItemDto`

## 2. Backend: current-season membership

- [x] 2.1 Add `using AnimeTracker.Api.Services.Season;` to `MainDashboardService` and resolve `SeasonCalendar.GetSeasonFor(today)` once per dashboard load
- [x] 2.2 Replace the `AiringStatus == "currently_airing"` filter with the union rule from design decision 1: currently airing, OR (`AiredFrom` is set and on/before `today`, AND `SeasonCalendar.GetSeasonFor(AiredFrom)` equals the current season)
- [x] 2.3 Add a short comment above the filter explaining why the season quarter is derived from `AiredFrom` rather than read from `SeasonAnimeListing` (listing rows exist only for seasons the user has browsed, which would make the section's contents depend on browsing history)
- [x] 2.4 Confirm no new constructor dependency was needed — `SeasonCalendar` is static and `today` already exists in the method

## 3. Backend: bound the weekly aired-count estimate

- [x] 3.1 In `EpisodeScheduleService.EstimateAiredFromCadence`, clamp the reference date to `min(today, LocalDateOfJstBroadcast(AiredTo, broadcastTime))` when `AiredTo` is set, keeping today's date when it is not (design decision 5)
- [x] 3.2 Guard the existing "today's episode hasn't aired yet" adjustment so it runs only when the reference date was not clamped, otherwise it compares the current wall-clock time against a slot on a past date
- [x] 3.3 Add a comment recording why `EstimateLastLocalDate` is deliberately *not* reused here (its `MinimumEpisodeEstimate` fallback would fabricate a 12-episode ceiling)
- [x] 3.4 Build the backend (`dotnet build` via the sdk:10.0 Docker image per project memory) and confirm no warnings from the touched files

## 4. Frontend: currently-watching progress bar

- [x] 4.1 In `CurrentlyWatchingCarousel.tsx`, replace the `carousel__progress-row` span with `<ProgressBar watched={item.episodesWatched} total={item.totalEpisodes} onIncrement={() => increment(item)} incrementPending={pendingId === item.animeId} incrementLabel={...} />`, importing `ProgressBar` and dropping the now-unused `IncrementButton` import and local `atMax`
- [x] 4.2 Update the component's header comment so it describes the bar-plus-count row rather than the bare count
- [x] 4.3 Remove the dead `.carousel__progress-row` and `.carousel__progress` rules from `CurrentlyWatchingCarousel.css`
- [x] 4.4 Verify on the running app that the bar fills correctly, `watched/?` renders with an empty track for an unknown total, the plus still increments without navigating, and the fill grows on increment

## 5. Frontend: airing-today row layout

- [x] 5.1 In `AiringTodayList.tsx`, restructure each row as thumbnail + a text column: first line `{localTime} : Ep {episodeNumber}` (time alone when `episodeNumber` is null), second line the display title
- [x] 5.2 In `AiringTodayList.css`, enlarge `.airing-today__thumb` to roughly 48×66 (2:3 poster ratio) and add the text-column, meta-line, and two-line `-webkit-line-clamp` title rules following the `.anime-card__title` pattern — but do *not* copy that rule's `min-height: 2.6em`, which would reserve a second line for titles that don't need one (the thumbnail already sets the row height)
- [x] 5.3 Update the component's header comment to describe the two-line row
- [x] 5.4 Verify a long title ellipsises at the end of its second line, a title that fits renders in full with no ellipsis, a row with no resolvable episode number shows the time alone, and rows stay aligned in the 320px sidebar

## 6. Frontend: airing progress bar

- [x] 6.1 Add a `finished: boolean` prop to `AiringProgressBar` and pass `item.finishedAiring` from `CurrentSeasonSection`
- [x] 6.2 Implement the five-case fill table from design decision 4: proportional when the total is known; full track when the total is unknown and the run is finished; half track when the total is unknown, the run is unfinished, and the aired count is known; empty otherwise
- [x] 6.3 Measure the purple watched fill against `max(aired ?? 0, watched)` scaled into the blue extent whenever the total is unknown, so it never overshoots the blue fill; keep it measured against the total otherwise
- [x] 6.4 Keep the label logic as-is (`aired/?`, `?/total`, `?/?`) and extend the `aria-label`/`title` description so the half-track and finished cases read sensibly for screen readers
- [x] 6.5 Update the component comment to record why an unfinished unknown-total bar is a fixed half while a finished one is full — and fix the stale "green" in that comment, since the watched fill is `var(--accent)` (purple)
- [x] 6.6 Verify against a still-airing no-total show (half bar), one watched to the aired count (purple covers the whole blue half), one watched well short of the aired count (purple proportional inside the half — 2 of 8 aired lands at a quarter of the blue extent, not a half), a finished no-total title (full bar), a finished movie watched (full bar, fully purple, `?/?`), and a still-airing show with neither count (empty bar, `?/?`)

## 7. Frontend: home page state and copy

- [x] 7.1 In `HomePage.handleEpisodesWatchedChange`, also map `currentSeason` by `animeId` so an increment from the carousel updates that anime's purple fill in "Followed shows airing" (design decision 8), with a comment noting the same show can appear in both sections
- [x] 7.2 Update `CurrentSeasonSection.tsx`'s header comment — it says "my-list anime airing this season", which no longer covers finished current-season titles
- [x] 7.3 Update the section's empty-state string ("Nothing from my list is airing this season") to fit the widened rule, e.g. "Nothing from my list this season"

## 8. Verification

- [x] 8.1 Build the frontend with node 22 via nvm (per project memory) and confirm a clean `tsc` + Vite build
- [x] 8.2 Load the home page and confirm a finished current-season title (movie, short, or completed TV run) stays listed with a full aired bar, and that a not-yet-premiered current-season title does not appear
- [x] 8.3 Confirm a long-finished show with no published episode total no longer reports a runaway aired count (task 3.1's bound)
- [x] 8.4 Confirm nothing regressed in the weekly airing grid, My List, or the anime detail page (all still use the unchanged watched/total bar)
- [x] 8.5 Run `openspec validate refine-home-dashboard-sections --strict` and fix any reported issues
