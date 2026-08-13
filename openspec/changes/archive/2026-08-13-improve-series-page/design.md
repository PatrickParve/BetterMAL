## Context

The series page landed in `add-series-page` (archived 2026-08-12) and works, but three things surfaced in use:

1. `SeriesGraphBuilder.MemberCap = 60` truncates One Piece — entries the user has watched are simply absent, and the page only says "too large to show in full".
2. The page is thinner than its siblings: no external links, no broadcast progress, no signal that *I* have finished the series (only that MAL has).
3. The score stats read wrong. "Highest MAL score" and "My favourite" pick one arbitrary winner out of a tie; the MAL averages auto-reveal mid-run; and a series with no extras shows "everything" averages that are byte-identical to its main-series ones.

Constraints that shape the design:

- The read and rebuild endpoints are synchronous. Live MAL fetches go through `MalRequestPacer` at **1 request/second**, so a fetch budget is directly a wall-clock budget.
- Fetch budgets today: `VisitFetchBudget = 8`, `RebuildFetchBudget = 20`. Only members with **no cached row at all** (plus lean members with no relations) cost a fetch; cached members are free.
- Series-page averages are never stored — they are recomputed on every read (`add-series-page` decision 5), so anything derived from scores is a projection concern, not a persistence one.
- User display preferences live in `localStorage` (`ScoreVisibilityContext`, `ContentFilterContext`); user *data* lives in Postgres.

## Goals / Non-Goals

**Goals:**

- No real franchise is truncated, and filling in an incomplete one is a single user action rather than eight.
- The series page carries the same broadcast-progress and external-link affordances the home and detail pages already have, plus a "I have finished this" signal.
- Score stats tell the truth about ties, and the hide-scores reveal rules match how the user actually reads a part-aired franchise.

**Non-Goals:**

- No background job or queue for series building. Everything stays request-driven.
- No change to how a series is *composed* (traversal set, main-line classification, watch order) — only how large it may get.
- No change to the global hide-scores toggle or the `alwaysShowCompletedScores` setting themselves; only how the series page opts individual values into them.
- No drag-and-drop infrastructure.

## Decisions

### 1. Member cap raised to 400, not removed

`MemberCap` 60 → 400. Traversal costs two indexed queries per member plus one `Include` chain at projection time; 400 members is well within the endpoint's budget on local Postgres, and far past any real franchise reachable through the traversal set (One Piece's is ~60–80 entries).

A finite cap is kept rather than removed. `IsTruncated` is the only disclosure the page has that a component ran away — through `spin_off`/`alternative_version` a pathological merge is possible in principle — and with the cap removed a synchronous endpoint would issue an unbounded number of queries with nothing to say about it afterwards. 400 is high enough that hitting it means something is genuinely wrong, which is exactly what the truncation notice is for.

Rejected: removing the cap entirely (no ceiling on a synchronous request, and the truncation notice becomes dead code).

### 2. Rebuild becomes a client-driven loop, server budget unchanged

The server keeps its 20-fetch rebuild budget. `SeriesPage.handleRebuild` instead issues **rounds**: call `rebuildSeries`, and if the returned series is still `isPartial` **and** its member count grew since the previous round, go again. Stop on any of: not partial, no growth, an error, 12 rounds, or the page unmounting. While it runs the button reads `Rebuilding… N entries` so each round's progress is visible.

This gets One Piece complete in one click without a minute-long blocking request. Each round is still single-flighted server-side by `RefreshGate`, so a double-click cannot run two loops against the same series.

The no-growth stop condition is load-bearing: a member that keeps failing to fetch (network, MAL 5xx) sets `IsPartial` on every build forever, so "loop while partial" alone would spin. A member genuinely removed from MAL throws `AnimeMetadataNotFoundException` and is skipped *without* marking partial, so it doesn't trigger the loop at all.

Rejected: raising `RebuildFetchBudget` to 60 (a ~60s request with no feedback, exposed to proxy and browser timeouts); a background build service (new infrastructure for a single-user local app, and the page would still have to poll).

### 3. Existing truncated series are not force-rebuilt

