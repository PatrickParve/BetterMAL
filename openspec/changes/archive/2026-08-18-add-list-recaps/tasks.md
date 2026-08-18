## 1. Shared score-divergence helper

- [x] 1.1 Extract the normalised divergence math out of `ProfileService.BuildOpinionDivergence` into a `ScoreDivergence` helper in `Services/Profile/`: whole-list mean and population standard deviation on both score scales, the per-entry divergence value, and the minimum-rated-pairs guard (D4).
- [x] 1.2 Rewrite `ProfileService.BuildOpinionDivergence` to call the helper, keeping its 1.0-SD threshold and score-label gates in `ProfileService` where they belong (D4).
- [x] 1.3 Confirm `ProfileServiceOpinionDivergenceTests` still passes unchanged — the profile's output must not move.

## 2. Recap period and entry selection

- [x] 2.1 Add `Services/Recap/RecapPeriod.cs`: the three modes, their parameters, resolution to an inclusive date range, and the years and (year, season) points a period covers. Normalise reversed multi-year bounds to ascending order.
- [x] 2.2 Add `Services/Recap/RecapEntrySelector.cs` implementing both time filters — watch-history (Completed/Dropped with `CompletedAt` inside the period) and aired-in-period (Completed/Dropped/OnHold/Watching with `AiredFrom` inside the period). Plan-to-watch is excluded from both.
- [x] 2.3 Attribute anime to a year and season from `AiredFrom` via `SeasonCalendar.GetSeasonFor`, and exclude entries with no `AiredFrom` from every aired-in-period selection and from both rankings (D5).
- [x] 2.4 Unit-test the selector: a 30 Dec start lands in the earlier year and in a multi-year range ending that year; an anime spanning two years appears in exactly one; a missing `AiredFrom` is excluded under "aired" but included under "watched" when its `CompletedAt` falls in the period; plan-to-watch never appears.

## 3. Recap stats

- [x] 3.1 Add `Services/Recap/RecapStatsBuilder.cs` computing mean score (scored entries only, two decimals, null when none are scored), anime counted, completed count, episodes watched (non-`movie` media types), movies watched (`movie` entries with at least one episode watched, D6), and time spent.
- [x] 3.2 Compute time spent as `EpisodesWatched × (AverageEpisodeDurationSeconds ?? ProfileService.AssumedMinutesPerEpisode × 60)`, referencing the existing constant rather than restating it (D7).
- [x] 3.3 Build hot takes: up to three included entries with both scores, ordered by absolute divergence from the shared helper, each carrying both scores and the divergence direction — no label gates, no SD threshold (D4).
- [x] 3.4 Unit-test the stats: movies stay out of the episode count, unscored entries stay out of the mean, no-scores yields a null mean, the duration fallback matches what `SeriesService` reports for the same anime, and hot takes degrade to two, one, and none as eligible entries run out.

## 4. Bayesian season and year rankings

- [x] 4.1 Add `Services/Recap/RecapRankingBuilder.cs` computing `W = (v/(v+m))·R + (m/(v+m))·C` per season and per year, with `v` as the count of *scored* anime in the group (D3), `R` its mean of my scores, `C` my whole-list mean, and `m` of 5 for a season and 20 for a year.
- [x] 4.2 Omit groups holding no scored anime; order by `W` descending, then scored count descending, then chronologically.
- [x] 4.3 Attach the top-three highest-scored anime (id, title, poster) to the leading season and the leading year only.
- [x] 4.4 Gate the rankings: season ranking on multi-year and yearly recaps under the aired filter only; year ranking on multi-year recaps only; neither on a season recap or under the watch-history filter.
- [x] 4.5 Unit-test: an eight-anime season averaging 8.5 outranks a one-anime season scoring 10 against a 7.4 global mean; a well-covered season sits near its raw mean; the same season scores identically via a yearly and a multi-year recap; a season and a year both holding ten scored anime are weighted differently.

## 5. Recap API

