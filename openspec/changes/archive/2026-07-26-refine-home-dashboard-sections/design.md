## Context

All three home sections are already wired end to end: `MainDashboardService` projects `currentlyWatching`, `airingToday`, and `currentSeason` from `IUserAnimeEntryRepository.GetAllAsync` plus `IEpisodeScheduleService`, and `HomePage` renders `CurrentlyWatchingCarousel`, `AiringTodayList`, and `CurrentSeasonSection`.

Three of the four asks are presentation-only. The fourth — keeping current-season titles listed after they finish airing — needs a membership rule, because nothing on `AnimeMetadata` records a season: MAL's `start_season` is persisted only as `SeasonAnimeListing` rows written by the season browser when a season page is fetched, and the anime's own `AiredFrom` date is the only always-present signal.

Widening membership also drags in two latent problems in the aired-episode count, which until now only ever ran against currently-airing shows:
- `EpisodesAiredAsOf` returns the total for a finished run **only when the total is known** (`EpisodeScheduleService.cs:61`). A finished title with no published episode count falls through to the cadence estimate, which needs a `BroadcastTime` a movie doesn't have — so it reports `null` and the bar renders empty. That is exactly the title this change exists to put on screen.
- `EstimateAiredFromCadence` never consults `AiredTo`, so its weekly count climbs forever. Harmless while the section held only airing shows; wrong the moment a finished show with an unknown total is listed.

Two smaller facts shape the rest:
- `IEpisodeScheduleService.ResolveOnLocalDate` already returns `ResolvedEpisode(LocalTime, EpisodeNumber)`; `MainDashboardService` throws the episode number away when building `AiringTodayItemDto`. The "Airing today" episode number is a plumbing change, not new logic.
- `ProgressBar` already renders track + `watched/total` label + optional inline `IncrementButton` — exactly the currently-watching card's new layout. The carousel's bespoke `carousel__progress-row` markup predates that support.

There is no test project in this repo; verification is a build plus a manual pass over the running home page.

## Goals / Non-Goals

**Goals:**
- Currently-watching cards show watched/total progress visually, reusing the shared bar rather than a second implementation.
- "Airing today" rows lead with a larger thumbnail and read `time : Ep N` over a clamped title.
- The airing bar communicates something for shows with no published episode count instead of sitting empty — a half-track while still broadcasting, a full track once finished.
- "Followed shows airing" keeps a season's movies, shorts, and completed runs visible for the whole season.

**Non-Goals:**
- No database schema change, no new repository method, no new MAL/AniList call; membership is derived from data already on `AnimeMetadata`.
- No change to the weekly airing grid, My List, the detail page, or the `ProgressBar` component's own behaviour.
- No proactive background fetch of the current season's listing.
- Not touching the section's sort options (the spec's "my score" option is pre-existing drift from the implementation and stays as-is).

## Decisions

### 1. Current-season membership: airing status OR premiered in the current quarter

Replace `e.Anime.AiringStatus == "currently_airing"` with a union:

```
include(entry) =
    anime.AiringStatus == "currently_airing"
 || (anime.AiredFrom is { } from
     && from <= today
     && SeasonCalendar.GetSeasonFor(from) == SeasonCalendar.GetSeasonFor(today))
```

`today` is the broadcast-local date already computed in `GetDashboardAsync` via `IBroadcastLocalTimeConverter.GetLocalDate(now)`, so "has premiered" and "which season is now" use the same local calendar as "Airing today". `SeasonCalendar` is a static already in the project (`Services/Season/SeasonCalendar.cs`), so this needs no new dependency, no new query, and no change to `MainDashboardService`'s constructor.

`AiredFrom == null` means the anime can never satisfy the "premiered" branch; such an entry appears only while MAL reports it as currently airing, which matches today's behaviour.

**Rejected: also consulting `SeasonAnimeListing` rows.** MAL's `start_season` is authoritative where it disagrees with the quarter `AiredFrom` falls in (a late-March premiere MAL files as spring), and `SeasonRepository` already documents that. Consulting the listing would have meant a new `ISeasonRepository.GetAnimeIdsInSeasonAsync`, a fourth constructor dependency on `MainDashboardService`, and a query per dashboard load. Because the rule is an `OR`, that branch can only ever *add* members, and the set it adds is narrow: titles that have premiered, are no longer `currently_airing`, are filed by MAL under the current season, **and** whose start date falls in a different calendar quarter — i.e. finished boundary premieres. Worse, `SeasonAnimeListing` rows exist only for seasons the user has actually browsed, so that branch would make home-page membership depend on unrelated navigation, silently and untestably. The quarter rule is deterministic and self-contained; if a boundary premiere is ever actually missed, the listing branch can be added then.

