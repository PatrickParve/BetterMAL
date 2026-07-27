## Why

Three page-level rough edges make the app harder to read at a glance: the detail page's Status field says "Currently airing" without saying how far into the run a show is (the home page already shows this, so the data exists but the detail page doesn't use it); the Schedule page's day headers name the weekday but not the date, and its slot boxes jitter in height depending on title length and whether an episode number is known; and the Top anime pagination shows so many page numbers (`1 … 4 5 6 7 8 … 10`) that the current page is hard to pick out.

## What Changes

- **Anime detail — aired episode count in Status.** When an anime's airing status is `currently_airing`, the info box's Status row reads `Currently airing: x/y ep aired` instead of just `Currently airing`. This requires the aired-episode count on the detail payload, which the backend already computes for the home dashboard (`IEpisodeScheduleService.EpisodesAiredAsOf`) but does not yet include in `AnimeDetailDto`. Other airing statuses are unchanged.
- **Schedule — day-of-month in the column header.** Each day-column header shows the day number next to the weekday name (e.g. `Monday 27`), derived from the day's existing local date. No backend change.
- **Schedule — uniform slot boxes.** Every slot box is the same height regardless of content: the title area always reserves two lines (clamped with an ellipsis when longer), and the episode-number row is always reserved, showing a placeholder when the episode number is unknown.
- **Top anime — tighter pagination window.** Page-number controls show the first page, the last page, and the current page with exactly one neighbour on each side (on page 6 of 10: `1 … 5 6 7 … 10`), replacing today's two-neighbour window. The control is shared with the search results page, so search paginates the same way.

## Capabilities

### New Capabilities

None — all three areas are refinements of existing behavior.

### Modified Capabilities

- `anime-detail`: the info box's Status field gains an aired-episode count while a show is currently airing.
- `airing-schedule`: day-column headers include the day of the month, and slot boxes are fixed-height with a reserved two-line title and a reserved episode row.
- `library-views`: the Top anime page's page-number controls use a one-page window around the current page plus first/last.

## Impact

- Backend: `Services/Detail/AnimeDetailDto.cs` (new `EpisodesAired` field on the record and its `FromEntity` factory), `Services/Detail/AnimeDetailService.cs` (inject `IEpisodeScheduleService` to supply the count). No new endpoint, no schema or migration change.
- Frontend: `api/types.ts` (`AnimeDetailDto.episodesAired`), `pages/AnimeDetailPage.tsx` (Status formatting), `pages/AiringPage.tsx` + `pages/AiringPage.css` (day header, uniform boxes), `components/Pagination.tsx` (window size).
- Shared-component ripple: the pagination window is defined once in `components/Pagination.tsx`, so the search results page inherits the tighter window. This is intentional (one pagination behavior app-wide); no search requirement pins the old window.
- No API contract break for existing consumers — `episodesAired` is an added, nullable field.
