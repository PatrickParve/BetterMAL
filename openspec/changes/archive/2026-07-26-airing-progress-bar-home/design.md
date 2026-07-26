## Context

The home page's "Followed shows airing" section (`CurrentSeasonSection.tsx`) lists every my-list entry whose anime has `AiringStatus == "currently_airing"`, regardless of watch status, and renders the shared `ProgressBar` with `watched/total`. That component is also used by `MyListPage`, `AnimeDetailPage`, and `CurrentlyWatchingCarousel` (via its own markup), so it cannot be repurposed.

Nothing currently computes "episodes aired so far". The building blocks exist though: `EpisodeScheduleService` already answers two adjacent questions — "does an episode air on this local date?" (`ResolveOnLocalDate`) and "when does the next one air?" (`NextAiringInstant`) — over an AniList-backed per-episode cache (`IEpisodeScheduleCache`) with a bounded weekly-cadence fallback. `IBroadcastLocalTimeConverter` handles all JST→local conversion, including the DST edge cases the estimator already rounds around.

Constraint worth naming: the aired count is only as good as the schedule data. Some entries have no `AiredFrom` or no `BroadcastTime` and nothing cached — for those, the honest answer is "unknown", not zero.

## Goals / Non-Goals

**Goals:**
- Home cards communicate broadcast progress (aired/total) at a glance, with personal watch progress readable in the same track.
- The aired count comes from the existing schedule source of truth, so it never contradicts "Airing today" or the next-episode countdown.
- Unknown stays unknown — no fabricated aired counts.
- Zero behavioural change to any other view.

**Non-Goals:**
- Changing the shared `ProgressBar` component or any of its other call sites.
- Adding increment controls to the home section (it has none today; that stays).
- Persisting aired counts, adding a migration, or introducing new network calls on the dashboard path.
- Applying aired-progress semantics to My List, detail, the season browser, or the airing schedule page.

## Decisions

### Compute the aired count on the backend, not the frontend

The frontend has no air dates — `CurrentSeasonItemDto` carries only watched/total/score/rank. Shipping raw schedules to the client to compute a single integer would mean a much larger payload and a duplicate implementation of the JST/DST/break-week logic. So the backend computes it and sends `episodesAired`.

*Alternative considered:* have the client derive it from `AiredFrom` plus a weekly cadence. Rejected — it silently disagrees with the AniList-cached truth the backend already has (break weeks, irregular schedules), and re-implements timezone logic in a second language.

### Extend `IEpisodeScheduleService` with `EpisodesAiredAsOf(anime, nowUtc)`

The method returns `int?` — `null` meaning "not determinable". It belongs on this interface because it answers the same class of question from the same two data sources, and reuses the cache/fallback split already established there.

Resolution order inside the implementation:

1. **Cached AniList schedule** (non-empty): take the highest `Episode` number among entries whose `AirsAtUtc <= nowUtc`; zero such entries means 0 aired. Using the max episode number rather than a count is deliberate — it stays correct if the cached list ever starts partway into a run.
2. **Weekly-cadence estimate**, when there is no usable cache but `AiredFrom` and `BroadcastTime` are both known: convert the premiere to a local date via `LocalDateOfJstBroadcast`, count whole weekly slots elapsed up to the local "now", and subtract the current week's slot if its local broadcast time has not passed yet. Before the premiere → 0.
3. **Neither** → `null`.

Then, uniformly: clamp to `[0, TotalEpisodes]` when the total is known, and short-circuit a `finished_airing` anime with a known total to that total (its run is over by definition, and the estimate can drift on a long-finished show).

*Alternative considered:* loop `ResolveOnLocalDate` backwards over every date since the premiere and count hits. Rejected — O(weeks) per anime per dashboard load for no extra accuracy over reading the cache directly.

*Alternative considered:* a standalone `IAiredEpisodeCountService`. Rejected — it would need the same cache and converter dependencies and would risk drifting out of sync with the scheduling rules it duplicates.

### Nullable `EpisodesAired` on the DTO, rendered as `?`

`CurrentSeasonItemDto` gains `int? EpisodesAired` as a new trailing positional parameter — additive, so no API break for any other consumer. The frontend types it `episodesAired: number | null`. When it is `null` the label renders `?/total` and the aired fill is zero-width, while the green watched fill still draws — the user's own progress is known even when the broadcast timeline is not.

*Alternative considered:* fall back to the old watched/total bar when aired is unknown. Rejected — two different bar semantics in one grid, distinguishable only by squinting at the label, is worse than one honest `?`.

### A separate `AiringProgressBar` component, home-only

New `components/AiringProgressBar.tsx` + `.css`, imported only by `CurrentSeasonSection.tsx`. `ProgressBar` is left completely alone rather than growing optional `aired`/`showAired` props, which would put a home-only concern into a component three other views depend on and make it easy to leak the new look elsewhere.

### Two fills layered in one track

The two fills share a single track: the aired fill (`--status-completed`, blue) is the base layer; the watched fill (`--accent`, the site's purple, already defined for both light and dark themes) is absolutely positioned over it, drawn only when `episodesWatched > 0`. Both widths are percentages of the same total-episode denominator, both clamped to 100%, so they are directly comparable and the watched fill can never visually overrun the track. When caught up, the watched fill exactly covers the aired fill — which reads correctly as "watched everything that has aired".

*Alternative considered:* two stacked bars. Rejected — doubles the vertical footprint in a dense card grid and makes the "how much have I got left to catch up on" comparison require eye travel instead of being a single segment.

*Note on the watched-fill condition:* "if I am watching it" is implemented as `episodesWatched > 0` rather than `status == Watching`, since this section includes entries of every status and progress on a paused or on-hold show is still real progress worth seeing.

### Label and accessibility

The visible label is `aired/total` (`?` for either unknown side). Because the numeric watched count is no longer in the label, the track carries a `title` and `aria-label` conveying both — e.g. "5 of 12 episodes aired, 3 watched" — so the information is not lost for screen readers or on hover.

## Risks / Trade-offs

- **The weekly estimate can be off for irregular schedules** (recaps, sudden breaks) when AniList data has not been cached for that show → mitigated by preferring the cache whenever present and by clamping to the known total; the same estimator already backs the airing grid, so any error is consistent with the rest of the app rather than a new inconsistency.
- **The watched count is no longer shown as a number on home cards** → mitigated by the `title`/`aria-label` text; the green fill carries the at-a-glance signal, and the exact number remains one click away on the detail page.
- **Watched can exceed aired** if a user tracks ahead of the broadcast or the estimate lags → both fills clamp to the track, so the worst case is green covering the whole bar rather than overflowing layout.
- **Aired count is computed per request** with no caching → negligible: it is an in-memory lookup or a handful of arithmetic operations per entry, on a list already bounded by the size of the user's list.
- **The watched fill reuses `--accent`**, the same purple used for links and highlights site-wide, and the aired fill reuses `--status-completed` blue, already used for the "Completed" status badge elsewhere → acceptable: both are established site colours, so a future palette change to either moves consistently with its other use. Trade-off: the aired fill now shares a hue with the unrelated "Completed" watch-status badge, though the two never appear in the same view.

## Migration Plan

Additive and stateless — no schema change, no data backfill, no rollout ordering constraint. A frontend built before the backend change simply sees `episodesAired` as `undefined` and renders the `?` state; a backend deployed ahead of the frontend sends a field nobody reads. Rollback is reverting the commits.

## Open Questions

None blocking. Two judgement calls made explicitly above rather than left open: green keys off `episodesWatched > 0` (not watch status), and the fills are layered in one track (not stacked).
