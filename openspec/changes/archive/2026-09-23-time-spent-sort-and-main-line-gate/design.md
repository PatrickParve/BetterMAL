## Context

"Most time spent" (added by `archive/2026-09-21-add-time-spent-and-trim-empty-scopes`, design D7/D8) is served by `SeriesRankingIndex.TimeSpentSeries()`. That method sums `MemberWatchedSeconds` over every member of each series and lists the series when the sum is above zero. D8 chose the sum itself as the listing rule on purpose, reasoning that "total above zero" is the same as "something watched". It did not consider extras-only franchises. Watching one movie or OVA of a franchise lists the whole franchise, although its main series has never been touched.

The Series page reads `GET /api/series` (`SeriesListService` → `SeriesRankingIndex.ListedSeries()`). The whole eligible set comes back in one response, and the client sorts it locally through `SERIES_SORT_COMPARATORS` in `frontend/src/utils/anime.ts`. Every sort key is a field on `SeriesListItemDto`. Both `ListedSeries()` and `TimeSpentSeries()` iterate the same `_membersBySeriesId` lookup over the same `SeriesRankingMemberProjection` rows. Those rows already carry everything `MemberWatchedSeconds` reads: `EntryStatus`, `EpisodesWatched`, `TotalEpisodes`, `RewatchCount` and `AverageEpisodeDurationSeconds`.

`IsMainLine` is set only for members of the story component's main line. `SeriesGraphBuilder.BuildMemberAsync` sets it from `telling.MainLineIds`, which is derived from Core members. Version neighbours (`FoldedVersion`, `NeighbourTelling`) are never main line. Every alternative in a version slot is main line, and the default one is marked by `DefaultVisibleMainLineAnimeIds`.

## Goals / Non-Goals

**Goals:**

- "Most time spent" lists a franchise only when at least one main-line member has been watched. Its total keeps covering every member.
- The Series page can sort by exactly the figure "Most time spent" ranks by, computed once and not duplicated.
- The browser sort stays client-side with no new request, like every other sort.

**Non-Goals:**

- Showing the time total on the Series card. It is a sort key only, like My progress's ratio.
- Changing how the total is computed. The per-member arithmetic, `WatchMath` reuse and Days-stat agreement all stay as they are.
- Applying the main-line gate to the Series page's listing or to the Time spent sort (see D3).
- Changing "Most rewatched by series". Its listing rule stays "rewatch total above zero". A rewatch of one extra is a rewatch, and that section was not part of the request.

## Decisions

### D1. The gate is "some main-line member has watch time above zero", reusing `MemberWatchedSeconds`

`TimeSpentSeries()` gains one predicate before the sum:

```
members.Any(m => m.IsMainLine && MemberWatchedSeconds(m) > 0)
```

`MemberWatchedSeconds` is already zero for a member not in my list, and is otherwise episodes × a strictly positive duration. So "above zero" means exactly "at least one episode counted for this member". That covers every case the request lists: a Dropped entry with one episode, a Watching entry on a still-airing show, and a Rewatching entry whose `EpisodesWatched` has reset to 0, where `FirstViewingEpisodes` still counts one complete run.

*Alternatives considered:*

- **`EpisodesWatched > 0` on the raw field.** This would wrongly exclude a Rewatching member that has not started its new run yet. It would also disagree with the total, which does count that member's first viewing.
- **`WatchMath.FirstViewingEpisodes(...) > 0`.** Nearly identical, but it would differ in one degenerate case: `RewatchCount > 0` with `EpisodesWatched == 0` on a non-Rewatching entry. Reusing the member's own contribution means the gate and the total can never disagree about whether a member was watched.

### D2. The gate covers the whole main line, not the default combination

Any `IsMainLine` member qualifies, including a non-default alternative in a version slot. The request is "at least one episode of the main series watched", and an alternative telling of the main story is main series. The default combination exists so a picker-less card shows one coherent route's episode figures. It says nothing about whether I have watched the franchise. The score averages already cover the whole main line for the same kind of reason (series-browser "A card's figures cover its own telling").

In practice the two readings rarely differ, because `SeriesVersionSlots.DefaultAlternativeId` picks the default by watch progress first. The branch I have watched most becomes the default, so a watched alternative is usually the default one. They differ only when rule 1 is skipped or tied. The realistic case is an alternative marked Rewatching with no episodes of the new run watched: its raw `EpisodesWatched` is 0, so rule 1 sees nothing watched and the default falls to MAL score. That member still has a full first viewing by D1. Gating on the whole main line keeps that case correct without the gate depending on how the default is chosen.

