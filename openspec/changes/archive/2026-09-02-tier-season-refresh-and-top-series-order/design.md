## Context

Two unrelated items, each a rule that already exists somewhere in the app and needs to exist in one more place — or needs a parameter it currently hard-codes.

Current state worth naming:

- **The Series page's My average tie-break is already specified and already computed.** `polish-search-sort-and-titles` added `MainLineAverageRank` and `MainLineAiredCount` to `SeriesListItemDto`, and `frontend/src/utils/anime.ts` composes `compareMyScoreChain` from three small null-last comparators. The chain is: my-average descending → average rank ascending → main-line episodes aired descending → display title (added by `sortSeries` itself).
- **The profile's Top series read is a different index method with a different eligibility rule.** `SeriesRankingIndex.EligibleSeries()` and `SeriesRankingIndex.ListedSeries()` walk the same in-memory member index but differ deliberately in both directions: `EligibleSeries` counts *any* member in my list (a version-neighbour extra included) and additionally applies the two-aired-main-line-entries coverage rule; `ListedSeries` requires a `Core` member in my list and applies no coverage rule. Neither can be expressed as a filter over the other, so this change adds figures to `EligibleSeries` rather than merging the two.
- **`SeriesRankingResult` carries neither figure.** It has the averages, the entry count, and `MainLineAiredCount` — but no rank average and no aired-episode count. `EligibleSeries()` also takes no arguments, where `ListedSeries` takes the aired-episode map and the ranking snapshot.
- **`ProfileService` already injects `IEpisodeScheduleService`** (for the all-list episode progress bar) but not `IAnimeRankingService`.
- **The season refresh gate is three lines.** `SeasonBrowseService.RefreshAsync` locks per season, reads `LastFetchedAt`, and returns `Skipped` when `broadcastConverter.GetLocalDate(fetchedAt) == todayLocalDate`. Everything downstream — `FetchAndCacheAsync`, the fetch-log stamp, the outcome fold, the horizon ceiling — hangs off that one comparison and is otherwise indifferent to why a refresh was skipped.
- **`RefreshYearAsync` is a loop over `RefreshAsync`.** A year has no gate of its own; it inherits whatever the per-season gate decides, four times, in calendar order.
- **The forward horizon depends on daily re-probing.** `GetBoundsAsync` lowers the navigable ceiling past a season MAL answered `404` for *on the current local date*, and re-probes the next day. That only works if future seasons keep being fetched daily.

## Goals / Non-Goals

**Goals:**

- Two franchises tied on my average are ordered the same way on the profile as on the Series page, from the same numbers, not from two derivations of them.
- An old season costs a MyAnimeList request proportional to how likely it is to have changed.
- The age rule is one small pure function with its own tests, not a condition inlined in a service method.
- The forward-season horizon keeps rolling forward on its own, untouched.

**Non-Goals:**

- No change to Top series *eligibility* — the membership rule and the two-aired-main-line-entries coverage rule stay exactly as they are, and no control switches either off.
- No change to the **MAL score** basis on Top series: it keeps ordering by MAL average, then scored main-line count, then title.
- No change to the Series page's own sort, filters, or figures.
- No change to what triggers a refresh (a visit, never a timer or a control), to single-flight, to the four outcomes' names or wire values, or to how the client renders them.
- No user-facing control over the refresh interval, and no display of when a season was last fetched.
- No new stored column and no migration. Both new Top series figures are derived at read time, as their Series-page counterparts already are.

## Decisions

### D1. Top series gains the two figures on `SeriesRankingResult`, computed by the same code `ListedSeries` uses

`EligibleSeries()` becomes `EligibleSeries(Dictionary<int, int> airedEpisodesByAnimeId, AnimeRankingSnapshot ranking)` — the same two parameters `ListedSeries` already takes, in the same order — and `SeriesRankingResult` gains:

- `double? MainLineAverageRank` — the mean of `ranking.RankOf(m.AnimeId)` over main-line members that hold a rank, `null` when none does;
- `int MainLineAiredEpisodes` — the same figure `ListedSeries` reports.

