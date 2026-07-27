## Why

The three home-page sections each hide information the page already has. Currently-watching cards print a bare `3/12` with no visual sense of progress, "Airing today" rows squeeze a time and a full title onto one cramped line while dropping the episode number the schedule service already resolves, and "Followed shows airing" shows an empty bar for any show whose episode count MAL doesn't publish and silently drops a season's movies and shorts the moment they finish airing — exactly when you'd want to see them sitting there completed.

## What Changes

- **Currently watching**: each card's `watched/total` count gains a progress bar in front of it, reusing the shared watched/total bar (with its inline plus control) instead of the carousel's bespoke count-plus-button row.
- **Airing today**: rows get a larger thumbnail and a two-line layout — `time : Ep N` on top, the anime title beneath, clamped to two lines with an ellipsis. The episode number is newly carried through the dashboard payload; rows for anime whose episode number can't be resolved keep showing just the time.
- **Followed shows airing (unknown episode count)**: when an anime has no known total episode count, the blue aired fill renders at the halfway mark rather than empty, regardless of how many episodes have aired, and the purple watched fill is scaled within that half so being caught up on everything aired fills the whole blue extent.
- **Followed shows airing (membership)**: the section stops being limited to `currently_airing` anime. It also includes my-list anime that premiered in the current season quarter — a season's movies, shorts and finished TV runs stay listed for the rest of the season, with the aired bar simply full. Titles that haven't premiered yet remain excluded.
- **Followed shows airing (finished runs)**: the payload carries whether MAL reports the anime as finished airing, so a finished run with no published episode count renders a full blue bar instead of an empty one — precisely the case the widened membership rule newly puts on screen.
- **Aired-episode count**: the weekly-cadence estimate is bounded by the anime's last air date, so a finished show with no published episode total stops accruing a phantom episode every week.
- **Home page state**: incrementing an episode from the currently-watching carousel also updates that anime's purple fill in "Followed shows airing", so the same show's two bars can't sit on one screen disagreeing until reload.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `main-dashboard`: the currently-watching card gains a progress bar; the "Airing today" row layout and payload gain an episode number and a defined two-line/clamped title; the airing progress bar gains defined behaviour for an unknown total and for a finished run; the followed-shows-airing section's membership rule widens from "currently airing" to "currently airing or premiered this season"; the aired-episode count is bounded by the anime's last air date.

## Impact

- Backend: `MainDashboardDto` (`AiringTodayItemDto` gains `EpisodeNumber`, `CurrentSeasonItemDto` gains `FinishedAiring`), `MainDashboardService` (airing-today projection, current-season membership filter), `EpisodeScheduleService.EstimateAiredFromCadence` (bounded by `AiredTo`).
- Frontend: `CurrentlyWatchingCarousel` (+ its CSS), `AiringTodayList` (+ its CSS), `AiringProgressBar`, `CurrentSeasonSection`, `HomePage`, `api/types.ts`.
- No new repository method and no new service dependency: season membership is derived from `AnimeMetadata.AiredFrom` and the existing `SeasonCalendar`.
- No database schema change, no MAL API change, no change to My List, the detail page, or the weekly airing grid.
