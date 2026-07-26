## 1. Backend: aired-episode count

- [x] 1.1 Add `int? EpisodesAiredAsOf(AnimeMetadata anime, DateTimeOffset nowUtc)` to `Services/Airing/IEpisodeScheduleService.cs`, documenting that `null` means "not determinable" and that the count never exceeds a known total
- [x] 1.2 Implement the cached-schedule path in `EpisodeScheduleService`: when `IEpisodeScheduleCache.TryGet` yields a non-empty schedule, return the highest `Episode` number whose `AirsAtUtc <= nowUtc` (0 when none have aired)
- [x] 1.3 Implement the weekly-cadence fallback: with `AiredFrom` and `BroadcastTime` known, derive the premiere's local date via `LocalDateOfJstBroadcast`, count whole weekly slots elapsed up to the local date of `nowUtc`, and exclude the current week's slot when its local broadcast time has not yet passed; return 0 before the premiere
- [x] 1.4 Return `null` when there is no usable cached schedule and either `AiredFrom` or `BroadcastTime` is missing
- [x] 1.5 Apply the uniform post-rules: short-circuit `finished_airing` with a known `TotalEpisodes` to that total, and clamp any computed count to `[0, TotalEpisodes]` when the total is known

## 2. Backend: dashboard wiring

- [x] 2.1 Add a trailing `int? EpisodesAired` parameter to `CurrentSeasonItemDto` in `Services/Dashboard/MainDashboardDto.cs`
- [x] 2.2 Populate it in `MainDashboardService.GetDashboardAsync` for each `currentSeason` entry by calling `scheduleService.EpisodesAiredAsOf(e.Anime, now)`, reusing the existing `now` rather than re-reading the clock
- [x] 2.3 Build the backend and confirm it compiles with no other call sites of `CurrentSeasonItemDto` broken

## 3. Frontend: airing progress bar component

- [x] 3.1 Add `episodesAired: number | null` to `CurrentSeasonItemDto` in `frontend/src/api/types.ts`
- [x] 3.2 Create `frontend/src/components/AiringProgressBar.tsx` taking `aired: number | null`, `watched: number`, and `total: number | null`; compute both fill percentages against `total`, clamped to 0–100, treating a null `aired` or null `total` as a zero-width aired fill
- [x] 3.3 Render the label as `{aired ?? '?'}/{total ?? '?'}`, and give the track a `title` and `aria-label` naming both counts (e.g. "5 of 12 episodes aired, 3 watched")
- [x] 3.4 Render the green watched fill only when `watched > 0`
- [x] 3.5 Create `frontend/src/components/AiringProgressBar.css`: track styled like `.progress-bar__track` (5px, `--code-bg`, rounded, `position: relative`, `overflow: hidden`), aired fill in `--accent`, watched fill absolutely positioned over it at full track height in `--status-watching`

## 4. Frontend: home section

- [x] 4.1 In `CurrentSeasonSection.tsx`, replace the `ProgressBar` import and usage with `AiringProgressBar`, passing `item.episodesAired`, `item.episodesWatched`, and `item.totalEpisodes`
- [x] 4.2 Update the component's leading comment so it describes an airing-progress bar rather than an episode progress bar
- [x] 4.3 Confirm `ProgressBar.tsx`/`.css` are untouched and still used by `MyListPage`, `AnimeDetailPage`, and the currently-watching carousel

## 5. Verification

- [x] 5.1 Build the frontend with Node 22 (`nvm use 22`) and confirm the type check and Vite build pass
- [x] 5.2 Run the app and check the home page: a partially-aired show shows an accent fill short of full width with an `aired/total` label
- [x] 5.3 Check a followed show with watching progress renders green over accent, and one with zero episodes watched renders accent only
- [x] 5.4 Check a show with unknown total renders `aired/?`, and confirm any show with no determinable aired count renders `?/total` with an empty accent fill
- [x] 5.5 Confirm My List, the anime detail page, and the currently-watching carousel still show the unchanged watched/total bar