The arithmetic is not restated. `ListedSeries` already computes both, so both move into private helpers on the index (`MainLineAverageRank(mainLine, ranking)` and the existing `ListedSeriesMainLineAiredEpisodes`) that both methods call. Two methods deriving "the same figure" from the same rows by two expressions is exactly how the profile and the Series page came to disagree in the first place.

*Alternatives:* computing the rank average on the client (the profile page has no ranking data and would need a second full read); having Top series call `ListedSeries` and filter (the two eligibility rules differ in both directions — see Context — so this would silently change which series the strip shows); storing a rank on the series member (ranks are derived on purpose, so a stored copy is wrong the moment a score changes).

### D2. Each figure keeps the member scope its Series-page counterpart has

This is the part that is easy to get subtly wrong, so it is stated explicitly:

- **`MainLineAverageRank` covers the whole, unfiltered main line.** It breaks a tie in `MineMain`, which is itself computed over the whole main line, so it must describe the same member set (`polish-search-sort-and-titles` D8).
- **`MainLineAiredEpisodes` covers `scopedMainLine`** — the default combination of version alternatives — because that is what `ListedSeries` reports and what the Series page's chain therefore compares. A profile tile carries no version picker either, so the default combination is the only one it could mean.

`EligibleSeries` does not currently compute `scopedMainLine`; it gains the same `DefaultVisibleMainLineAnimeIds(mainLine)` call `ListedSeries` makes. That call is pure over rows already in memory.

### D3. `ProfileService` resolves the aired-episode map the same way `SeriesListService` does

`GetTopSeriesSectionAsync` gains, before `EligibleSeries`:

```
var currentlyAiringIds = rankingIndex.CurrentlyAiringMainLineAnimeIds();
var airedEpisodesByAnimeId = currentlyAiringIds.Count > 0
    ? await episodeScheduleService.EpisodesAiredAsOfAsync(currentlyAiringIds, DateTimeOffset.UtcNow, ct)
    : [];
var ranking = await rankingService.GetSnapshotAsync(ct);
```

— three lines lifted from `SeriesListService.GetSeriesListAsync`, using the id-taking `EpisodesAiredAsOfAsync` overload that exists for exactly this projection-without-entities case. `ProfileService` gains `IAnimeRankingService` in its primary constructor; it already has the schedule service.

Cost: one batched aired-episode read (only when some main-line member is currently airing) plus the ranking snapshot's two reads (all entries, plus the stored hand-order), per Top series request. The Series page pays the identical price on every visit. Nothing here builds a series or calls MyAnimeList, so the endpoint's existing cost property holds. `GetRewatchedSeriesSectionAsync` shares the same index but needs neither figure and is left alone.

### D4. The server's default order is the full chain, not a prefix of it

`GetTopSeriesSectionAsync` currently pre-orders by my average → scored count → raw title so that a client doing nothing with the basis control still gets a sensible order. That order becomes the my-score chain in full:

1. `MineMain.Value` descending, nulls last;
2. `MainLineAverageRank` ascending, nulls last;
3. `MainLineAiredEpisodes` descending;
4. `Title`, case-insensitively.

The client re-sorts anyway, so this is belt-and-braces — but a default order that is a *different* order from the one the page shows is a trap for the next reader, and the wire order is what a test can assert on cheaply.

### D5. The client's my-score chain is the same comparator shape, not a copy of `sortSeries`

`ProfilePage.rankTopSeries` keeps its two-step structure — filter out series with no value under the selected basis, then sort — and the sort splits by basis:

- **mine**: my-average descending → `mainLineAverageRank` ascending, nulls last → `mainLineAiredEpisodes` descending → display title;
- **mal**: unchanged (MAL average descending → MAL scored count descending → title).

