## Context

This change fixes triage items B3 and PF4. The proposal has the evidence with line references. This section covers only what shapes the design. Paths without a prefix are under `backend/AnimeTracker.Api/`.

**How a season fetch writes today** (`Services/Season/SeasonBrowseService.cs:164-251`, `FetchAndCacheAsync`):
1. `malClient.GetFullSeasonAsync` returns `null` on a 404. The service stamps the fetch log and returns `NotListed`.
2. Otherwise it builds `animeIds` (distinct) and `startSeasonById` from the edges.
3. It lean-upserts every returned anime into `AnimeMetadata`: `AiredFrom`, then update detection.
4. It loads this season's listing ids as an untracked `Select(l => l.AnimeId)`.
5. For each returned id not already listed, it adds a `SeasonAnimeListing` unless `start_season` names a different season. A missing `start_season` is trusted.
6. `StampFetchLogAsync` stamps the log and runs the one `SaveChangesAsync`. Everything above is staged until then.

`RefreshAsync` wraps this in the cadence gate, the per-season `RefreshGate` lock, and a catch that turns any exception into `Failed`. `RefreshYearAsync` calls `RefreshAsync` for winter, spring, summer and fall in turn, on the one request-scoped `DbContext`.

**Why a partial list can't happen.** `GetFullSeasonAsync` (`Services/Mal/MalClient.cs:72-94`) reads page 0 through `GetAsyncOrNotFound`, and every later page through `GetSeasonAsync` → `GetAsync`, which calls `EnsureSuccessStatusCode` (`:166`). A failure on any later page, a 404 included, throws out of the loop. `FetchAndCacheAsync` either gets the whole list or never reaches step 2. Every request sends `nsfw=true` (`:98`). `MalClientTests` covers the offsets and the page-0 404, but not a later page failing.

**The year read** (`Data/Repositories/SeasonRepository.cs:76-197`) queries listing *rows* for the year's four points. `items` holds one entry per row, and four id-only ordering queries turn into position maps through `ToPositionMap`. `SeasonRepositoryTests.SortOrderPositionsAreDenseOverTheWholeListing` pins the positions as dense.

**The ceiling today.**
- `GetBoundsAsync` (`SeasonBrowseService.cs:70-98`) works out the current season from the broadcast converter's local date.
- It loads `GetHorizonInputsAsync` for current … current+2. That's one fetch-log query plus one `DISTINCT (Year, Season)` over listings.
- It then calls `SeasonHorizon.Resolve` (`Services/Season/SeasonHorizon.cs:17-44`), which starts at `max(current + FutureSeasonWindow, latest cached)` and walks back past seasons `NotListedToday`, never below current.
- The frontend reads the result through `GET /api/season/bounds`, and uses it only to disable forward navigation.

**Controllers.** `SeasonController` (`Controllers/SeasonController.cs`) checks the season name against a four-item set and returns `BadRequest(new { error = $"Unknown season '{season}'." })`. `YearController` checks nothing. Both take `ISeasonBrowseService` only. There is no `SeasonControllerTests`. `YearControllerTests` uses a hand-written `RecordingSeasonBrowseService` whose unused members throw.

**Decisions already taken with the user** (2026-09-14):
- **Floor:** MAL's first archive year, 1917, not 1989.
- **Upper bound:** the ceiling before today's 404 step-back, not `GetBoundsAsync`'s.
- **Frontend:** none. An out-of-range URL shows the existing could-not-be-loaded state.

**Constraints.**
- Tests use the EF InMemory provider. It doesn't support `ExecuteDeleteAsync`, and can't catch a query that Postgres can't translate.
- The backend builds and tests only in `mcr.microsoft.com/dotnet/sdk:10.0`, from a copy under `/private/tmp`. The local SDK is 9.0, and `~/Documents` can't be bind-mounted.
- Two commits: B3, then PF4.

## Goals / Non-Goals

**Goals:**
- After a fetch that returns anime, the season's listing holds exactly the anime MAL files under it.
- One membership rule decides both adding and removing.
- A 404, an empty 200, a failed fetch and a skipped refresh never remove a row, and tests pin each case.
- The year read shows each anime once, even before the old season has been fetched again.
- The four season and year endpoints refuse a year or season outside [winter 1917, outer ceiling] with 400. The check runs before any cache read, MAL request or fetch-log write, and is worked out again on every request.
- Nothing a working page does today gets refused, old seasons linked from the detail page or Recap included.