*Alternative considered:* restricting the gate to `DefaultVisibleMainLineAnimeIds`. In the edge case above, that would drop a franchise I have watched only through a non-default route from "Most time spent", while its total, which covers every member, still counted that route. It would also make listing depend on the default-picking heuristic. It is more code for a worse answer.

Version neighbours never satisfy the gate because they are never `IsMainLine` (see Context). No `MembershipKind` check is added. A test pins the neighbour-only case so that a future change to how `IsMainLine` is assigned would fail loudly.

### D3. The browser sorts by the raw total, not by the profile's gated figure

`SeriesListItemDto` gains `long WatchedSeconds`, the ungated sum. The client sorts by it descending. A series the profile omits, such as one with only an extra watched, is ordered by its real total on the Series page.

The request asks for "the same time used in the profile page". The time is the figure; the gate is a listing rule for a ranked strip. The Series page already lists every franchise with any member in my list, plan-to-watch-only ones included. Zeroing or nulling some of their totals would make the sort misstate a figure to hide a series the page is showing anyway. With the raw total, every series that appears on both surfaces carries the identical figure. That is the property "same time" needs.

*Alternative considered:* sorting gated-out series as zero or null. The browser would agree with the profile's membership, but it would misreport a real total and add a second, browser-only rule for what the time figure means. If the user later wants the browser to follow the gate, it is a one-line comparator change: key on `watchedSeconds` only when a main-line-watched flag is set. That would need one more DTO field, which this change deliberately does not add.

### D4. One shared per-series helper

A private `SeriesWatchedSeconds(IEnumerable<SeriesRankingMemberProjection>)`, meaning `members.Sum(MemberWatchedSeconds)`, is called by both `TimeSpentSeries()` and `ListedSeries()`. The two surfaces then cannot drift apart. This is the same approach the index already takes with `MainLineAverageRankOf` for `EligibleSeries()`/`ListedSeries()`. A test builds one series and asserts that `ListedSeries()`'s `WatchedSeconds` equals `TimeSpentSeries()`'s for it. That pins the "same time" promise the way the existing Days-stat test pins D7's.

The sum covers every member, as the profile's does. It does not use the default-combination `scopedMainLine` that the card's episode figures use (series-browser spec delta: "a sort key and not a card figure").

### D5. `long` seconds on the wire, compared numerically on the client

`WatchedSeconds` is `long` in C#, matching `TimeSpentSeriesItemDto.WatchedSeconds`, and `number` in TypeScript. Real totals are far below 2^53, so JSON number precision is not a concern. The comparator is `seriesNullsLast((item) => item.watchedSeconds, 'descending')`. The value is never null, so the nulls-last wrapper only keeps the table uniform. Zero sorts last by value, and `sortSeries`'s shared title tie-break orders the zero-total tail.

### D6. Sort option placement and label

"Time spent" is appended after "My progress" in `SORT_OPTIONS`. The existing order runs scores → title → status → dates → personal progress, and time spent is a personal-viewing figure like progress. `SeriesSortKey` gains `'timeSpent'`. `isSortKey` validates URL values against `SORT_OPTIONS`, so `?sort=timeSpent` works with no further change, and an old link without it is unaffected. The default stays `myScore`.

### D7. Empty-state copy

The profile message changes from "No series have any watch time yet." to a sentence stating the new rule, "No series have any of their main series watched yet." The link to the Settings page's "Build all series from my list" stays, since an unbuilt series still cannot be listed.

## Risks / Trade-offs

- **[A franchise disappears from "Most time spent" for someone who watches only its films.]** → This is the intended behaviour. The request makes main-line viewing the bar. The Series page's Time spent sort still shows that franchise's real total.
- **[Mis-classified main lines.]** A series whose graph build puts a de-facto main entry among the extras would now be omitted. → This inherits the existing main-line classification, which every main-line figure already relies on. It is not a new failure mode. Fixing the classification fixes this too.
- **[The browser and profile orders can differ on exact ties.]** The profile tie-breaks on the resolved `Title`; the browser tie-breaks on the displayed title, which may be the English title. → This is accepted and stated in the spec scenario. Each surface keeps its existing tie-break rule rather than one being bent to match the other.
- **[Extra per-series work on every `/api/series` read.]** → `MemberWatchedSeconds` is O(members) arithmetic over rows already in memory. It adds no query and no MAL call.

## Migration Plan

No data migration. The DTO change is additive: an older client ignores `watchedSeconds`, and the new client is deployed with the new backend. Rollback is a plain revert.
