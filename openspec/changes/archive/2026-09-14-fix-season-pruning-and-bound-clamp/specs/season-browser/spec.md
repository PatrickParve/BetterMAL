## ADDED Requirements

### Requirement: Requests outside the seasons MAL could list are refused
The season endpoints (`GET /api/season/{year}/{season}` and `POST /api/season/{year}/{season}/refresh`) and the year endpoints (`GET /api/year/{year}` and `POST /api/year/{year}/refresh`) SHALL refuse a request for a season or year outside the range MAL could list. The response SHALL be `400`, in the same `{ error }` shape as an unknown season name. It SHALL be sent before the cache is read, before anything is requested from MAL, and before any fetch record is written. A refused request SHALL NOT be moved to the nearest valid season, and SHALL NOT be served as an empty listing.

The range SHALL start at winter 1917, the first season in MyAnimeList's season archive. It SHALL end at the **outer ceiling**: the current season plus MAL's two-season forward window, raised to any later season that already has a cached listing. The outer ceiling is the navigable ceiling from "MAL's forward season horizon" before that ceiling is lowered past seasons MAL answered `404` for today. It SHALL NOT follow that lowering. A season MAL answered `404` for today has already been fetched today, so accepting it costs no MAL request. Refusing it would turn the page's own re-read after that `404` into an error. Both ends of the range SHALL be accepted.

A season SHALL be accepted when it falls, in season order, between winter 1917 and the outer ceiling. A season later than the ceiling's season SHALL be refused even when its year is the ceiling's year. A year SHALL be accepted when it is between 1917 and the outer ceiling's year, inclusive, since a year is addressable when any of its seasons is.

The range SHALL be computed again for every request, from the current local date and the cache, and SHALL NOT be stored. It therefore moves forward with the calendar and with newly cached seasons. An unknown season name SHALL still be refused as before, and that check SHALL come first.

The earliest year the Season and Year pages' arrows and dropdowns offer is a navigation choice, separate from this range, and this range does not change it. A season or year older than that choice that is linked from elsewhere in the app, such as an anime's detail page or a recap, SHALL still be readable and refreshable.

#### Scenario: A far-future season is refused
- **WHEN** `GET /api/season/9999/winter` is requested
- **THEN** the response is `400`, no request is made to MAL, and no fetch record is written

#### Scenario: A refresh of a far-future season is refused
- **WHEN** `POST /api/season/9999/winter/refresh` is requested
- **THEN** the response is `400`, no request is made to MAL, and no fetch record is written

#### Scenario: A season before MAL's archive is refused
- **WHEN** `GET /api/season/1800/winter` or `GET /api/season/1916/fall` is requested
- **THEN** the response is `400`

#### Scenario: MAL's first archive season is accepted
- **WHEN** `GET /api/season/1917/winter` is requested
- **THEN** the season's page is served

#### Scenario: An old season linked from an anime is accepted
- **WHEN** I follow an anime detail page's link to summer 1988, older than the earliest year the season quick-jump offers
- **THEN** the API serves and refreshes that season like any other

#### Scenario: The outer ceiling itself is accepted
- **WHEN** the outer ceiling is winter 2027 and winter 2027 is read or refreshed
- **THEN** the request is served

#### Scenario: A later season in the ceiling's year is refused
- **WHEN** the outer ceiling is winter 2027 and spring 2027 is read or refreshed
- **THEN** the response is `400`, even though 2027 is the ceiling's year

#### Scenario: A season MAL answered 404 for today is still accepted
- **WHEN** the current season is summer 2026, MAL answered `404` for winter 2027 earlier today (so the navigable ceiling is fall 2026), and winter 2027 is read and refreshed again
- **THEN** the read is served, reporting that MAL has no listing for it, and the refresh is skipped with no request made to MAL

#### Scenario: A cached season past the window is accepted
- **WHEN** a season past the current season plus two already has a cached listing
- **THEN** reads and refreshes of that season, and of the seasons before it, are accepted

#### Scenario: The range moves forward with the calendar
- **WHEN** a new season starts
- **THEN** the season two past it is accepted from then on, with no restart and nothing stored

#### Scenario: A year inside the range is accepted
- **WHEN** the outer ceiling is winter 2027 and `GET /api/year/2027` or `GET /api/year/1917` is requested
- **THEN** the year's page is served

#### Scenario: A year outside the range is refused
- **WHEN** the outer ceiling is winter 2027 and `GET /api/year/2028`, `POST /api/year/2028/refresh` or `GET /api/year/1916` is requested
- **THEN** the response is `400`, and for the refresh no request is made to MAL and no fetch record is written

