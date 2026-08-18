## ADDED Requirements

### Requirement: MAL's forward season horizon
MyAnimeList publishes season listings only a bounded distance into the future and answers `404` for a season it has not opened yet; anime announced beyond that point carry no season classification at all and are unreachable through the season endpoint. The system SHALL treat that `404` as a fact about the season — MAL has no listing for it — and SHALL NOT treat it as a fetch failure.

A season MAL reports no listing for SHALL be recorded as fetched, with the same timestamp semantics as a successful fetch, so the once-per-local-day rule applies to it and the page leaves its loading state. The record SHALL be superseded by a later fetch that does return a listing, so a season MAL opens later stops being marked as unlisted without any manual intervention. A season marked as unlisted SHALL keep whatever listing it already had cached — the mark records MAL's answer, it does not delete anime.

The system SHALL expose the furthest season the user may navigate to (the navigable ceiling), computed as: the current season plus MAL's two-season forward window; raised to any later season that already has a cached listing, so a wider window, once observed, stays reachable; and lowered past any trailing season that MAL answered `404` for on the current local date, so the day's observed horizon is respected. The ceiling SHALL never fall below the current season. A `404` mark SHALL constrain the ceiling only on the local day it was made, so that the ceiling re-probes daily and rolls forward on its own as MAL opens each new season.

#### Scenario: MAL has no listing for a future season
- **WHEN** a background refresh runs for a season MAL has not opened yet and MAL answers `404`
- **THEN** the season is recorded as fetched with no listing, the page leaves its loading state, and no error is surfaced

#### Scenario: An unlisted season is not refetched the same day
- **WHEN** I open a season that MAL answered `404` for earlier on the current local day
- **THEN** no second MAL request is made for it

#### Scenario: A season MAL opens later stops being unlisted
- **WHEN** a season previously marked as having no MAL listing is refreshed on a later day and MAL now returns anime for it
- **THEN** those anime are cached, the season is no longer marked as unlisted, and it is navigable from then on

#### Scenario: Default ceiling spans MAL's forward window
- **WHEN** nothing is known about any future season
- **THEN** the navigable ceiling is the current season plus two

#### Scenario: Ceiling extends to a cached later season
- **WHEN** a season beyond the current season plus two already has a cached listing
- **THEN** the navigable ceiling extends to that season

#### Scenario: Ceiling retreats past a season MAL answered 404 for today
- **WHEN** the season at the ceiling was answered `404` by MAL earlier on the current local day
- **THEN** the ceiling becomes the season before it for the rest of that day

#### Scenario: Ceiling re-probes the next day
- **WHEN** a new local day begins after a season was marked as having no MAL listing
- **THEN** that season is navigable again and the next visit re-fetches it, so MAL opening it in the meantime is picked up

#### Scenario: Ceiling never falls below the current season
- **WHEN** MAL answers `404` for every season from the current season forward
- **THEN** the current season remains navigable

## MODIFIED Requirements

### Requirement: Cache-first read with visit-triggered background refresh
The system SHALL render the season page from the cached listing first and refresh that season from MAL in the background, updating the page in place once the refresh completes. Opening a season SHALL trigger a background refresh regardless of whether that season is past, current, or upcoming — refresh is driven by the user visiting a season, never by a timer or schedule, so that a deployment which is not running continuously never misses a refresh window.

A season SHALL be fetched at most once per local calendar day: if its last successful fetch falls on the current local date, revisiting it SHALL be served from cache with no MAL request, however many times it is opened. At most one refresh per season SHALL additionally be in flight at a time, so concurrent tabs collapse into a single fetch. A fetch that fails SHALL NOT count as the day's fetch — the next visit retries. MAL answering that it has no listing for the season is not a failure and SHALL count as the day's fetch (see "MAL's forward season horizon"). The refresh SHALL be triggered only by a change of the selected season, not by changing the sort or filter controls. There SHALL be no user-facing control that triggers a refresh. A failed refresh SHALL leave the cached listing and the displayed page intact and SHALL NOT be surfaced as a page error.

A refresh SHALL report which of four outcomes it had — anime were fetched, MAL has no listing for the season, the season was already fetched today, or the fetch failed — so the page can tell them apart rather than reading a single "did anything change" flag. The page SHALL leave its never-cached loading state once a refresh attempt has settled, whatever its outcome, so a season with nothing cached can never be left loading indefinitely.