**Non-Goals:**
- Changing `GET /api/season/bounds`, `SeasonBoundsDto`, or the navigable ceiling the UI uses.
- Any frontend change: `EARLIEST_YEAR`, navigation guards, or empty-state wording.
- `SeasonRefreshCadence`, `RefreshGate`, MAL paging, the season-name check.
- Cleaning up stale rows ahead of time. Each season is pruned on its own next fetch.
- Filing an anime under the season MAL now names for it, when that season wasn't the one fetched. See D2.
- Working out whether MAL has widened its forward window. See Risks.

## Decisions

### D1. One membership set drives adding and pruning, saved in the same `SaveChangesAsync`

Pull the `start_season` test at `:241-244` out into `private static bool IsFiledUnder(MalStartSeason? startSeason, int year, string season)`. It returns true when `startSeason` is null, or when its year and season (case-insensitive) match. In `FetchAndCacheAsync`, after the upsert loop:

```csharp
var memberIds = animeIds.Where(id => IsFiledUnder(startSeasonById.GetValueOrDefault(id), year, season)).ToHashSet();

var existingListings = await db.SeasonAnimeListings
    .Where(l => l.Year == year && l.Season == season)
    .ToListAsync(ct);                      // tracked, so removals commit with the stamp
var existingIds = existingListings.Select(l => l.AnimeId).ToHashSet();

foreach (var animeId in memberIds.Where(id => !existingIds.Contains(id)))
    db.SeasonAnimeListings.Add(new SeasonAnimeListing { Year = year, Season = season, AnimeId = animeId });

if (edges.Count > 0)                        // D2
    db.SeasonAnimeListings.RemoveRange(existingListings.Where(l => !memberIds.Contains(l.AnimeId)));
```

The comment explaining why MAL's `start_season` is authoritative moves onto `IsFiledUnder`. The add set and the remove set can't overlap: adds are members not yet listed, and removals are listed rows that aren't members. The removals, the adds, the upserts and the fetch-log stamp all commit in the one `SaveChangesAsync` in `StampFetchLogAsync`.

A listing row has three columns, and a season holds at most a few hundred. So loading the entities tracked, where today's code projects only ids, costs nothing that matters.

`RefreshYearAsync` needs no change. Each season it refreshes prunes itself. When a year visit finds both the old and the new season due, one pass removes the anime from one and adds it to the other.

*Alternatives considered:*
- **`ExecuteDeleteAsync`.** Rejected. It deletes straight away, outside the `SaveChangesAsync` unit, so a failed save afterwards would keep the removals but lose the adds and the stamp. The InMemory test provider doesn't support it either.
- **A second check written just for pruning.** Rejected. The brief rules it out, and two copies could disagree about a missing `start_season`.

### D2. Only a response with at least one anime prunes

An empty 200 still reports `Fetched`, still stamps the fetch log, and adds nothing, as today. It removes nothing either.

- `SeasonBrowseDto.cs:6-12`, and the horizon change it came from, already treat an empty 200 as meaning the same as a 404. The spec says a 404 "SHALL keep whatever listing it already had cached".
- A real season holds hundreds of anime. An empty answer about one that has rows is far more likely a MAL fault than a real emptying, and pruning would wipe the whole listing.

A season whose last cached anime really leaves isn't pruned. That can't realistically happen.

`NotListed` and `Failed` never reach this code, and D3 pins that. When the fetch removes everything because every returned anime is filed elsewhere, that's allowed. It's the same state a first fetch of that answer would leave.

*Alternatives considered:*
- **Prune on every `Fetched`.** Rejected, for the reason above.
- **A limit such as "never remove more than half".** Rejected. The number is arbitrary, it would block a real mass re-file, and it answers no failure anyone has seen.
- **Also file a removed anime under the season its `start_season` now names.** Rejected. It would write rows for a season that wasn't fetched, making `HasListing` true there with no fetch-log stamp. That would also move the horizon's latest cached season.

### D3. Tests pin what pruning relies on

New `SeasonBrowseServiceTests` seed listing rows (and their `AnimeMetadata`) straight into the InMemory context, with no fetch-log row. The season counts as never fetched, so `RefreshAsync` fetches whatever its age. The fakes need one change: `PerSeasonFakeMalClient` gets its dictionary from the test, so a test can reassign one season's response between calls.

