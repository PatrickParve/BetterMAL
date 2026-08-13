## Context

`AiringScheduleService.GetWeekAsync` buckets stored `EpisodeAiring` rows into local days, then within each day sorts by the formatted `LocalTime` string and emits one `AiringSlotDto` per row. The frontend (`AiringPage.tsx`) just maps that list straight into `<li>` cards. There is currently no concept of "this row belongs with that row" — every stored episode is its own card.

This change introduces a grouping step between "day-bucketed rows" and "rendered slots": rows for the same anime on the same local day collapse into one ranged card, unless another anime's episode aired strictly between them.

## Goals / Non-Goals

**Goals:**
- Collapse contiguous same-anime, same-local-day episodes into one card with an episode range (`Ep 1-8`).
- Preserve chronological ordering of the day's cards, including the boundary tie-break rule described in the proposal.
- Keep the grouping logic isolated and unit-testable, since it's the most algorithmically involved piece of this change.
- Leave day-bucketing (local-day conversion), week navigation, empty-week handling, and slot-box sizing untouched.

**Non-Goals:**
- No cross-day grouping — grouping never crosses a local-day boundary, regardless of how close two instants are across midnight.
- No change to `AiringTodayList.tsx` (dashboard "Airing today" list) — out of scope, listed as an open question for a possible follow-up.
- No change to what's stored in `EpisodeAiring` or to the `episode-airing-data` capability — this is purely a presentation-layer aggregation of existing rows.
- Never merges episodes across different anime, regardless of timing.

## Decisions

### 1. Grouping happens server-side, in `AiringScheduleService`

The day-bucketing and sort already happen there, and the frontend is currently a pure renderer of `day.slots`. Doing grouping server-side keeps that split intact and avoids shipping the ungrouped per-episode list to the client only to immediately collapse it.

Alternative considered: group client-side in `AiringPage.tsx`. Rejected — it would duplicate the day-bucketing context the backend already computed and add payload/logic to a component that currently has none of this responsibility.

### 2. DTO shape: `EpisodeNumberEnd` alongside the existing `EpisodeNumber`

`AiringSlotDto` gains a nullable `EpisodeNumberEnd`. `EpisodeNumber` remains the range start. When `EpisodeNumberEnd` is null (or equal to `EpisodeNumber`), the slot is a single episode, rendered as today (`Ep {EpisodeNumber}`, or `—` placeholder if `EpisodeNumber` itself is null). When set and greater, the frontend renders `Ep {EpisodeNumber}-{EpisodeNumberEnd}`. `LocalTime` continues to represent a single instant: the group's earliest episode.

Alternative considered: carry an explicit `int[] EpisodeNumbers`. Rejected — the proposal calls for a compact range label, not an enumeration; a start/end pair is enough to render it and is simpler for the frontend to format.

### 3. Grouping algorithm: per-anime interval runs, then a day-level merge

Two phases, run once per local day:

**Phase 1 — per-anime run formation.** For each anime with rows that day, take only its rows with a known episode number, sorted by `AirsAtUtc` (the real instant, not the formatted string — see risk below). Split them into maximal runs such that no *other* anime's row instant falls strictly between two consecutive rows in a run (open interval — an other-anime instant exactly equal to a boundary does not split the run). Rows with an unknown episode number are excluded from this process entirely and always emitted later as their own singleton item; since they belong to the *same* anime, they can never appear in another anime's "other anime" interruption set, so they never block anyone else's grouping either — no special transparency logic is needed beyond that exclusion.

This produces, per anime, one or more runs for the day: a `(minInstant, maxInstant, minEpisode, maxEpisode)` tuple each. An anime with a single episode that day, or whose episodes are all separated by interruptions, ends up with one or more singleton runs, matching today's behavior exactly.

**Phase 2 — day-level ordering.** Collect every run from every anime that day (plus the excluded unknown-episode rows as their own singleton items) into one flat list, keyed by each run's earliest instant. Sort ascending by that key. At an exact tie, a single-instant run (min == max) sorts before a multi-instant run — this one rule produces both halves of the proposal's tie-break requirement:
- A different anime's episode tied with a group's *first* instant shares the group's sort key and, being single-instant, sorts before it — "pushed up."
- A different anime's episode tied with a group's *last* instant has a strictly later sort key than the group (whose key is the *earliest* instant), so it already sorts after the group with no extra rule needed — "pushed down" falls out naturally.
- The degenerate case where a group's first and last instant are the same (a true batch drop, all episodes at one instant) is covered by the same "single-instant sorts before multi-instant" rule.

Remaining ties (two unrelated anime's singleton runs at the same instant, or two different anime's multi-instant runs sharing an identical earliest instant) fall back to whatever order the rows arrived in — unspecified today and unchanged by this design.

**Phase 3 — DTO emission.** Each run becomes one `AiringSlotDto`: `EpisodeNumber` = run's minimum episode number, `EpisodeNumberEnd` = run's maximum (omitted when the run has one row), time fields from the run's earliest row, `Title`/`EnglishTitle`/`PictureUrl` from any member (identical within a run since it's a single anime).

Alternative considered: a single left-to-right pass over one globally time-sorted list, switching runs whenever `AnimeId` changes. Rejected — correctness depends on how same-instant ties between *different* anime happen to be pre-sorted, which is exactly the case the tie-break rule needs to control; resolving membership per-anime first (phase 1) before interleaving (phase 2) avoids that chicken-and-egg problem.

### 4. Sort key switches from formatted `LocalTime` string to `AirsAtUtc` instant

The current code sorts each day's rows with `OrderBy(s => s.LocalTime, StringComparer.Ordinal)`. The grouping algorithm needs a true instant for interval comparisons, so the underlying sort key changes to `AirsAtUtc`. This is strictly more correct than string comparison and doesn't change the effective order for any currently-passing scenario in `airing-schedule`.

## Risks / Trade-offs

- **[Risk]** The interval-based grouping is materially more complex than today's flat per-row projection. → **Mitigation**: isolate it in a small, pure, unit-tested component (e.g. a single function taking a day's rows and returning grouped runs) separate from `GetWeekAsync`'s existing day-bucketing/week-shape logic, with tests covering: simple same-time merge, non-simultaneous-but-contiguous merge, interruption splitting a run, tie-at-first pushes the other anime before, tie-at-last leaves the other anime after, degenerate all-same-instant batch, and unknown-episode rows never merging.
- **[Risk]** Any other consumer of `AiringSlotDto.EpisodeNumber` assuming it always names exactly one episode could be surprised once it can represent a range start. → **Mitigation**: verify at implementation time whether this DTO (or its frontend type) is consumed anywhere besides `AiringPage.tsx`; if so, update those call sites too.
- **[Trade-off]** A merged card still links only to the anime detail page (as every card does today), not to a specific episode within the range — not a regression, since today's single-episode cards have the same limitation.

## Migration Plan

Pure read-path/presentation change over existing stored `EpisodeAiring` rows — no data migration. Backend (DTO + grouping) and frontend (range rendering) ship together in one release, since the new field is meaningless until the frontend interprets it. Rollback is a plain revert of both; no stored-data cleanup required either direction.

## Open Questions

- Should `AiringTodayList.tsx` (dashboard) get the same grouping in a follow-up change? Left out of scope here per the proposal.
- Tie order between two unrelated anime's singleton runs (or two multi-instant runs) sharing an identical instant is left to existing/unspecified fallback order — acceptable since today's behavior is likewise unspecified for that case.