`utils/anime.ts`'s `seriesNullsLast` and `compareMyScoreChain` are typed to `SeriesListItemDto`, and `TopSeriesItemDto` is a different (smaller) record, so the chain is not literally reusable as-is. Rather than widening the utility's type to a structural interface for one caller, the profile composes the same three steps locally with a comment naming `SERIES_SORT_COMPARATORS.myScore` as the rule it must move with. The spec, not a shared symbol, is what binds them — as it already does for the multi-entry filter, which `filterSeries` and `filterMultiEntry` implement separately on purpose.

The nulls-last direction matters and is easy to invert: a *lower* rank number is *better*, so the comparator is ascending, and a series with **no** rank sorts **after** every tied series that has one.

**Final tie-break: display title.** The Series page's chain ends on `pickDisplayTitle`, and the profile's ends on the raw `title`; the mine chain adopts the display title so the two are the same comparison. The MAL basis keeps the raw title, matching its unchanged server order. Neither is visible — a Top series tile shows a poster and two chips, no title — so this settles a rule rather than changing an appearance.

**One branch of the Series page chain is unreachable here.** On the Series page, two series that both have *no* my-average compare equal and continue down the chain. On the profile, a series with no value under the selected basis is filtered out before sorting (an existing rule, unchanged), so that case cannot arise. The chain is stated in full anyway — the filter is what makes the branch unreachable, not the comparator, and if the filter ever changes the comparator should not need to.

### D6. Season age is measured from the first day of its quarter

`SeasonCalendar` owns MAL's quarter convention (winter = Jan–Mar, spring = Apr–Jun, summer = Jul–Sep, fall = Oct–Dec), so it gains:

```
public static DateOnly SeasonStart(int year, string season)   // (year, 3 * index + 1, 1)
```

Age is whole elapsed years from that date to today's **local** date — the same local date the fetch gate already uses, so age and staleness are measured on one calendar. Whole years, not fractions: the boundaries the rule states are "a year old", "two years old", "five years old", and a whole-year count says exactly that.

*Alternatives:* the season's *end* (a currently-airing season would then be aged from a future date, and every rule would need a special case); the newest `AiredFrom` among its cached anime (data-dependent, so a season's cadence would change as its listing changed, and a season with nothing cached would have no age at all).

### D7. The interval table is a pure static beside `SeasonCalendar` and `SeasonHorizon`

New `Services/Season/SeasonRefreshCadence.cs`:

| Whole years since season start | Minimum days between fetches |
|---|---|
| 0 — including a season that has not started yet | 1 |
| 1 | 3 |
| 2, 3, 4 | 5 |
| 5 or more | 10 |

```
public static int MinimumDaysBetweenFetches(int year, string season, DateOnly todayLocalDate)
public static bool IsFresh(int year, string season, DateOnly lastFetchedLocalDate, DateOnly todayLocalDate)
```

`IsFresh` is `todayLocalDate.DayNumber - lastFetchedLocalDate.DayNumber < MinimumDaysBetweenFetches(...)`. Both are pure and take today as a parameter, so the tests state a date instead of arranging one — the shape `SeasonCalendar` and `SeasonHorizon` already use, and the reason both have their own test files.

**The tier-0 case is exactly today's rule.** An interval of 1 day means "skip only when the last fetch was on today's local date" — the same comparison `RefreshAsync` makes now. So this generalizes the existing rule rather than replacing it, and a current-or-recent season's behaviour is provably unchanged.

*Alternatives:* a `TimeSpan`-based "hours since last fetch" (a season fetched at 23:50 would then be refetched at 00:10 the next night on a 1-day interval, which is not what "once a day" has meant here); putting the table in configuration (nobody is going to tune this, and a config value would need a validation story).

### D8. A future season is age 0, and that is what keeps the horizon working

A season whose start date has not arrived yields a negative elapsed-year count, clamped to 0, so it sits in the daily tier. This is load-bearing, not incidental: `GetBoundsAsync` lowers the navigable ceiling past a season MAL answered `404` for **on the current local date** and re-probes the next day. Put a future season on a 3-day interval and the ceiling would freeze for days at a time. The clamp is written with a comment saying so.

