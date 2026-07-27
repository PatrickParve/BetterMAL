## 1. Backend — aired-episode count on the detail payload

- [x] 1.1 Add an `int? EpisodesAired` parameter to the `AnimeDetailDto` record in `backend/AnimeTracker.Api/Services/Detail/AnimeDetailDto.cs`, placed next to `TotalEpisodes`, and update its XML doc comment if it enumerates fields
- [x] 1.2 Change `AnimeDetailDto.FromEntity` to take the count as a second parameter (`FromEntity(AnimeMetadata anime, int? episodesAired)`) and pass it through to the new field
- [x] 1.3 Inject `IEpisodeScheduleService` into `AnimeDetailService` (`backend/AnimeTracker.Api/Services/Detail/AnimeDetailService.cs`) and call `FromEntity(anime, scheduleService.EpisodesAiredAsOf(anime, DateTimeOffset.UtcNow))`, matching `MainDashboardService`'s use of `DateTimeOffset.UtcNow`
- [x] 1.4 Confirm no other caller of `FromEntity` exists and that `IEpisodeScheduleService` is already registered in `Program.cs` (no DI wiring should be needed)
- [x] 1.5 Build the backend (`sdk:10.0` Docker image per the documented local setup) and confirm it compiles clean

## 2. Frontend — detail page Status shows aired episodes

- [x] 2.1 Add `episodesAired: number | null` to `AnimeDetailDto` in `frontend/src/api/types.ts`, next to `totalEpisodes`
- [x] 2.2 In `frontend/src/pages/AnimeDetailPage.tsx`, extend the status formatting so a `currently_airing` anime with a known aired count renders `Currently airing: <aired>/<total> ep aired`, using `?` when the total is unknown (`null` or `0`), and falling back to the plain label when `episodesAired` is `null`; leave other statuses and the `NO_INFO` fallback untouched
- [x] 2.3 Verify against the spec scenarios: known total, unknown total, no derivable count, and finished/not-yet-aired statuses unchanged

## 3. Frontend — Schedule day headers show the date

- [x] 3.1 In `frontend/src/pages/AiringPage.tsx`, render the day number alongside `day.dayOfWeek` in `.airing-day__header`, parsing it off the `day.localDate` ISO string (string slice, not `new Date(...)`, per the file's existing time-zone caution) with a short comment saying why
- [x] 3.2 Check the header still fits in a seven-column layout and in the ≤800px single-column layout

## 4. Frontend — uniform Schedule slot boxes

- [x] 4.1 In `frontend/src/pages/AiringPage.tsx`, always render `.airing-slot__episode`, showing `Ep —` when `slot.episodeNumber` is `null` instead of omitting the element
- [x] 4.2 In `frontend/src/pages/AiringPage.css`, give `.airing-slot__title` an explicit `line-height` and a two-line `min-height` so a one-line title still reserves two lines, keeping the existing 2-line clamp for the ellipsis on overflow
- [x] 4.3 Confirm every slot box renders at the same height across a week containing short titles, titles longer than two lines, and slots with and without episode numbers

## 5. Frontend — tighter pagination window

- [x] 5.1 Change `WINDOW_SIZE` from `2` to `1` in `frontend/src/components/Pagination.tsx` and update the comment above `pageNumbers` if it describes the window size
- [x] 5.2 Verify Top anime page 6 of 10 renders `1 … 5 6 7 … 10`, edge pages (1, 2, 9, 10) render without a stray ellipsis between adjacent numbers, and the arrows' disabled states are unchanged
- [x] 5.3 Verify the search results page — which shares the component — still paginates correctly with its own page count

## 6. Verification

- [x] 6.1 Build the frontend with nvm's node v22 (`nvm use 22`) and confirm the TypeScript build passes
- [x] 6.2 Run the app and walk all four behaviors: a currently-airing detail page, a finished-airing detail page, the Schedule week, and Top anime pagination
- [x] 6.3 Update `CODE_GUIDE.md` if its description of the anime-detail response or the pagination component is now stale