Cases:
- **The brief's move.** Spring 2020 has rows for 1 and 2. MAL's spring now returns `[2, 3]`, and summer returns `[1 filed summer]`. Refresh summer, then spring. Spring holds {2, 3}, and summer holds {1}.
- **Returned but filed elsewhere.** A row for 1 exists. Spring returns `[1 filed 2019 fall, 2]`. Spring holds {2}.
- **No `start_season`.** A row for 1 exists. Spring returns `[1 with StartSeason = null, 2]`. Spring holds {1, 2}.
- **The four no-prune cases**, one `[Theory]` or four `[Fact]`s. Each starts from rows {1, 2}:
  - `Edges: null` → `NotListed`
  - `Edges: []` → `Fetched`
  - `Throws: true` → `Failed`
  - a fetch-log stamp from today → `Skipped`

  The rows are still {1, 2} each time, and each asserts its outcome.
- **The year.** Rows for 1 in spring 2020, plus others. MAL moves 1 to summer, and the other two seasons return `null`. `RefreshYearAsync(2020)`, then `GetYearPageAsync(2020, false)`, lists 1 once. The rows for 1 are summer only.

`MalClientTests` adds season tests for a failure partway through paging. Page 0 returns 100 entries with `next` at offset 100. Offset 100 answers `500` in one case and `404` in the other. `GetFullSeasonAsync` throws (`Assert.ThrowsAnyAsync<HttpRequestException>`), and makes no third request. "Failed partway means failed" was true by construction until now. This makes it tested.

### D4. The combined read keeps each anime's first row, and positions stay dense

In `SeasonRepository.GetListingAsync`:
- `items = (await projected.ToListAsync(ct)).DistinctBy(a => a.Id).ToList()`
- `ToPositionMap` skips an id it has already placed: `if (positions.TryAdd(id, positions.Count)) …`, in place of `positions[orderedIds[i]] = i`

Two rows for the same anime project identically, since the projection has no season column. So they sort next to each other under every ordering, and the order of what's left doesn't change. A single-season read can't produce a duplicate, because of the key, so the change does nothing there.

This covers the window D1 can't close by itself: MAL moves an anime from spring to summer, summer gets fetched, and spring is skipped until its next interval. That's up to a day for a recent season.

*Alternatives considered:*
- **`projected.Distinct()` in SQL.** Rejected. The five queries would then order and left-join over a `DISTINCT` subquery. That probably translates on Postgres, but InMemory tests couldn't show it, and a translation failure would take down every season and year page.
- **Root the query at `AnimeMetadata` with an `EXISTS` on listings.** Rejected for the same reason, and it would mean rewriting a query that carries the ranking rules.
- **Rely on pruning alone.** Rejected. The year spec says each anime appears once, and pruning alone would break that for a day at a time.

### D5. `SeasonHorizon` splits out the outer ceiling

Add `public static (int Year, string Season) ResolveOuter((int Year, string Season) current, (int Year, string Season)? latestCachedSeason)`. It returns `max(current + FutureSeasonWindow, latest cached)` as a season point.

`Resolve` then starts from `ResolveOuter`'s index and keeps its step-back loop unchanged. Its results don't change, so `SeasonHorizonTests` stays as it is. New unit tests pin `ResolveOuter`:
- the default is current+2
- it rises to a later cached season
- a cached season below current+2 doesn't lower it
- it moves forward with `current` (the "moves with the calendar" scenario, tested purely)

The API checks against the outer ceiling, not `Resolve`'s. The seasons between the two are exactly the ones `NotListedToday` stepped back past. Every one of them has a fetch-log stamp from today, so a refresh returns `Skipped` with no MAL request. Accepting them costs nothing. Refusing them would break the page's re-read after its own `notListed` refresh (proposal, "Verified").

*Alternatives considered:*
- **`GetBoundsAsync`'s resolved ceiling.** Rejected, as described above.
- **A fixed allowance such as current+3.** Rejected. It defines MAL's window a second time, and would let an extra season through every day.

### D6. The floor is winter 1917

Add `public const int EarliestArchiveYear = 1917;` to `SeasonCalendar`, since it's another fixed MAL fact about seasons. Its comment says it's the first year on `myanimelist.net/anime/season/archive` (checked 2026-09-14). It is not the frontend's `EARLIEST_YEAR` (1989), which only sets how far back the arrows and dropdowns go.

The range also closes a quiet 500. Once a season with a year outside 1–9999 has a fetch-log stamp, `SeasonRefreshCadence` (`:26`) calls `SeasonCalendar.SeasonStart`, which builds an invalid `DateOnly` and throws outside `RefreshAsync`'s catch. The `{year:int}` route accepts such a year today.

