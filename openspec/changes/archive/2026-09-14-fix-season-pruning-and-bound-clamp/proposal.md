## Why

This change fixes two items from `docs/ISSUE_TRIAGE.md`, Phase 4 ("Season browsing correctness") of its fix order.

- **B3.** A season's cached listing only ever grows. When MAL moves a show to another season, it stays on the old season page for good, and it shows up on the new one as well. If both seasons are in the same year, the Year page lists it twice.
- **PF4.** The season and year endpoints accept any integer year. Typing `/season?year=9999` costs one MAL request a day for each made-up season and leaves a fetch-log row behind for good. `SECURITY_REVIEW` §6 and its "Recommended action 4" name the same problem.

Both are in the season browser. B3 breaks a requirement the specs already state ("Each anime SHALL appear in exactly one season"). PF4 is small, and it's cheaper to do now, while the season code is already open, than to fold it into the Phase 6 security pass.

**Verified against the code** (2026-09-14, at `5c60ab8`). Paths without a prefix are under `backend/AnimeTracker.Api/`.

- **B3: listings are never pruned.**
  - `Services/Season/SeasonBrowseService.cs:225-247` (`FetchAndCacheAsync`) loads the season's existing listing ids, then adds a row for each returned anime that isn't cached yet. Nothing ever removes a row.
  - The `start_season` check at `:241-244` skips an anime MAL files under a different season. It only gates additions. A row that an earlier fetch added stays.
  - The listing key is `(Year, Season, AnimeId)` (`Data/AnimeTrackerDbContext.cs:115`), so one anime can have a row in two seasons. The year read (`Data/Repositories/SeasonRepository.cs:76-122`) reads rows, not anime, so it returns both.
  - A 404 (`:167-176`) and a failed fetch never reach the insertion code. `MalClient.GetFullSeasonAsync` (`Services/Mal/MalClient.cs:72-94`) is all-or-nothing. Page 0 tolerates a 404, and every later page goes through `GetSeasonAsync`, which throws on any error. So a failure partway through paging never produces a partial list. That's the property pruning depends on.
  - The request sends `nsfw=true` (`MalClient.cs:98`), so a hentai title that MAL still files under the season is returned and won't be pruned.
  - A 200 with no entries comes back as an empty list, not `null`, and today it's reported as `Fetched`. `Services/Season/SeasonBrowseDto.cs:6-12` treats that the same as "not listed".
  - Specs: `season-browser/spec.md:7` ("Each anime SHALL appear in exactly one season"), `:148` ("A season marked as unlisted SHALL keep whatever listing it already had cached"), and `year-browser/spec.md:13,19-21` ("No duplicates across the year").
- **PF4: no bound on year or season.**
  - `Controllers/SeasonController.cs:17-29` (`GetPage`) and `:38-46` (`Refresh`) check only the season name. `Controllers/YearController.cs:16-24` (`GetPage`) and `:34-39` (`Refresh`) check nothing.
  - `Services/Season/SeasonRefreshCadence.cs` puts a season that hasn't started (such as 9999) in the one-day tier, so it's re-requested daily.
  - **Below 1989 is reachable through the app, not only by typing a URL.** `EARLIEST_YEAR = 1989` (`frontend/src/pages/SeasonPage.tsx:46`) only sets how far back the arrows and dropdowns go. The anime detail page links to the anime's own MAL season (`frontend/src/pages/AnimeDetailPage.tsx:685`), and Recap links to its season or year page (`RecapPage.tsx:492,507`, where Recap's own floor is 1960). Both pages already handle a year older than `EARLIEST_YEAR` (`SeasonPage.tsx:212`, `YearPage.tsx:178`). A 1989 floor on the API would break the "Summer 1988" link on Akira's detail page. MAL's season archive starts at winter 1917 (checked on myanimelist.net on 2026-09-14), so that's the floor.
  - **Checking against `GetBoundsAsync`'s ceiling would break a normal visit.** `SeasonHorizon.Resolve` (`Services/Season/SeasonHorizon.cs:40-41`) steps the ceiling back past any season MAL answered 404 for today. The page reads the season again after a `notListed` refresh (`SeasonPage.tsx:313-316`; `YearPage.tsx:267` for years). So when you step to the ceiling season and MAL 404s it, that second read would be refused, and the page would say "couldn't be loaded" instead of "MyAnimeList hasn't listed this season yet". The check uses the ceiling *before* that step-back. Every season in the gap was already fetched today, so allowing it costs no MAL request.

## What Changes

- **B3: a successful fetch prunes the season's listing.** After a `Fetched` fetch adds its new rows, it removes this season's rows for anime that MAL no longer files under it: not returned at all, or returned with a `start_season` for a different season.
  - One membership check decides both adding and pruning, so the two can't disagree. An anime with no `start_season` is still trusted to the season that returned it.
  - An empty response (a 200 with no entries) prunes nothing. A 404 (`NotListed`), a failed fetch, and a skipped refresh prune nothing either.
  - The removals are saved in the same `SaveChangesAsync` as the additions and the fetch-log stamp.
  - The year read returns each anime once, even while two seasons of the year both still hold a row for it. That can happen between fetching the new season and re-fetching the old one. Sort positions stay dense.
  - `RefreshYearAsync` needs no change. It refreshes each season in turn, and each one prunes itself.
  - Unchanged: lean writes, `AiredFrom`, update detection, the fetch-log stamp, the refresh cadence and single-flight.
