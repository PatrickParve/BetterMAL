## Why

Two surfaces do more work than they need to, in opposite directions. The profile's **Top series** strip ranks by my score with a tie-break — "the average earned over more entries wins" — that the Series page has already replaced with something better: where two franchises share an average, the one whose entries sit nearer the top of my rankings comes first. The Series page computes and ships that figure today; the profile does not read it, so the same two franchises are ordered one way on one page and another way on the other.

And every visit to a **season or year** re-fetches from MyAnimeList once a local day, whatever the season's age. Fall 2011 finished airing fourteen years ago and its listing has not moved since; re-asking MAL for it every day someone browses that year is a request spent on a page that cannot change. The cost falls hardest on the Year page, which walks four seasons per visit.

## What Changes

- **Top series ranked by my score uses the Series page's tie-break chain.** Where two series' main-line my-averages compare equal, they are ordered by the average position their main-line entries hold in my rankings (nearer the top first, an entry with no rank left out of the mean entirely, a series with no ranked entry placed last among the tied), then by main-line episodes aired so far (descending), then by display title — the identical chain, in the identical order, that `series-browser`'s **My average** sort already specifies. **BREAKING** for the `profile-stats` requirement that ties be broken by scored main-line count: that rule now applies to the MAL-score basis only.
- **The Top series read carries the two figures that chain needs.** `mainLineAverageRank` and `mainLineAiredEpisodes`, computed exactly as the series-list read computes them, so the two surfaces sort on the same numbers rather than on two derivations of them.
- **A season is re-fetched on a schedule set by its own age**, measured from the first day of its quarter: under a year old, at most once per local day, exactly as today; one to two years, at most once every 3 days; two to five years, once every 5 days; five years and older, once every 10 days. A season never fetched is still fetched on the first visit, whatever its age; a failed fetch still does not consume the interval; a future season is under a year old by this measure and so keeps re-probing daily, which is what keeps the forward-horizon ceiling rolling forward.
- **A year refreshes each of its four seasons on that season's own interval.** Because a year straddles the age boundaries — in September 2026, fall 2025 is still under a year old while winter 2025 is over it — a year visit may refresh some of its seasons and skip others. No new mechanism: each season already goes through the same per-season gate, which now asks a different question.
- The refresh remains visit-triggered, single-flight per season, and invisible: no timer, no schedule, no user-facing refresh control, and no change to what a refresh outcome (`fetched` / `notListed` / `skipped` / `failed`) means to the page. `skipped` widens from "already fetched today" to "fetched recently enough for its age".

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `profile-stats`: the Top series my-score ranking gains the Series page's tie-break chain, and the scored-count tie-break narrows to the MAL-score basis.
- `season-browser`: the once-per-local-day fetch rule becomes an age-tiered minimum interval between fetches.
- `year-browser`: a year's four seasons are each subject to their own interval, so one year visit may refresh a subset of them.

## Impact

- `backend/AnimeTracker.Api/Services/Season/SeasonCalendar.cs` — a `SeasonStart(year, season)` returning the first day of the quarter, the date every age is measured from.
- New `backend/AnimeTracker.Api/Services/Season/SeasonRefreshCadence.cs` — the age → interval table and the "is this stamp still fresh" predicate, pure and static like `SeasonCalendar`/`SeasonHorizon` beside it.
- `backend/AnimeTracker.Api/Services/Season/SeasonBrowseService.cs` — `RefreshAsync`'s same-local-day check becomes the cadence check. `RefreshYearAsync`, `GetBoundsAsync`, `FetchAndCacheAsync`, and the outcome fold are untouched.
- `backend/AnimeTracker.Api/Services/Season/SeasonBrowseDto.cs` — `SeasonRefreshOutcome.Skipped`'s doc comment.
- New `backend/AnimeTracker.Api.Tests/Services/Season/SeasonRefreshCadenceTests.cs`; additions to `SeasonBrowseServiceTests.cs` and `SeasonCalendarTests.cs`.
- `backend/AnimeTracker.Api/Services/Series/SeriesRankingIndex.cs` — `EligibleSeries` takes the aired-episode map and the ranking snapshot the way `ListedSeries` already does, and `SeriesRankingResult` gains the two figures; the per-figure arithmetic is shared with `ListedSeries` rather than restated.
- `backend/AnimeTracker.Api/Services/Profile/ProfileDto.cs`, `ProfileService.cs` — two fields on `TopSeriesItemDto`; `GetTopSeriesSectionAsync` resolves the aired-episode map and the ranking snapshot and orders by the new chain; `ProfileService` gains `IAnimeRankingService`.
- `backend/AnimeTracker.Api.Tests/Services/Profile/*`, `Services/Series/SeriesRankingLookupTests.cs`, `SeriesListEligibilityTests.cs` — `EligibleSeries()` and `ProfileService` construction sites, plus tie-break and figure tests.
- `frontend/src/api/types.ts`, `frontend/src/pages/ProfilePage.tsx` — the two new fields and `rankTopSeries`'s my-score chain.
- `CODE_GUIDE.md` — the two places that state the once-per-local-day rule.
- No database migration, no new stored column, no API contract removal: the top-series response gains two fields and the season contract is unchanged in shape.