*Alternatives considered:*
- **1989.** Rejected with the user. The detail page's and Recap's links to older seasons would get 400.
- **Return the earliest year from `GET /api/season/bounds`, so the frontend reads one source.** Rejected. The two numbers mean different things. Feeding 1917 to the frontend would lengthen its dropdowns by 70 years, and feeding 1989 to the API brings back the broken links. The backend check never reads anything the client sends.
- **No floor.** Rejected. Any integer would still leave a fetch-log row, and `SECURITY_REVIEW` §6 asks for a range on both ends.

### D7. `SeasonRequestRange` holds the check, and the controllers call it on every request

New `Services/Season/SeasonRequestRange.cs`:

```csharp
public sealed record SeasonRequestRange(int EarliestYear, int LatestYear, string LatestSeason)
{
    public bool ContainsYear(int year) => year >= EarliestYear && year <= LatestYear;

    // Year bounds first: GetSeasonPointIndex is year * 4, which wraps in the
    // project's default unchecked arithmetic for a large enough {year:int}
    // (1073743741 * 4 lands on winter 1917's index), so a point-index
    // comparison alone could accept a bogus year.
    public bool ContainsSeason(int year, string season) =>
        ContainsYear(year) &&
        (year < LatestYear || SeasonCalendar.GetSeasonIndex(season) <= SeasonCalendar.GetSeasonIndex(LatestSeason));
}
```

The earliest end is always winter, so a year at or above `EarliestYear` needs no season test there. The caller must already have checked the season name, since `GetSeasonIndex` returns -1 for an unknown one.

`ISeasonBrowseService` gains `Task<SeasonRequestRange> GetRequestRangeAsync(CancellationToken ct = default)`. `SeasonBrowseService` moves the first half of `GetBoundsAsync` into a private `LoadHorizonAsync(ct)`, which returns `(current, todayLocalDate, inputs)`. `GetBoundsAsync` keeps the rest unchanged. `GetRequestRangeAsync` returns `new SeasonRequestRange(SeasonCalendar.EarliestArchiveYear, outer.Year, outer.Season)`, where `outer = SeasonHorizon.ResolveOuter(current, inputs.LatestCachedSeason)`. Nothing is cached, and it runs for every call.

In the controllers:
- **`SeasonController.GetPage` and `Refresh`:** the name check stays first. Then `var range = await seasonBrowseService.GetRequestRangeAsync(ct);`. When `!range.ContainsSeason(year, season)`, return `BadRequest(new { error = $"{season} {year} is outside the seasons MyAnimeList lists (winter {range.EarliestYear} to {range.LatestSeason} {range.LatestYear})." })`.
- **`YearController.GetPage` and `Refresh`:** the same with `ContainsYear(year)`, and `error = $"{year} is outside the years MyAnimeList lists ({range.EarliestYear} to {range.LatestYear})."`.

The doc comments on each action say so. `YearController`'s "do not add a year-level bounds endpoint" note stays true.

*Alternatives considered:*
- **Validate inside the four service methods.** Rejected. Their return types would need a "refused" case for the controller to turn into 400, and input checks already live in the controllers, where the name check is.
- **An action filter or attribute.** Rejected. It's too much for four actions, and it would hide the order after the name check.
- **Compare point indices as `long`.** That works too. The year-first check was chosen because it also reads as the year rule `YearController` needs.

Tests:
- `SeasonRequestRangeTests`, pure:
  - both ends accepted
  - 1916 fall refused
  - the ceiling year's later season refused
  - an earlier season in the ceiling year accepted
  - `int.MaxValue` and `1073743741` refused for both `ContainsYear` and `ContainsSeason`
  - negative years refused
- `SeasonControllerTests`, new, with a recording fake returning a fixed range (winter 1917 to winter 2027):
  - 9999 winter, 1800 winter and 2027 spring get 400 on both `GetPage` and `Refresh`, and the fake records no page or refresh call. That's the controller-level proof that no MAL request or fetch-log write happens.
  - 2027 winter and 1917 winter get 200, and the call is passed through.
  - `autumn` still gets the unknown-season 400, and the range is never asked for.