- **PF4: the API refuses a season or year outside what MAL could list.**
  - **The accepted range** runs from winter 1917 (MAL's first archive season) to the *outer ceiling*. The outer ceiling is the current season plus MAL's two-season window, raised to the latest season with a cached listing. That's the navigable ceiling without today's 404 step-back.
  - **Recomputed on every request.** Nothing is cached, so the range moves forward with the calendar.
  - `SeasonController.GetPage` and `.Refresh` return 400 with the existing `{ error }` shape for a season outside the range. The name check still runs first. Both ends of the range are accepted.
  - `YearController.GetPage` and `.Refresh` return 400 for a year below 1917 or past the outer ceiling's year.
  - A refused request makes no cache read, no MAL request and no fetch-log write.
  - `SeasonHorizon` splits out the outer ceiling so `Resolve` and the new check share it. `ISeasonBrowseService` gains a method that returns the accepted range.
  - `GET /api/season/bounds`, `SeasonBoundsDto` and the navigable ceiling the UI uses are unchanged.
  - **No frontend change.** `EARLIEST_YEAR` stays as the navigation floor, which is a different idea from the API floor, so it's not sent over the bounds endpoint. A URL outside the range shows the page's existing "couldn't be loaded" state.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `season-browser`:
  - **Modified:** "Full season listing with live-then-cached fetch". "Exactly one season" becomes a steady-state rule. A successful re-fetch removes anime MAL no longer files under the season, and a fetch that returns nothing removes nothing. Adds scenarios for an anime MAL moves to another season, an anime still returned but filed elsewhere, and an empty response.
  - **Added:** "Requests outside the seasons MAL could list are refused". This defines the accepted range for the season and year endpoints: winter 1917 to the ceiling before today's 404 step-back. Anything outside it gets 400 before any cache read, MAL request or fetch-log write.
  - **Modified:** "Season selection". The "A URL past the horizon still renders" scenario is split. A URL past the ceiling but inside the accepted range renders as before. A URL outside the range makes no MAL request and shows the could-not-be-loaded state. Old seasons that the anime detail and Recap pages link to stay reachable.
  - "MAL's forward season horizon" needs no change. Its "keeps whatever listing it already had cached" rule is still true, because only a fetch that returns anime prunes.
- `year-browser`:
  - **Modified:** "Year selection". Adds a cross-reference: the API itself refuses a year outside the range the UI can't navigate past. The "A URL past the ceiling still renders" scenario is split the same way. The rule that a URL-addressed year is rendered as asked, never rewritten, stays.
  - "A year is its four seasons combined" and its "No duplicates across the year" scenario need no change. They already state the rule, and this change makes it actually hold.

## Impact

- **Backend** (under `backend/AnimeTracker.Api/`):
  - `Services/Season/SeasonBrowseService.cs`: pruning in `FetchAndCacheAsync`, the shared membership check, a shared horizon-loading helper, and the new range method
  - `Data/Repositories/SeasonRepository.cs`: the listing read returns each anime once
  - `Services/Season/SeasonHorizon.cs`: the outer ceiling split out of `Resolve`
  - `Services/Season/SeasonCalendar.cs`: the earliest-year constant
  - new `Services/Season/SeasonRequestRange.cs`
  - `Services/Season/ISeasonBrowseService.cs`
  - `Controllers/SeasonController.cs` and `Controllers/YearController.cs`
- **Backend tests** (under `backend/AnimeTracker.Api.Tests/`):
  - **Extended:** `Services/Season/SeasonBrowseServiceTests.cs`, `Data/Repositories/SeasonRepositoryTests.cs`, `Services/Season/SeasonHorizonTests.cs` and `Services/Mal/MalClientTests.cs` (a failure partway through a season read throws)
  - **New:** `Services/Season/SeasonRequestRangeTests.cs` and `Controllers/SeasonControllerTests.cs`
  - **Updated:** `Controllers/YearControllerTests.cs`, for the fake's new method and the range checks
- **API:** the four season and year endpoints can now return 400 for an out-of-range year or season. The bounds endpoint is unchanged. No database or migration change.
- **Frontend:** none.
- **Docs:**
  - `CODE_GUIDE.md`: the season browser notes
  - `docs/ISSUE_TRIAGE.md` and `docs/ISSUE_TRIAGE_RESOLVED_2026-09-13.md` (local, gitignored): B3 and PF4 move to resolved
  - the triage doc's appendix rows for `SECURITY_REVIEW` §6 and Recommended action 4 point at this change; `docs/SECURITY_REVIEW.md` itself isn't edited
- **Commits:** two.
  1. B3: the pruning, the year read returning each anime once, and their tests.
  2. PF4: the accepted range, the controller checks and their tests, then archiving this change, which writes both capabilities' spec deltas.
- **Out of scope:**
  - the frontend's navigation guards, `EARLIEST_YEAR` and the page's empty-state wording
  - `SeasonRefreshCadence`, MAL request and paging behaviour, and the season-name check
  - S3 and S4 (framing headers, non-root containers) and any other security item
  - finding out that MAL has opened a season past the two-season window. Only a typed URL ever did that, and the API now refuses it. See design Risks.