#### Scenario: An unknown season name is still refused first
- **WHEN** `GET /api/season/9999/autumn` is requested
- **THEN** the response is the existing `400` for an unknown season name

## MODIFIED Requirements

### Requirement: Full season listing with live-then-cached fetch
The system SHALL show all anime whose MAL season classification (`start_season`) is the selected season (not just my list), fetching the season live from the API when it has never been fetched before and caching the results for subsequent visits. Season membership SHALL follow MAL's own `start_season` — the field MAL uses to build its per-season listings — which can differ from the calendar quarter the anime's start date falls in; the system SHALL NOT re-derive an anime's season from its start date. Each anime SHALL appear in exactly one season: the season MAL currently files it under. This SHALL hold for the cached listing as it stands after each successful fetch, not only when a row is first added. A long-running anime SHALL NOT appear in seasons after its premiere. The cached listing SHALL be the single source of truth read back to the page (no start-date re-filtering at read time).

Reading a season's page SHALL NOT block on a live fetch: the cached listing is served immediately, and any live fetch happens outside the read path (see "Cache-first read with visit-triggered background refresh"). Re-fetching a season that already has a cached listing SHALL add anime MAL now returns for that season and that are not yet cached, and SHALL update each returned anime's listing fields (title, picture, episode count, type, MAL score, popularity rank), so entries and scores missing or stale from an earlier fetch are corrected on the next refresh.

Re-fetching a season SHALL also remove from that season's cached listing every anime MAL no longer files under it. That is an anime MAL did not return for the season, or one it returned with a `start_season` naming a different season. Adding and removing SHALL use the same membership rule, so one answer from MAL can never add an anime and also remove it. An anime MAL returns with no `start_season` SHALL be kept, just as it is added.

Only a fetch that returns at least one anime SHALL remove anything. The cached listing SHALL be left as it is when MAL answers that it has no listing for the season (see "MAL's forward season horizon"), when a successful response lists no anime, when a fetch fails, and when a refresh is skipped as fresh enough. A fetch whose paging fails partway through SHALL count as a failed fetch, never as a shorter listing.

A read that combines several seasons SHALL show each anime once, even at a moment when two of those seasons both still hold it. That happens after MAL moves an anime, once the new season has been fetched but before the old one is fetched again.

#### Scenario: First visit to a season
- **WHEN** I open a season that has not been fetched before
- **THEN** the system fetches that season from the API, caches every anime MAL classifies under that season, and displays them

#### Scenario: Premiere season differs from the start-date quarter
- **WHEN** an anime's MAL `start_season` is the selected season but its start date falls in a different calendar quarter (e.g. an early-June start date that MAL files under Summer)
- **THEN** it still appears on the selected season, because membership follows MAL's classification rather than the start-date quarter

#### Scenario: Long-running anime shown only in its premiere season
- **WHEN** an anime started airing well before the selected season but is still airing during it
- **THEN** it appears only in its premiere season (its `start_season`) and is not listed in the selected later season

#### Scenario: Revisiting a cached, fully-past season
- **WHEN** I return to a season that has already fully aired and was previously cached
- **THEN** the cached listing renders immediately and a background refresh still runs, so the season's MAL scores and popularity ranks are brought up to date

#### Scenario: Refresh picks up anime missing from an earlier fetch
- **WHEN** a season is refreshed and MAL returns an anime for that season that is not in the cached listing
- **THEN** that anime is added to the cached listing and appears on the page, without duplicating anime already cached

#### Scenario: Refresh updates stale scores and ranks
- **WHEN** a season is refreshed and MAL now reports a different score or popularity rank for an anime already in the cached listing
- **THEN** the cached anime's score and rank are updated, so score and popularity sorts reflect current MAL values even for anime that are not in my list

#### Scenario: An anime MAL moves to another season leaves the old one
- **WHEN** an anime is cached under spring, MAL postpones it and now returns it only for summer, and summer is fetched and then spring is fetched again
- **THEN** after the spring fetch the anime is listed under summer only

#### Scenario: An anime still returned but filed elsewhere is removed
- **WHEN** a season is refreshed and MAL's response for it includes an already-cached anime whose `start_season` now names a different season
- **THEN** that anime is removed from this season's cached listing, the same way it would not have been added

#### Scenario: An anime with no start season is kept
- **WHEN** a season is refreshed and MAL returns an already-cached anime with no `start_season`
- **THEN** it stays in this season's cached listing

