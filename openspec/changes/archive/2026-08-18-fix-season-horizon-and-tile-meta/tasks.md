## 1. MAL client: a 404 season is an answer, not an error

- [x] 1.1 Change `IMalClient.GetFullSeasonAsync` to return `Task<List<MalAnimeListEdge>?>`, documenting `null` as "MAL has no listing for this season (404)" — the only status treated as data rather than failure.
- [x] 1.2 In `MalClient`, add a 404-tolerant read for the season endpoint in the same shape `DeleteMyListStatusAsync` already uses (`MalClient.cs:107`): a `NotFound` first page returns `null`, every other non-success status still goes through `EnsureSuccessStatusCode`. Keep `GetSeasonAsync` (the single-page call) unchanged — only the paging wrapper needs the tolerance, and a 404 on a *later* page is a real error, not an unopened season.
- [x] 1.3 Update the `GET anime/season/{y}/{s}` row of `CODE_GUIDE.md` §1 to note the 404 contract.

## 2. Season repository: "does this season have a cached listing at all"

- [x] 2.1 Add `Task<bool> HasListingAsync(int year, string season, CancellationToken ct)` to `ISeasonRepository` and implement it in `SeasonRepository` as an unfiltered `AnyAsync` over `SeasonAnimeListings`. Comment why `GetPageAsync`'s `TotalCount` cannot serve here (it is post-filter, so a type filter would make a listed season look unlisted).
- [x] 2.2 Add a repository read for the horizon inputs: for a given set of `(year, season)` points, return `(LastFetchedAt, HasListings)` per point, plus the latest `(Year, Season)` that has any `SeasonAnimeListing` row. One round trip, no MAL call.

## 3. Season calendar and the horizon resolver

- [x] 3.1 Extend `SeasonCalendar` with season-point ordering: an index (`year * 4 + seasonIndex`) and its inverse, plus `Shift(year, season, delta)`. Keep the existing `GetSeasonFor`/`GetSeasonIndex` API intact.
- [x] 3.2 Add `SeasonHorizon.Resolve(current, latestCachedSeason, notListedToday)` as a pure function implementing design.md decision 3: start at `current + 2`, raise to `latestCachedSeason` if later, then step back while the candidate is above `current` and was answered 404 on the current local date. Document each clause with the reason it exists (MAL's published window / a wider window stays reachable / today's observed horizon), and state explicitly that a 404 mark constrains the ceiling only for the local day it was made, so the ceiling re-probes daily and can never deadlock.
- [x] 3.3 Name the forward-window constant (`FutureSeasonWindow = 2`) with a comment recording the 2026-08-18 probe from design.md's Context that established it.

## 4. Season browse service

- [x] 4.1 Add `SeasonRefreshOutcome` (`Fetched`, `NotListed`, `Skipped`, `Failed`) and change `SeasonRefreshResultDto` to carry it in place of `Refreshed`. Serialize as the camelCase strings the frontend reads.
- [x] 4.2 In `RefreshAsync`, return `Skipped` for the already-fetched-today path, `Failed` from the `catch`, and let `FetchAndCacheAsync` report `Fetched` vs `NotListed`.
- [x] 4.3 In `FetchAndCacheAsync`, handle a `null` edge list: add no listing rows, still write/refresh the `SeasonFetchLog` stamp, and return `NotListed`. Comment that the stamp is what puts an unopened season under the once-per-day rule and what lets the page leave its loading state.
- [x] 4.4 Add `HasListing` to `SeasonPageDto` and populate it from `HasListingAsync` in `GetPageAsync`. Extend the DTO's existing comment (which already explains `LastFetchedAt`'s null meaning) to spell out the three-way empty state the pair encodes.
- [x] 4.5 Add `SeasonBoundsDto(int LatestYear, string LatestSeason)` and `GetBoundsAsync` to `ISeasonBrowseService`/`SeasonBrowseService`: resolve "now" through `IBroadcastLocalTimeConverter` (same local-date source the daily rule uses), feed task 2.2's read into `SeasonHorizon.Resolve`, return the ceiling. Never calls MAL.

## 5. Season controller

- [x] 5.1 Add `GET /api/season/bounds` returning `SeasonBoundsDto`. Confirm it does not collide with `api/season/{year:int}/{season}` — the `int` route constraint keeps `bounds` out of that template — and note that in the action's doc comment.
- [x] 5.2 Update the refresh action's doc comment for the outcome enum, replacing the "Triggers a background refresh … subject to the once-per-local-day rule" wording with one that also covers the unopened-season answer.

## 6. Frontend API layer

- [x] 6.1 In `api/types.ts`: add `hasListing: boolean` to `SeasonPageDto`, replace `SeasonRefreshResultDto.refreshed` with `outcome: 'fetched' | 'notListed' | 'skipped' | 'failed'`, and add `SeasonBoundsDto`.
- [x] 6.2 In `api/client.ts`: add `getSeasonBounds()`.

## 7. Season page: the horizon