A season with no anime to show SHALL say which of three situations it is in: MAL has no listing for it yet, MAL lists it but nothing matches the current filters, or its first fetch failed and will be retried on the next visit. The third of these states the failure of that first fetch only — it SHALL NOT be shown for a season that already has a cached listing, which continues to render its cached anime with no error.

#### Scenario: Cached season renders before the refresh completes
- **WHEN** I open a season that already has a cached listing
- **THEN** the cached anime are displayed immediately without waiting for any MAL request

#### Scenario: Past season refreshes on visit
- **WHEN** I open a fully-past season that was cached long ago
- **THEN** a background refresh runs for it just as it would for the current season, and the page updates once it completes

#### Scenario: Background refresh updates the page
- **WHEN** a background refresh finishes
- **THEN** the displayed results are re-read from the cache and updated in place, preserving my scroll position and the number of pages I had already loaded

#### Scenario: Refresh in progress is visible
- **WHEN** a background refresh is running for the season I am viewing
- **THEN** the page shows a passive updating indicator, so results changing underneath me are explained, and the indicator disappears when the refresh completes

#### Scenario: Empty cache waits for the first fetch
- **WHEN** I open a season that has never been cached
- **THEN** the page shows a loading state until the first fetch completes, rather than reporting that no anime were found

#### Scenario: Loading state ends when the first fetch finds no MAL listing
- **WHEN** I open a season that has never been cached and MAL reports it has no listing for that season
- **THEN** the loading state ends and the page says MyAnimeList has not listed that season yet, rather than continuing to load or claiming no anime were found

#### Scenario: Loading state ends when the first fetch fails
- **WHEN** I open a season that has never been cached and its first fetch fails
- **THEN** the loading state ends and the page says the season could not be loaded and will be retried, rather than continuing to load or claiming the season is empty

#### Scenario: A cached season with no matching filters
- **WHEN** I open a season that has a cached listing and my current filters exclude every anime in it
- **THEN** the page reports that no anime match, not that MAL has no listing for the season

#### Scenario: Revisiting a season already fetched today
- **WHEN** I open a season that was already fetched earlier on the current local day, whether moments ago or hours ago
- **THEN** no second MAL fetch is started and the page is served from the cache

#### Scenario: Revisiting a season the next day
- **WHEN** I open a season whose last successful fetch was on an earlier local date
- **THEN** a background refresh runs for it

#### Scenario: A failed fetch does not consume the day
- **WHEN** a refresh fails and I open that season again the same day
- **THEN** the refresh is retried, because only a successful fetch marks the season as fetched for that day

#### Scenario: Stepping quickly through seasons
- **WHEN** I step through several seasons in quick succession with the previous/next arrows
- **THEN** only the season I settle on is fetched, and the intermediate seasons I passed through are not fetched

#### Scenario: Concurrent visits share one refresh
- **WHEN** a second request for a season arrives while that season's refresh is already running
- **THEN** no additional MAL fetch is started for that season

#### Scenario: Changing sort or filter does not refetch
- **WHEN** I change the sort order or the "In my list" checkbox without changing the season
- **THEN** the results are re-read from the cache and no MAL fetch is started

#### Scenario: Failed refresh keeps the cached page
- **WHEN** a background refresh fails (MAL is unreachable or returns an error)
- **THEN** the page keeps showing the cached listing and the failure is not surfaced as a page error

### Requirement: Season selection
The system SHALL allow changing the selected season, and SHALL keep the selected season, sort, and filter state in the page's URL so it persists through back-navigation from an anime detail page. Opening the Season page from the navbar with no season specified SHALL default to the current season. The system SHALL provide a quick-jump control to select a specific year and season directly, in addition to stepping one season at a time.

Forward navigation SHALL stop at the navigable ceiling defined by "MAL's forward season horizon": at the ceiling the next-season control SHALL be disabled, and the quick-jump controls SHALL NOT offer any season beyond it — neither a year later than the ceiling's year, nor a season later than the ceiling's season within that year. Backward navigation SHALL be unaffected. A season addressed directly in the URL SHALL still be rendered as asked rather than silently rewritten, so an old link or a bookmark is never redirected; forward movement from there SHALL remain blocked.

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
- **WHEN** I open a bookmarked link to a season beyond the navigable ceiling
- **THEN** that season is shown as asked, reporting that MyAnimeList has not listed it, and forward navigation from it remains blocked

#### Scenario: Header control placement
- **WHEN** I view the season header
- **THEN** the arrows and season label sit centered in the header with the year/season quick-jump dropdowns directly to their right, rather than the quick-jump sitting alone at the far right edge