**Rejected: `StartSeasonYear`/`StartSeasonName` columns on `AnimeMetadata`.** A migration plus a re-sync of every cached anime, for a home-page filter `AiredFrom` already answers.

Over-inclusion is bounded and benign: a June-premiering show MAL files under summer appears in the spring section too — but during spring it genuinely is airing, so listing it is right anyway. Under-inclusion is likewise mostly covered: a show MAL files one season ahead of its start date is still `currently_airing`, so branch one keeps it; only once it has finished does the quarter rule drop it early.

### 2. `AiringTodayItemDto` gains `EpisodeNumber`, mirroring `AiringSlotDto`

`AiringSlotDto` (weekly grid) already carries `int? EpisodeNumber` and the frontend already renders `Ep {n}` from it. Adding the same nullable field to `AiringTodayItemDto` keeps the two schedule-driven payloads shaped alike; `MainDashboardService` just stops discarding `x.Episode!.EpisodeNumber`. Null renders as the time alone — no placeholder, matching how the grid handles an unresolvable number.

### 3. `CurrentSeasonItemDto` gains `FinishedAiring`, so a finished run reads as fully aired

The widened membership rule puts finished titles on screen, and for those the aired count alone cannot express "fully broadcast": `EpisodesAiredAsOf` reports `null` for a finished title with no published total and no broadcast cadence (a movie), which would render as an *empty* bar — the opposite of the truth.

Rather than have the count fabricate a number, the payload carries the fact directly: `bool FinishedAiring`, fed from `e.Anime.AiringStatus == "finished_airing"` — the same string `EpisodesAiredAsOf` already keys on. The bar then treats "finished" as a fill state of its own (decision 4).

This only changes rendering when the total is unknown. When the total *is* known, `EpisodesAiredAsOf` already short-circuits a finished run to the total, so the bar was full via the ordinary proportional rule and stays that way.

Alternative rejected: inferring "finished" on the frontend from `aired === total`. That can't distinguish a finished run from a caught-up one, and says nothing at all when either count is null — which is the only case that needed solving.

### 4. Airing-bar fill table: half-track for an unknown total, full track once finished

In `AiringProgressBar`, an unknown `total` currently drives both fills to 0% because `pct` returns 0 without a total. New rules, in order:

| total | finished | aired | blue fill | purple fill | label |
|---|---|---|---|---|---|
| known | — | known | `aired/total` | `watched/total` | `aired/total` |
| known | — | unknown | empty | `watched/total` | `?/total` |
| unknown | **yes** | any | **100%** | `watched / max(aired ?? 0, watched) × 100%` | `aired?/?` |
| unknown | no | known | **50%** | `watched / max(aired, watched) × 50%` | `aired/?` |
| unknown | no | unknown | empty | none | `?/?` |

The half-track is a fixed "this far, end unknown" marker — the point is that no honest proportion exists, so it must not scale with the aired count. The full track for a finished run is the same idea at the other end: the end is now known to have been reached, even when nobody published how many episodes that took. A finished movie you have watched therefore reads as a full purple bar labelled `?/?` — the bar states "fully broadcast, fully watched", the label states honestly that neither count is published.

Measuring purple against `max(aired, watched)` inside the blue extent keeps "caught up on everything aired" reading as a full blue extent while guaranteeing purple can never overshoot blue, even if the aired estimate lags what the user has already watched. Both denominators are safe: purple only renders when `watched > 0`, so `max(…, watched) > 0`.

The 50%/100% constants live in `AiringProgressBar.tsx` next to `pct`; the component stays presentational.

Alternative rejected: leaving purple at zero for unknown totals — a show you are fully caught up on would then render as bare blue, losing the exact signal the bar exists for.

### 5. Bound the weekly aired-count estimate by `AiredTo`

`EstimateAiredFromCadence` counts one episode per 7 days since the premiere with no upper bound, so a show with no published `TotalEpisodes` keeps accruing episodes long after it ended (the `Math.Clamp` at `EpisodeScheduleService.cs:71` only applies when a total is known). Clamp the reference date to the show's last local air date:

```
todayLocalDate = converter.GetLocalDate(nowUtc)
nowLocalDate   = anime.AiredTo is { } to
                     ? Min(todayLocalDate, converter.LocalDateOfJstBroadcast(to, broadcastTime))
                     : todayLocalDate
```

The existing "today's episode hasn't aired yet" adjustment must then run only when the date was *not* clamped (`nowLocalDate == todayLocalDate`); otherwise it compares the current wall-clock time against a broadcast slot on a date in the past.