- [x] 5.1 Add `Services/Recap/RecapDto.cs`: period echo, per-filter availability counts, the stat block with hot takes, the full included set as ranked rows (anime id, titles, poster, media type, my score, MAL score, MAL-revealed flag), and the two rankings.
- [x] 5.2 Add `RecapService`/`IRecapService` assembling the DTO from the pieces above, reading entries through `IUserAnimeEntryRepository.GetAllAsync`.
- [x] 5.3 Add `RecapAvailabilityService` returning per-year counts under both time filters and per-season counts across the whole list (D2).
- [x] 5.4 Add `RecapController` with `GET /api/recap` (mode, period parameters, filter) and `GET /api/recap/availability`; validate mode and season names and return 400 on unsupported values, matching `SeasonController`'s handling.
- [x] 5.5 Register the new services in `Program.cs`.
- [x] 5.6 Controller tests: an unknown mode and an unknown season name each return 400; a valid request returns the DTO; a period with no entries under either filter returns a well-formed empty-period response rather than an error.

## 6. Recap page

- [x] 6.1 Add the recap DTO types to `frontend/src/api/types.ts` and a `getRecap`/`getRecapAvailability` pair to `frontend/src/api/client.ts`.
- [x] 6.2 Add `pages/RecapPage.tsx` on route `/recap` in `AppShell`, reading mode, period, filter, basis, and media type from `useSearchParams` and loading through `usePageData` keyed on those params (D8).
- [x] 6.3 Render the period controls and the time-filter toggle, disabling an option whose availability count is zero and falling back to the other option when the selected one becomes unavailable after a period change.
- [x] 6.4 Render the stat block and the hot-takes block, including the "no hot takes for this period" state.
- [x] 6.5 Render the top 10 from the returned set: slice to ten after applying the media-type narrowing and the ranking basis locally, ranking unscored entries below scored ones and breaking ties by title.
- [x] 6.6 Add the ranking-basis control, shown on season recaps and on aired-filter multi-year/yearly recaps only (D12), rendering MAL scores through the existing `ScoreValue` visibility rules.
- [x] 6.7 Add the media-type control, offering "all" plus only the types present in the included set, and leaving the stat block and rankings on the full set (D11).
- [x] 6.8 Add the "see all in my list" control, shown only when the type-narrowed set holds more than ten anime, linking to `/my-list` with the recap scope params (D9).
- [x] 6.9 Render the season ranking and the year ranking, with posters on the leaders only and the year ranking capped at five.
- [x] 6.10 Add `components/YearRankingOverlay.tsx` on the shared `Modal`, listing every qualifying year in rank order, opened from the year ranking when more than five qualify.
- [x] 6.11 Render the empty-period state — a plain statement plus the still-usable period controls — instead of zeroed stats or a blank top 10.
- [x] 6.12 Add `pages/RecapPage.css` following the existing page-CSS conventions.

## 7. My-list entry point and scope

- [x] 7.1 Add `components/RecapPickerOverlay.tsx` on the shared `Modal`: recap type, the settings each type needs, and the time filter for multi-year and yearly, gating unavailable options from `GET /api/recap/availability`.
- [x] 7.2 Add the **Recap a period** button to `MyListPage`'s status-tab row, set apart from the status tabs and outside the filter bar, opening the picker and navigating to `/recap` on confirm.
- [x] 7.3 Give `MyListPage` `useSearchParams` awareness of an incoming recap scope, loading the same `/api/recap` call through `usePageData` and intersecting the returned ids with its rows before the existing status/filter/sort pipeline runs (D9, D10).
- [x] 7.4 Render the dismissible scope indicator naming period, time filter, and media type; dismissing clears the scope params and leaves every other control untouched.
- [x] 7.5 Point the "showing N of M" count at the scoped set when a scope is active.

## 8. Verification and docs

- [x] 8.1 Run the backend test suite and the frontend build; both clean.
- [x] 8.2 Walk the flows against real data: pick a period from my list, switch all three modes, toggle the time filter on a period where one option is unavailable, switch basis and media type, and follow "see all" into my list and back.
- [x] 8.3 Check a season's rank is identical in a yearly and a multi-year recap covering it, and that recap stats agree with the profile page for a period that spans the whole list.
- [x] 8.4 Update `CODE_GUIDE.md`: the two new endpoints in the endpoint table, `Services/Recap/` in the backend section list, and the recap page in the frontend page list.