- [x] 7.1 Hold the ceiling in `SeasonPage` state seeded with the client-computed `shiftSeason(current, +2)`, replaced when `getSeasonBounds()` resolves (fetch once per mount, ignore a failure and keep the default). Comment why the seed matches the server default: nothing is disabled while the request is in flight and the agreeing case shows no transition.
- [x] 7.2 Disable the next-season button at or past the ceiling. Disable the previous-season button at or before `EARLIEST_YEAR` (winter) — the year quick-jump dropdown's own lower bound — so the arrow can't desync from what the dropdown offers. Both buttons get a visible disabled (greyed, `cursor: not-allowed`) style that also overrides the hover state, via `:hover:not(:disabled)` on the existing hover rule plus a new `:disabled` rule.
- [x] 7.3 Cut `yearOptions` at `max(ceilingYear, viewedYear)` — keeping the viewed year so a URL-addressed season past the horizon still has its own year in the `<select>` — and cut the season options to the ceiling's season when the selected year is the ceiling year, always keeping the currently-selected season in the list.
- [x] 7.4 Clamp the season when a year change would land past the ceiling (selecting the ceiling year while a later season is selected moves the season back to the ceiling's).
- [x] 7.5 Do not rewrite a URL that addresses a season past the ceiling — render it as asked, with forward movement blocked.

## 8. Season page: terminal states

- [x] 8.1 Track the refresh outcome per season alongside the existing `refreshing` flag, resetting it when the season changes, so one season's outcome can never decide another season's render.
- [x] 8.2 Re-read the page on `fetched`/`notListed`; on `skipped` only when nothing is cached client-side (the cross-tab race); never on `failed`. Replace the current `if (cancelled || !result.refreshed) return undefined` guard (`SeasonPage.tsx:261`).
- [x] 8.3 Implement design.md decision 5's ordered matrix in the render: grid → "No anime match the current filters." → "MyAnimeList hasn't listed this season yet." → "Loading…" → "This season couldn't be loaded — it'll be retried next time you open it." Retire the `neverCached` expression in favour of it, and confirm no combination of `items`/`lastFetchedAt`/`hasListing`/outcome renders a blank page.
- [x] 8.4 Style the two new messages with the existing `season-page__empty`/`season-page__loading` treatment; add a CSS rule only if the retry message needs to read differently from a plain empty state.

## 9. Series More-section tile

- [x] 9.1 ~~Remove `mediaTypeLabel(entry.mediaType)` from `SeriesExtraTile`'s meta line~~ — reverted per product feedback after seeing the type-less tile live: the media type stays on the tile (`mediaTypeLabel · year · N ep`, unchanged from before this change). The wrap is fixed by 9.2 alone.
- [x] 9.2 Give `.series-extra-tile__meta` and `.series-extra-tile__aired` the `white-space: nowrap; overflow: hidden; text-overflow: ellipsis` treatment `.series-timeline__card-meta-line` already uses, with a comment pointing at that precedent.

## 10. Tests

- [x] 10.1 Add `AnimeTracker.Api.Tests/Services/Season/SeasonHorizonTests.cs`: default ceiling is current + 2; ceiling extends to a cached season past the window; ceiling retreats past a season 404'd today; a 404 dated to an earlier local day does not constrain it; the ceiling never falls below the current season; a run of consecutive 404s retreats past all of them.
- [x] 10.2 Add `SeasonCalendarTests` for `Shift` across year boundaries in both directions and for the season-point index round-trip.
- [x] 10.3 Add `SeasonBrowseServiceTests` covering the refresh outcomes: a `null` edge list writes the fetch log, adds no listings and reports `NotListed`; a same-day revisit reports `Skipped` with no MAL call; a throwing client reports `Failed` and writes no fetch log.
- [x] 10.4 Compile-check the backend and run the test project through the `mcr.microsoft.com/dotnet/sdk:10.0` image (rsync the source to `/private/tmp/...` first — Docker Desktop cannot bind-mount `~/Documents`).

## 11. Verify against the live app

- [x] 11.1 Build and run the stack (`docker compose build backend && docker compose up -d`; frontend via `nvm use 22 && npm run build`, or `npm run dev` for the dev server). Confirm `npm run lint` and `tsc -b` are clean.
- [x] 11.2 Open the Season page on the current season and step forward. Confirm you can reach current + 2 (winter 2027 at time of writing), that both of those seasons list anime, and that the next arrow is disabled at the ceiling.
- [x] 11.3 Confirm the quick-jump dropdowns offer nothing past the ceiling, and that selecting the ceiling year from a later season clamps the season back.
- [x] 11.4 Address a season past the horizon directly by URL (`/season?year=2027&season=spring`). Confirm it renders "MyAnimeList hasn't listed this season yet." within one fetch instead of spinning, that the next arrow stays disabled, and that a reload the same day makes no second MAL request (check the backend log for the season fetch).
- [x] 11.5 Confirm a normal cached season is unchanged: cards render immediately, the updating indicator behaves as before, and sort/filter changes still cause no MAL fetch.
- [x] 11.6 Confirm the filter-empty state still reads as a filter problem: pick a season, then select a type filter no anime in it match, and check the message is "No anime match the current filters." rather than the not-listed wording.
- [x] 11.7 Open the Jujutsu Kaisen series page and confirm the *Jujutsu Kaisen Season 2 Recaps* tile's meta is one line, that its chips and footer line up with the other tiles in its row, and that the "TV special" heading above the group still names the type. Check one more group (Movie or OVA) at a narrow window width for the same.