`NeedsBuild` is left alone — it does not gain an `IsTruncated` check. A series that is genuinely over the new cap would then re-traverse and spend 8 fetches on *every single visit*, forever. Stored series built under the old cap pick up the new one when the user clicks Rebuild (which the truncation notice already sits next to) or when the 30-day staleness check fires.

### 4. Aired-episode aggregation for the blue bar

New stat `MainLineAiredEpisodes`, summed over exactly the main-line members that have a **known** `TotalEpisodes` — the same set `EpisodesAndRuntime` already counts, so aired can never exceed the total the page displays:

| member airing status | contributes |
| --- | --- |
| `finished_airing` | `TotalEpisodes` |
| `currently_airing` | `min(EpisodesAiredAsOfAsync(a, now) ?? 0, TotalEpisodes)` |
| `not_yet_aired` / unknown | 0 |

Only currently-airing members hit `IEpisodeScheduleService` — typically zero or one per series — so this adds at most a query or two to the projection.

A currently-airing member with an *unknown* total contributes to neither side, and the existing `HasUnknownEpisodeCounts` "+" marker already discloses that. Counting its aired episodes while leaving its total out was rejected: aired would exceed the total and the bar would sit at 100% mid-run.

### 5. The blue bar shows only while the series is ongoing

**My progress** renders `AiringProgressBar` (blue broadcast fill, purple watched fill layered on top) when any member is currently airing — i.e. `status === 'Ongoing'` — and the existing `ProgressBar` otherwise. Outside an ongoing series aired equals total, so the blue fill would be a permanently full track carrying no information. The component is reused unchanged: `aired={stats.mainLineAiredEpisodes}`, `watched={stats.myWatchedEpisodes}`, `total={mainLineEpisodeTotal > 0 ? mainLineEpisodeTotal : null}`, `finished={false}`.

### 6. External links point at the series root

The root (`series.rootAnimeId`, main-line entry 1) is what both MAL and SeriesGraph index a franchise under, so all three links target it:

- MAL — `myanimelist.net/anime/{rootAnimeId}`, no extra data needed.
- AniList — needs `SeriesDto.RootAniListId`, read from `AnimeAiringSyncs` for the root exactly as `AnimeDetailService` does, falling back to AniList's title search when null. Same fallback the detail page already has.
- SeriesGraph — title search on the root's display title, same as the detail page.

### 7. Personal completion is computed server-side

New DTO flag `MainLineCompletedByMe`: every main-line member whose `AiringStatus` is `finished_airing` has `UserEntry.Status == Completed`, and at least one such member exists. This mirrors the frontend's existing `isGroupCompleted` convention (entries not yet out can't count against you), but it moves server-side because the reveal rules in decision 9 depend on the same predicate and the two must not drift.

Rendered as a second pill beside the status pill, with two labels because the same predicate means different things:

- some member still airing or unaired → **Caught up**
- everything has finished airing → **Completed**

### 8. Score-stat ties return every tied entry

`HighestMalScoreAnimeId` / `MyHighestScoreAnimeId` become `HighestMalScoreAnimeIds` / `MyHighestScoreAnimeIds` (`List<int>`, empty when nothing in the series is scored). Ties are exact equality on the stored values — MAL scores carry one decimal and my scores are integers, so no epsilon is warranted.

Tie ordering:

- **Highest MAL score** — watch order (main-line members by `Order`, then extras). No user input needed and it reads naturally as "seasons 2 and 4".
- **My favourite** — `FavouriteRank` ascending with unranked entries last, then watch order.

**Highest MAL score** also gains a reveal opt-in: it is shown in full when the entry holding it is one I have completed *and* scored, since I already know that score. It is a per-entry MAL score, so it rides the existing `ScoreValue completed` prop rather than inventing a mechanism.

### 9. Reveal rules for the MAL averages

These only bite while the global hide-scores toggle is on; with it off nothing changes. In precedence order:

1. `mainLineCompletedByMe` **and no main-line member is currently airing** → **MAL · main series** and **MAL · everything** both reveal, unconditionally. Finishing the main line is the strongest signal there is that spoilers no longer apply.
2. Otherwise a group reveals only when it is fully completed (`isGroupCompleted`) **and** no member *of the entire series* is currently airing.
3. Otherwise it stays blurred behind its reveal control, as today.