- `YearControllerTests`: the fake gains `GetRequestRangeAsync`, and the existing tests keep passing. Add 2027 → 200, 2028 → 400 and 1916 → 400, for both actions, with no service call on refusal.
- `SeasonBrowseServiceTests` (InMemory, anchored to today like `SeasonWholeYearsAgo`):
  - `GetRequestRangeAsync` with nothing cached returns current+2
  - a listing row seeded at current+5 raises it to current+5
  - current+2 stamped today with no rows (MAL's 404 today) still leaves it at current+2, while `GetBoundsAsync` returns current+1
  - a `RefreshAsync` of that current+2 season returns `Skipped` with no MAL call

### D8. The frontend is unchanged

A URL the API refuses goes like this. `usePageData` swallows the 400 on the read (`frontend/src/hooks/usePageData.ts:95`). The refresh's 400 lands in the `catch` → `refreshOutcome = 'failed'`. With `lastFetchedAt` null, that shows `loadFailed`: "This season couldn't be loaded — it'll be retried next time you open it."

The "retried" half isn't true for such a URL. The user accepted that, since only a typed URL gets there (proposal, "What Changes"). The spec deltas say the page shows its could-not-be-loaded state, and add no fourth empty state.

### D9. Commit split

1. **B3**:
   - `SeasonBrowseService.cs` (D1, D2)
   - `SeasonRepository.cs` (D4)
   - `SeasonBrowseServiceTests.cs`, `SeasonRepositoryTests.cs`, `MalClientTests.cs` (D3)
   - the `CODE_GUIDE.md` season note
2. **PF4**:
   - `SeasonHorizon.cs`, `SeasonCalendar.cs`, `SeasonRequestRange.cs`, `ISeasonBrowseService.cs`, `SeasonBrowseService.cs` (D5–D7)
   - both controllers and their tests
   - the `CODE_GUIDE.md` endpoint rows
   - archiving this change, which writes both spec deltas

`SeasonBrowseService.cs` is in both commits, in unrelated methods.

## Risks / Trade-offs

- **[Finding out that MAL has widened its window is no longer possible]**
  - Today, the only way a season past current+2 ever got cached (and so raised the ceiling) was someone typing its URL. The API now refuses that.
  - A detail-page link to an anime MAL files past current+2 would also show could-not-be-loaded.
  - → MAL gives no season classification to anime past its window (`season-browser/spec.md:146`), so that link shouldn't come up. Each season comes into the window by itself within a quarter. If MAL widens the window for good, raise `FutureSeasonWindow`. Existing cached later seasons still count.
- **[A paging shift can prune an anime that's still listed]**
  - If MAL's season list changes between two page requests, one entry can fall between offsets.
  - Before, that was harmless. Now the row is removed until the season's next fetch: a day for a recent season, up to 10 days for one five or more years old.
  - → Old seasons' lists barely change. Paging follows MAL's own `next` offsets. The next fetch puts the row back.
- **[An anime is briefly in neither season]**
  - When the old season is fetched (pruned) before the new season is due, the anime shows on neither page until the new season is fetched.
  - → A year visit refreshes all four seasons in one pass. A season-page visit to the new season adds it straight away. Both happen within the seasons' own intervals.
- **[A refused URL says "it'll be retried"]** → Accepted (D8). It's reachable only by typing a URL.
- **[Browser and server disagree at a quarter boundary]**
  - Until `GET /api/season/bounds` answers, or if it fails, the page uses a current+2 worked out from the browser's clock.
  - A browser a few hours ahead of the server's local date at a quarter boundary could step to a season the API still refuses, and see could-not-be-loaded until the server's date catches up.
  - → Rare, short, and the bounds response replaces the guess as soon as it arrives.
- **[Two more small queries per season or year request]** `GetRequestRangeAsync` reuses the horizon load: a fetch-log lookup for three points, and a `DISTINCT (Year, Season)` over listings. → Both are small next to the listing read's five queries. The alternative is a second query shape for the same fact.
- **[Changes staged by a failed save carry over within a year refresh]**
  - Already true today: if one season's `SaveChangesAsync` throws inside `RefreshYearAsync`, its staged changes stay on the shared context, and the next season's save commits them.
  - Removals now join the adds and the stamp in that.
  - → Not changed by this design. What gets committed is still the result of a complete MAL answer for that season.

## Migration Plan

No schema or data migration. Stale rows from before this change are removed the next time their season is fetched, on its own interval, with no backfill. Rolling back means reverting the two commits. Rows already pruned don't come back, and they were stale anyway.

## Open Questions

None. The floor, the upper bound and the frontend question were settled with the user before this design was written.
