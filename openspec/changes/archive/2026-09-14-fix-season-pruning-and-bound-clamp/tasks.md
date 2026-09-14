Paths without a prefix are under `backend/AnimeTracker.Api/`. Test paths are under `backend/AnimeTracker.Api.Tests/`.

**Two commits.**
- Groups 1–4 (B3) are the first commit.
- Groups 5–9 (PF4) are the second. The spec deltas for `season-browser` and `year-browser` are written into `openspec/specs/` when this change is archived, after group 9, in the same commit as PF4, as earlier changes did.

## 1. B3: prune on a fetch that returns anime (design D1, D2)

- [x] 1.1 In `Services/Season/SeasonBrowseService.cs`, add `private static bool IsFiledUnder(MalStartSeason? startSeason, int year, string season)`. It returns `true` when `startSeason` is null, or when `startSeason.Year == year` and `startSeason.Season` equals `season` (`StringComparison.OrdinalIgnoreCase`). Move the comment at `:235-240` (MAL's `start_season` is authoritative, and a missing one trusts the endpoint) onto it, and add one line: adding and pruning both use this, so they can't disagree. `MalAnimeNode.StartSeason` is `MalStartSeason?` (`Services/Mal/Dto/MalAnimeNode.cs:22,45-49`).
- [x] 1.2 In `FetchAndCacheAsync`, replace the block at `:225-247` as design D1 shows:
  - `memberIds`, from `animeIds` filtered through `IsFiledUnder`
  - `existingListings`, a *tracked* `ToListAsync` of this season's `SeasonAnimeListing` rows, and `existingIds` from it
  - add a row for each member not in `existingIds`
  - when `edges.Count > 0`, `RemoveRange` the existing rows whose `AnimeId` isn't a member

  Leave the upsert loop above it and `StampFetchLogAsync` below it untouched.
- [x] 1.3 Put a comment on the prune line saying:
  - only a response with anime prunes
  - an empty 200 means the same as "not listed" (`SeasonBrowseDto.cs:6-12`), and removing on it would wipe the season
  - `NotListed` and `Failed` never get this far
  - `GetFullSeasonAsync` is all-or-nothing, so a failure partway through paging throws, never returns a shorter list
- [x] 1.4 Update the `SeasonBrowseService` class and `ISeasonBrowseService.RefreshAsync` doc comments, where they describe what a fetch writes, to mention removal. Confirm `RefreshYearAsync` needs no change beyond that (design D1).

## 2. B3: the combined read shows each anime once (design D4)

- [x] 2.1 In `Data/Repositories/SeasonRepository.cs` `GetListingAsync`, de-duplicate `items` by `Id` after `ToListAsync`, keeping the first. Add a comment:
  - a year read can see an anime under two of its seasons between fetching the season MAL moved it to and fetching the old one again
  - two such rows project identically, since there's no season column
  - de-duplicating in memory, not with SQL `Distinct()`, keeps the five queries as they are (design D4)
- [x] 2.2 In `ToPositionMap`, place each id only once (`TryAdd(id, positions.Count)`), so positions stay dense when an id repeats. Extend its comment to say so.

## 3. B3: tests (design D3)

- [x] 3.1 In `Services/Season/SeasonBrowseServiceTests.cs`, add helpers:
  - `SeedListing(db, animeId, year, season)`: adds `AnimeMetadata { Id, Title = $"Anime {id}" }` if it's missing, plus the listing row, then saves
  - `ListedIds(db, year, season)`: the sorted ids for that point

  Reuse `SeasonEdge`, adding an overload or optional parameter that gives a `null` `StartSeason`. Use 2020 seasons with no fetch-log row, so each refresh fetches whatever the season's age.
- [x] 3.2 **A moved anime leaves its old season.**
  - Seed spring 2020 with {1, 2}.
  - A `PerSeasonFakeMalClient` answers spring with `[2, 3]` (filed spring 2020) and summer with `[1]` (filed summer 2020).
  - `RefreshAsync(2020, "summer")`, then `RefreshAsync(2020, "spring")`. Both report `Fetched`.
  - Spring lists {2, 3}, and summer lists {1}.
- [x] 3.3 **Returned but filed elsewhere is pruned.** Seed spring 2020 with {1}. Spring answers `[1 filed fall 2019, 2 filed spring 2020]`. After the refresh, spring lists {2}.
- [x] 3.4 **No `start_season` is kept.** Seed spring 2020 with {1}. Spring answers `[1 with no start season, 2 filed spring 2020]`. After the refresh, spring lists {1, 2}.
- [x] 3.5 **Nothing back removes nothing.** A `[Theory]`, or four `[Fact]`s. Each seeds spring 2020 with {1, 2} and asserts the outcome, and that spring still lists {1, 2}:
  - `Edges: null` → `NotListed`
  - `Edges: []` → `Fetched`
  - `Throws: true` → `Failed`
  - a `SeasonFetchLog` stamped `DateTimeOffset.UtcNow` → `Skipped`, with `FullSeasonCallCount == 0`
- [x] 3.6 **The year shows a moved anime once.**
  - Seed spring 2020 with {1, 2}.
  - The fake answers winter and fall `null`, spring `[2]`, and summer `[1 filed summer]`.
  - `RefreshYearAsync(2020)`, then `GetYearPageAsync(2020, hideHentai: false)`.
  - The item ids are exactly {1, 2}, each once. Spring lists {2}, and summer lists {1}.
- [x] 3.7 In `Data/Repositories/SeasonRepositoryTests.cs`, add **a duplicate row is read once**:
  - Seed anime 1, 2 and 3, listing 1 under both spring and summer 2020, 2 under spring and 3 under fall.
  - `GetListingAsync(Year2020, false)` returns 3 items with distinct ids.
  - For each of the four orderings, the positions are exactly {0, 1, 2}.
  - The relative order matches the same data seeded without the duplicate row.

  Confirm `SortOrderPositionsAreDenseOverTheWholeListing` and `FullListingReturnsTheUnionWithNoDuplicates` still pass unchanged.
- [x] 3.8 In `Services/Mal/MalClientTests.cs`, under `GetFullSeasonAsync`, add **a failure partway through paging throws**, a `[Theory]` over `HttpStatusCode.InternalServerError` and `HttpStatusCode.NotFound`:
  - offset 0 answers `AnimeListPage(1, 100, next: "…/anime/season/2024/spring?offset=100&limit=100")`
  - offset 100 answers that status with `""`
  - `await Assert.ThrowsAnyAsync<HttpRequestException>(() => client.GetFullSeasonAsync(2024, "spring"))`
  - the requested offsets are `[0, 100]`

  Add a comment: season pruning depends on this, since a partial list must never reach `FetchAndCacheAsync`.

## 4. B3: build, test and docs

- [x] 4.1 Copy `backend/` to `/private/tmp/bm-build/` (excluding `bin/` and `obj/`), then run `dotnet build` and `dotnet test` in `mcr.microsoft.com/dotnet/sdk:10.0`. Everything must pass.
- [x] 4.2 In `CODE_GUIDE.md` `### Services/Season/` (the `SeasonBrowseService` bullet), after "filtered to shows MAL classifies under *that* season via `start_season`", add:
  - a fetch that returns anime also removes the season's rows for anime MAL no longer files under it, using the same check
  - a 404, an empty answer, or a failed fetch removes nothing

  In the endpoint table row for `GET /api/year/{year}`, note that an anime cached under two of the year's seasons is returned once.
- [x] 4.3 In `docs/ISSUE_TRIAGE.md` (local, gitignored), following the pattern Phase 3 used:
  - replace the B3 entry with a short blockquote pointing to the resolved file
  - update the Summary table
  - mark item 7 of Phase 4 done
  - update the appendix row for "Anime appearing in several seasons" (`:341`) and the B3 half of the ISSUES #18 row (`:405`)
  - fix the early-list note at `:475`
- [x] 4.4 In `docs/ISSUE_TRIAGE_RESOLVED_2026-09-13.md`, add a `## B3.` section under the code-fixes heading. Name this change. Record:
  - only a response with anime prunes, and why an empty 200 doesn't
  - the year read shows an anime once while two seasons hold it
  - the remaining gaps: a paging shift can drop an entry until the next fetch, and an anime can be briefly in neither season (design Risks)
- [x] 4.5 Commit B3: groups 1–3 and `CODE_GUIDE.md`. Leave out this change's `openspec/changes/` directory, which lands archived with PF4, as earlier changes did. Use no Claude attribution lines.

## 5. PF4: the outer ceiling and the floor (design D5, D6)

- [x] 5.1 In `Services/Season/SeasonHorizon.cs`, add `public static (int Year, string Season) ResolveOuter((int Year, string Season) current, (int Year, string Season)? latestCachedSeason)`. Move `Resolve`'s first two steps (the forward window, then raising it to the latest cached season) into it, comments included. Doc comment:
  - this is the ceiling before today's `404` step-back
  - it's the upper end of the range the season and year endpoints accept (`SeasonRequestRange`)
  - the seasons between it and `Resolve`'s result were all fetched today, so accepting them costs no MAL request
- [x] 5.2 Make `Resolve` start from `SeasonCalendar.GetSeasonPointIndex` of `ResolveOuter(current, latestCachedSeason)`, keeping the step-back loop and its comment as they are.
- [x] 5.3 In `Services/Season/SeasonCalendar.cs`, add `public const int EarliestArchiveYear = 1917;`. Doc comment:
  - it's the first year in MyAnimeList's season archive (`myanimelist.net/anime/season/archive`, checked 2026-09-14) and the lower end of the range the API accepts
  - it's deliberately not the frontend's `EARLIEST_YEAR` (1989), which only sets how far back the season and year pages' arrows and dropdowns go, while detail-page and Recap links reach older seasons

## 6. PF4: the accepted range (design D7)

- [x] 6.1 Add `Services/Season/SeasonRequestRange.cs` with the record from design D7: `ContainsYear`, and `ContainsSeason` checking the year first. The overflow comment explains why the year check comes first. The doc comments say:
  - the caller has already checked the season name
  - both ends are accepted
  - the earliest end is always winter
- [x] 6.2 In `Services/Season/ISeasonBrowseService.cs`, add `Task<SeasonRequestRange> GetRequestRangeAsync(CancellationToken ct = default);`. Doc comment:
  - a repository-only read that never calls MAL
  - winter `SeasonCalendar.EarliestArchiveYear` to `SeasonHorizon.ResolveOuter`
  - worked out again on every call and never cached, so it moves forward with the calendar and the cache
  - it's for input checks, while `GetBoundsAsync` is for navigation
- [x] 6.3 In `Services/Season/SeasonBrowseService.cs`:
  - move `GetBoundsAsync`'s first half (`now`, `todayLocalDate`, `current`, `candidatePoints`, `GetHorizonInputsAsync`) into `private async Task<(int Year, string Season) Current, DateOnly TodayLocalDate, SeasonHorizonInputs Inputs)> LoadHorizonAsync(CancellationToken ct)`, keeping the candidate-points comment
  - have `GetBoundsAsync` call it, leaving its result unchanged
  - implement `GetRequestRangeAsync` with it and `SeasonHorizon.ResolveOuter`

## 7. PF4: the controllers (design D7)

- [x] 7.1 In `Controllers/SeasonController.cs` `GetPage` and `Refresh`, after the existing season-name check, call `await seasonBrowseService.GetRequestRangeAsync(ct)`. When `!range.ContainsSeason(year, season)`, return `BadRequest(new { error = $"{season} {year} is outside the seasons MyAnimeList lists (winter {range.EarliestYear} to {range.LatestSeason} {range.LatestYear})." })`. Extend both doc comments: a season outside that range is refused before any cache read, MAL request or fetch-log write.
- [x] 7.2 In `Controllers/YearController.cs` `GetPage` and `Refresh`, do the same with `ContainsYear(year)` and `error = $"{year} is outside the years MyAnimeList lists ({range.EarliestYear} to {range.LatestYear})."`. Change `GetPage`'s "Parameter handling mirrors SeasonController.GetPage exactly" to include the range check. Keep `Refresh`'s "no year-level bounds endpoint … do not add one" note, and add that the accepted range comes from `GetRequestRangeAsync`, not `GET /api/season/bounds`.

## 8. PF4: tests (design D5, D7)

- [x] 8.1 In `Services/Season/SeasonHorizonTests.cs`, add `ResolveOuter` tests. Leave the existing `Resolve` tests unchanged, and they must still pass.
  - Current summer 2026 with nothing cached gives winter 2027.
  - A cached fall 2027 gives fall 2027.
  - A cached spring 2026 (below current+2) still gives winter 2027.
  - Current fall 2026 gives spring 2027. That's the range moving with the calendar.
  - With a `notListedToday` that's true for winter 2027, `ResolveOuter` still gives winter 2027 while `Resolve` gives fall 2026.
- [x] 8.2 Add `Services/Season/SeasonRequestRangeTests.cs` over `new SeasonRequestRange(1917, 2027, "winter")`:
  - **`ContainsSeason` true:** (1917, winter), (2027, winter), (2026, fall), (1988, summer)
  - **`ContainsSeason` false:** (1916, fall), (2027, spring), (2028, winter), (9999, winter), (1800, winter), (0, winter), (-1, fall), (int.MaxValue, winter), (1073743741, winter)
  - **`ContainsYear` true:** 1917, 2027
  - **`ContainsYear` false:** 1916, 2028, int.MaxValue, 1073743741

  Add one more case: a range ending at fall accepts every season of its last year.
- [x] 8.3 Add `Controllers/SeasonControllerTests.cs`, modelled on `YearControllerTests`, with a `RecordingSeasonBrowseService` that returns a fixed `SeasonRequestRange(1917, 2027, "winter")` and records `GetPageAsync`, `RefreshAsync` and `GetRequestRangeAsync` calls. The other members throw.
  - **Refused**, a `[Theory]` over (9999, winter), (1800, winter), (1916, fall) and (2027, spring), for both `GetPage` and `Refresh`: the result is `BadRequestObjectResult`, and no page or refresh call is recorded.
  - **Accepted:** (2027, winter), (1917, winter) and (1988, summer) return `OkObjectResult`, with the call passed through.
  - **Unknown name first:** `autumn` with year 9999 returns the unknown-season `BadRequestObjectResult`, and `GetRequestRangeAsync` is never called.
- [x] 8.4 In `Controllers/YearControllerTests.cs`:
  - give the fake a `GetRequestRangeAsync` returning `SeasonRequestRange(1917, 2027, "winter")` (the existing 2020 tests stay valid)
  - add a refused `[Theory]` over 2028, 9999, 1916 and 1800 for both actions: `BadRequestObjectResult`, and no page or refresh request recorded
  - add accepted cases for 2027 and 1917 on both actions
  - update the class comment
- [x] 8.5 In `Services/Season/SeasonBrowseServiceTests.cs`, add `GetRequestRangeAsync` tests anchored to today like `SeasonWholeYearsAgo`, with `current = SeasonCalendar.GetSeasonFor(DateOnly.FromDateTime(DateTime.UtcNow))`:
  - **Nothing cached:** `EarliestYear == 1917`, and `(LatestYear, LatestSeason) == SeasonCalendar.Shift(current.Year, current.Season, 2)`.
  - **Seeded current+5 listing row:** the range ends at current+5.
  - **Current+2 stamped `DateTimeOffset.UtcNow` with no listing rows** (today's 404): the range still ends at current+2, and `GetBoundsAsync` returns current+1. `RefreshAsync` of current+2 then returns `Skipped`, with `FullSeasonCallCount == 0`.

## 9. PF4: build, test, docs and archive

- [x] 9.1 Copy `backend/` to `/private/tmp/bm-build/` again, then run `dotnet build` and `dotnet test` in `mcr.microsoft.com/dotnet/sdk:10.0`. Everything must pass.
- [x] 9.2 Grep `backend/` (excluding `bin/` and `obj/`) for `ISeasonBrowseService` implementations. Confirm each fake has `GetRequestRangeAsync` and nothing else implements the interface.
- [x] 9.3 In `CODE_GUIDE.md`:
  - **The four season and year endpoint rows:** add "`400` outside winter 1917 … the outer ceiling (`SeasonRequestRange`)". Also fix the refresh row's stale "if not already fetched today … `{refreshed: bool}`" wording to the age-tiered interval and the four-outcome result, since the row is being edited anyway.
  - **The `Services/Season/` notes:** add a bullet for `SeasonHorizon` (`Resolve` for navigation, `ResolveOuter` for input checks) and one for `SeasonRequestRange`.
- [x] 9.4 Run `openspec validate fix-season-pruning-and-bound-clamp --strict`, and fix anything it reports.
- [x] 9.5 In `docs/ISSUE_TRIAGE.md` (local, gitignored):
  - replace the PF4 entry with a short blockquote pointing to the resolved file
  - update the Summary table's "Partly fixed" count and the resolved row
  - mark Phase 4 of the Fix order done
  - update the appendix rows for ISSUES #18 (`:405`), SECURITY_REVIEW §6 (`:430`) and Recommended action 4 (`:443`, the year clamp part only, since S3 and S4 stay open)
- [x] 9.6 In `docs/ISSUE_TRIAGE_RESOLVED_2026-09-13.md`, add a `## PF4.` section. Name this change. Record:
  - the 1917 floor, and why not 1989 (detail-page and Recap links)
  - the outer ceiling, and why not `GetBoundsAsync`'s ceiling (the re-read after a same-day 404)
  - the accepted leftovers: a refused URL says "it'll be retried", and finding out that MAL has widened its window is no longer possible
- [x] 9.7 Archive the change (`/opsx:archive`), which writes the `season-browser` and `year-browser` deltas into `openspec/specs/`. Check that the three modified requirements and the added one read correctly in the main specs.
- [x] 9.8 Commit PF4: groups 5–8, `CODE_GUIDE.md`, the archived change and the updated main specs. Use no Claude attribution lines.