Deliberately **not** reused here: the sibling helper `EstimateLastLocalDate`. Its fallback chain invents a `MinimumEpisodeEstimate` (12-episode) ceiling when there is no `AiredTo` and no total, which is exactly the kind of fabricated number this bar is supposed to avoid. Bound by `AiredTo` only; when `AiredTo` is absent but MAL says the run is over, decision 3's `FinishedAiring` flag carries the meaning instead.

### 6. Currently-watching cards switch to `ProgressBar`

Replace the `carousel__progress-row` span (count + `IncrementButton`) with `<ProgressBar watched total onIncrement incrementPending incrementLabel />`. `ProgressBar` already computes `atMax` (`total !== null && watched >= total`) internally, so the carousel's local `atMax` and its `disabled={pendingId === item.animeId || atMax}` collapse into `incrementPending={pendingId === item.animeId}`.

`ProgressBar` renders inside `AnimeCard`'s `children`, i.e. inside the card's `<Link>` — same as today's row, and not the `actions` slot, which is absolutely positioned as a corner badge. `IncrementButton` already calls `preventDefault()`/`stopPropagation()`, so the plus keeps incrementing without navigating. `.carousel__progress-row` / `.carousel__progress` CSS is then dead and gets removed; `.progress-bar__label` is already 12px with a 6px gap, matching the row it replaces, and the card is 160px wide with `.progress-bar__track` at `flex: 1`, so the bar takes whatever the label and button leave.

### 7. "Airing today" row: CSS-driven two-line clamp, not a character cap

Row becomes thumbnail + a column of two lines. The title clamps with the `-webkit-line-clamp: 2` pattern already used by `.anime-card__title`, which the browser ellipsises at the real rendered width — no magic character count to retune when the sidebar (`flex: 0 0 320px`) or font changes.

The clamp is preferred over a character cap specifically because the ellipsis must appear *only* when the title is genuinely too long: a cap truncates by counting, so it either cuts short titles that would have fitted or lets long ones through, depending on the glyphs. `.anime-card__title`'s `min-height: 2.6em` is deliberately not carried over — it reserves a second line whether or not one is used, which here would pad every short row for nothing. Row height is set by the thumbnail (48×66) rather than the text, so rows stay uniform whether a title runs to one line or two.

### 8. `HomePage` keeps both sections' watched counts in sync

`handleEpisodesWatchedChange` currently patches only `currentlyWatching`. A show can appear in both sections, and once the carousel card has a visible bar the two would sit on one screen showing different fills until a reload. The handler patches `currentSeason` by the same `animeId` — `CurrentSeasonItemDto` already carries `episodesWatched`, so it is one added `map` and no payload change.

Left alone: `episodesAired` and section membership, which only the server can recompute. `onCompleted` still reloads the whole dashboard, which is what moves a finished show between sections.

## Risks / Trade-offs

- **A finished boundary premiere is dropped a few weeks early** → a title MAL files under this season whose start date fell in the previous quarter leaves the section once it stops being `currently_airing`. Accepted in exchange for deleting the season-listing lookup (decision 1); the alternative's own coverage depended on the user having browsed that season page.
- **Long-running shows (e.g. a 1000+ episode series with no published total) render a half bar** → arguably flattering, but it is exactly the "unknown end" signal the user asked for, and the `aired/?` label still states the true aired count.
- **A finished title with no published counts renders a full bar labelled `?/?`** → the bar and the label are answering different questions (extent vs. count). Judged clearer than an empty bar, which reads as "nothing has aired".
- **Section grows over a season** → a busy season's completed movies and shorts accumulate in the grid. Acceptable: it is bounded by my-list titles in one season, and the existing sort control still orders them.
- **`EstimateAiredFromCadence` now depends on `AiredTo` accuracy** → a stale or missing `AiredTo` on a finished show leaves the old unbounded behaviour, which decision 3's flag then covers visually.
- **`ProgressBar` gains a fourth call site** → any future change to it now also lands on the home carousel. This is the intent (one bar everywhere); the aired bar stays separate in `AiringProgressBar`.
- **No test project** → regressions must be caught by build + manual check of the cases listed in tasks 3.4, 4.4, 5.6 and 6.2.

## Migration Plan

Pure code change: no migration, no data backfill, no config. Backend and frontend deploy together; the frontend tolerates a missing `episodeNumber` as "unknown" and a missing `finishedAiring` as `false`, so a stale backend degrades to today's time-only line and today's empty bar rather than breaking. Rollback is a straight revert of the change.

## Open Questions

None — the three forks (section membership scope, purple-fill behaviour under an unknown total, two-line title clamp) were resolved with the user before this document, and the follow-up review fork (drop the season-listing lookup; carry `FinishedAiring` rather than change the spec's promise) was resolved after it.
