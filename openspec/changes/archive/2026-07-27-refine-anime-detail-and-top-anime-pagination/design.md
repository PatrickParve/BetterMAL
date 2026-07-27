## Context

Three independent UI refinements, one of which reaches into the backend.

- **Detail Status.** `AnimeDetailPage.tsx` renders `formatAiringStatus(detail.airingStatus)` into the info box, mapping `currently_airing` → `"Currently airing"`. The aired-episode count it now needs already exists server-side: `EpisodeScheduleService.EpisodesAiredAsOf(anime, nowUtc)` (`Services/Airing/`), which prefers the cached AniList per-episode schedule and otherwise estimates from the weekly broadcast cadence. `MainDashboardService` already calls it for the home page's current-season cards; `AnimeDetailDto` simply doesn't carry the value.
- **Schedule page.** `AiringPage.tsx` renders `day.dayOfWeek` in the column header while `day.localDate` (an ISO date string) sits unused in the same object, and slot boxes size themselves to their content — one- vs two-line titles and present/absent `Ep N` badges make neighbouring boxes different heights.
- **Pagination.** `components/Pagination.tsx` builds its page list from `WINDOW_SIZE = 2`, so page 6 of 10 renders `1 … 4 5 6 7 8 … 10`. The component is shared by Top anime and the search results page.

Constraint from the codebase: the backend has no test project, so verification is manual (Docker build + browsing the pages). Local builds follow the documented setup — nvm's node v22 for Vite, the `sdk:10.0` Docker image for the .NET 10 backend.

## Goals / Non-Goals

**Goals:**

- Surface aired-episode progress on the detail page's Status row for currently-airing shows, reusing the existing server-side computation rather than re-deriving it.
- Put the day of the month in each Schedule column header without a backend round-trip.
- Make every Schedule slot box identical in height, with two reserved title lines and a reserved episode row.
- Tighten the page-number window to first, last, and current ±1.

**Non-Goals:**

- No change to how aired-episode counts are computed (`EpisodesAiredAsOf` is used as-is, including its AniList-cache-then-cadence fallback).
- No per-page pagination configurability — one window size app-wide.
- No change to the airing week's backend DTO, the schedule refresh job, or the AniList integration.
- No change to the progress bar, which already communicates watched-vs-total separately from Status.

## Decisions

### 1. Add `EpisodesAired` to `AnimeDetailDto`, computed in `AnimeDetailService`

`AnimeDetailService` gains an `IEpisodeScheduleService` dependency and passes `scheduleService.EpisodesAiredAsOf(anime, DateTimeOffset.UtcNow)` into the DTO. Because `AnimeDetailDto.FromEntity(anime)` is a static factory over the entity alone and the count depends on the current instant plus the schedule cache, the count is passed in as a second parameter: `FromEntity(anime, episodesAired)`.

*Alternatives considered.* Computing the count in the frontend from `airedFrom` + total episodes: rejected — the frontend has neither the broadcast time/day nor the AniList per-episode schedule, so it would produce a second, worse answer that disagrees with the home page. Making `EpisodesAired` a computed property on `AnimeMetadata`: rejected — the entity has no access to the schedule cache or a clock. Injecting the schedule service into the DTO factory: rejected — DTOs stay data-only in this codebase.

`DateTimeOffset.UtcNow` is read directly in the service, matching `MainDashboardService`; there is no clock abstraction in the project to follow.

### 2. Format the Status string in the frontend, not the backend

The backend returns raw `airingStatus` plus a nullable `episodesAired`; `AnimeDetailPage.tsx` composes `"Currently airing: 5/12 ep aired"`. This keeps display strings in the layer that already owns `AIRING_STATUS_LABELS` and `NO_INFO`, and keeps the DTO reusable.

Formatting rules, applied only when `airingStatus === 'currently_airing'`:

| `episodesAired` | `totalEpisodes` | Status text |
|---|---|---|
| `5` | `12` | `Currently airing: 5/12 ep aired` |
| `5` | `null` or `0` | `Currently airing: 5/? ep aired` |
| `null` | any | `Currently airing` |

`?` for an unknown total matches the existing convention in the progress bar (`watched/?`) and `AiringProgressBar`'s `${aired ?? '?'}/${total ?? '?'}` label. MAL reports an unpublished total as `0`, which `MalMappingExtensions` already normalizes to `null` on the way in; treating a `0` that slips through as unknown too is cheap insurance against printing `5/0`.