#### Scenario: A fetch that brings nothing back removes nothing
- **WHEN** a season with a cached listing is refreshed and MAL answers `404`, returns a successful response listing no anime, or fails, including partway through paging
- **THEN** every anime in the season's cached listing is still there

#### Scenario: A combined read never shows an anime twice
- **WHEN** an anime MAL moved from spring to summer of the same year is still cached under both, because spring has not been fetched again yet
- **THEN** a read of that year shows it once

### Requirement: Season selection
The system SHALL allow changing the selected season, and SHALL keep the selected season, sort, and filter state in the page's URL so it persists through back-navigation from an anime detail page. Opening the Season page from the navbar with no season specified SHALL default to the current season. The system SHALL provide a quick-jump control to select a specific year and season directly, in addition to stepping one season at a time.

Forward navigation SHALL stop at the navigable ceiling defined by "MAL's forward season horizon": at the ceiling the next-season control SHALL be disabled, and the quick-jump controls SHALL NOT offer any season beyond it — neither a year later than the ceiling's year, nor a season later than the ceiling's season within that year. Backward navigation SHALL be unaffected. A season addressed directly in the URL SHALL still be rendered as asked rather than silently rewritten, so an old link or a bookmark is never redirected; forward movement from there SHALL remain blocked.

The API itself refuses a season outside the range MAL could list (see "Requests outside the seasons MAL could list are refused"), so what these controls prevent reaching is also enforced server-side. A URL-addressed season past the navigable ceiling but inside that range SHALL render like any other season: its cached listing, or a report that MAL has not listed it. A URL-addressed season outside that range SHALL still not be rewritten. The API refuses it, so no request reaches MAL, and the page SHALL show the state it shows for a season that could not be loaded. A season linked from elsewhere in the app, such as an anime's detail page or a recap, SHALL render normally even when it is older than the earliest year the quick-jump offers.

The season header SHALL place the step navigation (previous/next arrows with the season label) in the horizontal center of the header, the year and season quick-jump dropdowns immediately to the right of that navigation, and the sort and filter controls after them — so no control group is pushed to the far edge of the header.

#### Scenario: Changing season
- **WHEN** I select a different season
- **THEN** the page shows that season's anime and the selection is reflected in the URL

#### Scenario: Selection persists through back-navigation
- **WHEN** I pick a season, open an anime, then press the browser Back button
- **THEN** I return to the season I had selected, not the current season

#### Scenario: Navbar defaults to current season
- **WHEN** I open the Season page from the navbar with no season in the URL
- **THEN** it defaults to the current season

#### Scenario: Quick-jump to a specific season
- **WHEN** I use the quick-jump control to choose a year and season
- **THEN** the page jumps directly to that season without stepping through intermediate seasons

#### Scenario: Stepping forward stops at the horizon
- **WHEN** I am viewing the furthest season MAL publishes
- **THEN** the next-season control is disabled and I cannot step past it, while the previous-season control still works

#### Scenario: Quick-jump does not offer seasons past the horizon
- **WHEN** I open the year and season quick-jump controls
- **THEN** they offer nothing later than the navigable ceiling — the year list ends at the ceiling's year, and within that year the season list ends at the ceiling's season

#### Scenario: Switching year clamps a season past the horizon
- **WHEN** I am viewing spring of some year and use the year quick-jump to select the ceiling's year, whose ceiling season is winter
- **THEN** the selected season clamps to winter of that year rather than landing on a season past the ceiling

#### Scenario: A URL past the horizon still renders
- **WHEN** I open a bookmarked link to a season beyond the navigable ceiling that is still inside the range the API accepts, such as a season MAL answered `404` for earlier today
- **THEN** that season is shown as asked, reporting that MyAnimeList has not listed it, and forward navigation from it remains blocked

#### Scenario: A URL outside the accepted range is not rewritten
- **WHEN** I open a link to a season the API refuses, such as winter 9999
- **THEN** the URL is left as it is, no request reaches MAL, and the page shows its could-not-be-loaded state

#### Scenario: A linked season older than the quick-jump range renders
- **WHEN** I follow an anime detail page's link to a season older than the earliest year the quick-jump offers
- **THEN** that season is shown and refreshed like any other, and its year is kept among the quick-jump's options

#### Scenario: Header control placement
- **WHEN** I view the season header
- **THEN** the arrows and season label sit centered in the header with the year/season quick-jump dropdowns directly to their right, rather than the quick-jump sitting alone at the far right edge