### D9. Only the freshness comparison changes in `SeasonBrowseService`

`RefreshAsync`'s body becomes:

```
var lastFetched = await seasonRepository.GetLastFetchedAsync(year, season, ct);
if (lastFetched is { } fetchedAt
    && SeasonRefreshCadence.IsFresh(year, season, broadcastConverter.GetLocalDate(fetchedAt), todayLocalDate))
    return new SeasonRefreshResultDto(SeasonRefreshOutcome.Skipped);
```

Everything else holds by construction, and each is worth naming because each is a property someone might otherwise think this change breaks:

- **A never-fetched season still fetches**, at any age — the `is { }` guard is unchanged, so the interval is only ever consulted when there is a stamp to compare against.
- **A failed fetch still does not consume the interval** — only `FetchAndCacheAsync` writes the stamp, and it is not reached on the failure path.
- **A `404` still counts as a fetch** — `FetchAndCacheAsync` stamps the log before returning `NotListed`, unchanged. A very old season MAL has no listing for is now re-probed every 10 days instead of daily, which is strictly better.
- **Single-flight is unchanged** — the re-check inside the lock now asks the cadence question, and a waiter that arrives after the first refresh's stamp still sees a fresh stamp and skips.
- **The year path is unchanged** — `RefreshYearAsync` still loops `RefreshAsync` over the four seasons in calendar order, and each now answers on its own age. A year straddling a boundary refreshes some seasons and skips others with no new code, which is precisely the behaviour asked for.

### D10. `Skipped` widens its meaning; the wire contract does not change

`SeasonRefreshOutcome.Skipped` becomes "this season was fetched recently enough for its age" rather than "already fetched today". No enum member, JSON value, or client branch changes: `SeasonPage.tsx` and `YearPage.tsx` use `skipped` only to decide whether a never-cached page should re-read (`result.outcome === 'skipped' && lastFetchedAtRef.current === null`), and that logic is correct under either meaning. The doc comments on the enum and on `RefreshAsync` are updated; nothing else is.

## Risks / Trade-offs

- **An old season's listing changes and the app takes up to 10 days to notice** → That is the trade this change is for. MAL back-catalogue listings are near-static; the visible effect is at most a late-appearing entry on a season browsed by hand, and a metadata refresh through any other path still updates the anime itself. The interval is a single table in one file if it needs tuning.
- **Age tiers shift under a season as time passes, so the same season answers differently in different months** → Inherent to an age rule, and the tier-0 boundary is the only one a user could plausibly notice. The cadence function takes today as a parameter precisely so the boundary behaviour is testable rather than incidental.
- **The forward horizon silently depends on future seasons staying in tier 0** → D8 makes the dependency explicit in code and comment, and it gets a test: a season starting after today must report a 1-day interval.
- **The profile's my-score chain is a second implementation of the Series page's chain** → D5 accepts this rather than widening a shared utility's type for one caller; the binding is the spec, both sides carry a comment naming the other, and the ordering is asserted server-side (D4) as well as being derivable client-side.
- **Two more reads per Top series request** → Both are indexed local reads on a hand-opened page that already loads every series member; the Series page pays the same. Nothing forecloses caching the snapshot per request later.
- **`EligibleSeries()`'s signature changes** → Every call site is in this repo (one service, three test files); the compiler finds them all.
- **A tie-break on ranks is invisible on a poster tile** → Same trade the Series page already accepted: a stable, explainable order replacing an arbitrary one, with the rule written in the spec.

## Migration Plan

None required. No stored data changes shape, so there is no migration and no backfill — both Top series figures are derived at read time from members and entries that already exist, and the season change reads the `SeasonFetchLog` rows already being written. The top-series response gains two fields and removes none, so an older client keeps working against a newer server; a server rollback leaves the extra fields unread. Existing fetch-log stamps are interpreted under the new intervals immediately, which at worst means an old season is not re-fetched on the first visit after deploy. Rollback is a revert.

## Open Questions

None.