### 3. Derive the header day number client-side from `localDate`

`day.localDate` is a `YYYY-MM-DD` string already grouped into local days by the backend. The header renders `` `${day.dayOfWeek} ${dayNumber}` ``, where the day number is parsed off the ISO string directly (`Number(localDate.slice(8, 10))`) rather than via `new Date(localDate)` — the existing file already documents that constructing dates from bare ISO strings and formatting them risks a UTC/local off-by-one day (see `toLocalIso`'s comment and `formatWeekRange`'s use of pre-computed boundaries). Slicing the string sidesteps time zones entirely.

*Alternative considered.* Adding a `dayOfMonth` field to `AiringDayDto`: rejected — pure duplication of data the client already holds.

### 4. Uniform slot boxes via reserved rows in CSS

Height is equalized by making the two variable parts fixed:

- `.airing-slot__title` keeps its 2-line `-webkit-line-clamp` (which already supplies the ellipsis) and gains a fixed two-line height (`min-height` derived from `line-height`), so a one-line title still occupies two lines' worth of space.
- The episode row is always rendered. `AiringPage.tsx` currently renders `.airing-slot__episode` only when `slot.episodeNumber !== null`; it will always render the badge, showing `Ep —` when the number is unknown. Keeping the same element in both cases (rather than an invisible spacer) means the boxes match without a second set of layout rules, and screen readers get a meaningful "unknown" rather than a blank.

The thumbnail (52×72) is already fixed-size and is the tallest element in the body row, so with the title block fixed the whole box lands on one height.

*Alternative considered.* A fixed `height` on `.airing-slot`: rejected — it would clip content if the font size, thumbnail size, or badge padding ever changes; deriving the height from the reserved rows keeps it self-adjusting.

### 5. Change the shared `WINDOW_SIZE` from 2 to 1

`pageNumbers()`'s existing algorithm already produces the requested shape — first/last always shown, everything within `WINDOW_SIZE` of the current page shown, gaps collapsed into a single ellipsis. Only the constant changes: page 6 of 10 becomes `1 … 5 6 7 … 10`.

*Alternative considered.* A `windowSize` prop so Top anime narrows while search keeps the wider window: rejected — nothing in the search requirements pins the wider window, and one pagination behavior app-wide is less to reason about. The prop can be added later if a page genuinely needs a different window.

Edge behavior is unchanged and needs no special-casing: near an edge (page 2 of 10 → `1 2 3 … 10`) the first/last pages are adjacent to the window, so no ellipsis is emitted between them; with `totalPages <= 1` the component still renders nothing.

## Risks / Trade-offs

- **The aired count can be absent or approximate for anime outside my list** → The AniList per-episode cache is populated only for my-list entries (`EpisodeScheduleRefreshService` iterates `entryRepository.GetAllAsync`), so a browsed-but-not-added anime falls back to the weekly-cadence estimate, and to `null` when it has no `AiredFrom` + `BroadcastTime`. Mitigated by design: `null` degrades to today's plain `Currently airing`, so the page is never worse than it is now, and the estimate is the same one the home page already shows.
- **The estimate can disagree with reality for irregular schedules** (split cours, break weeks) → Accepted: this is the same number the home dashboard already displays, so the two pages stay consistent with each other; fixing cadence estimation is out of scope.
- **Narrowing the pagination window also narrows the search page** → Intended (decision 5) and called out in the proposal; the search spec never pinned a window size, so no requirement regresses.
- **Reserving two title lines makes short-title columns slightly taller** → Accepted; the uniformity is the point of the request, and the extra space is one line of 13px text.
- **`FromEntity`'s signature changes** → Compile-time break only, caught by the build; it has exactly one caller (`AnimeDetailService`).

## Migration Plan

No data migration, no schema change, no API removal. `episodesAired` is an added nullable field on an existing response, so a stale frontend against a new backend simply ignores it, and a new frontend against a stale backend reads `undefined` and falls back to the plain `Currently airing` label. Rollback is a straight revert of the commit.

## Open Questions

None. The one ambiguity in the request (the truncated Schedule bullet) was resolved with the user: reserve both the two-line title and the episode row so every box matches.