Rule 2 deliberately tests the whole series rather than the group: an airing movie or special suppresses the **main series** average too, which is the ask. Rule 1 overriding rule 2 is the explicit resolution of the two rules pulling against each other when a spin-off movie airs after the main line has finished.

`mainLineCompletedByMe` alone isn't "the main line has finished" — it only checks entries that have *finished* airing, so it stays true while a main-line season is itself mid-run (the "Caught up" badge state, decision 7). Rule 1 is only meant for a spin-off airing *after* the main line is genuinely done, so it additionally requires no main-line member to be currently airing; otherwise a series I'm merely caught up on, with its newest main-line season still airing, would reveal that season's own MAL average early.

Everything still routes through `ScoreValue`'s existing `completed` prop, so the user's `alwaysShowCompletedScores` setting remains the master switch.

### 10. The "everything" averages are suppressed when there are no extras

With `extras.length === 0`, **MAL · everything** and **Mine · everything** are computed over an identical member set to their main-series counterparts, so only the two main-series boxes render and the grid drops to two columns. Both are suppressed, not just the MAL one — the redundancy is the same on either side.

### 11. Favourite order persists as `SeriesMember.FavouriteRank`

A nullable `int` on the existing member row, not a new table. `PersistAsync` already updates member rows **in place** for members that survive a rebuild, so a rank costs nothing to preserve; a member that leaves the series takes its rank with it through the row it already owned.

`PUT /api/series/{seriesId}/favourite-order` takes an ordered anime-id list, writes `0..n-1` to those members, and nulls the rank on every other member of the series. Ids not belonging to the series are rejected.

This is user data about the library rather than a display toggle, so it goes in Postgres rather than `localStorage` — it should survive a browser change, the way scores and statuses do.

UI: up/down buttons on each row of the **My favourite** stat, rendered only when 2+ entries are tied. Buttons rather than drag-and-drop — the tied set is 2–4 rows in practice and the app has no drag infrastructure. A click reorders locally, then PUTs the whole list; a failed PUT reverts the local order.

## Risks / Trade-offs

- **A 400-member component slows the read endpoint** → Traversal is two indexed queries per member and projection is one `Include` chain; the realistic ceiling is well under 400. `IsTruncated` still discloses the pathological case instead of the request hanging.
- **The rebuild loop hammers MAL** → Every round is the same 20-fetch, 1/sec-paced budget as today, and the loop stops on no-progress or 12 rounds. Worst case is ~240 fetches over ~4 minutes, from an explicit click, with `RefreshGate` collapsing concurrent rounds.
- **A loop left running after the user navigates away** → The loop checks an abort flag between rounds and stops on unmount; a round already in flight is simply discarded.
- **`FavouriteRank` goes stale when scores change** → Ranks are a tie-break hint over whatever the tied set currently is, not a stored ordering of a fixed set. An entry that drops out of the tie keeps an unused rank; no cleanup, no correctness impact.
- **`MainLineAiredEpisodes` disagrees with a member's own detail page** → Both read the same `EpisodeAiring` rows through `IEpisodeScheduleService`. A series whose airing member has no AniList sync yet reports 0 aired, which renders as an empty blue extent — missing, not wrong.
- **`highestMalScoreAnimeId` → `...Ids` is a breaking DTO change** → Frontend-only consumer; both sides ship in the same change.

## Migration Plan

1. EF migration adds nullable `FavouriteRank` to `SeriesMembers`. Additive, no backfill — existing rows read as null, meaning unranked.
2. Backend and frontend ship together (the stats DTO change is breaking for the client).
3. No data migration for the cap change. Series stored under the old cap pick up the new one on the next Rebuild click or the next 30-day staleness rebuild (decision 3).
4. Rollback: revert the `MemberCap` constant and the frontend; the `FavouriteRank` column can be left in place unused.

## Open Questions

- The 12-round ceiling on the rebuild loop is a first guess (≈240 fetches / ≈4 minutes). Worth tuning once One Piece has actually been filled in end to end.
