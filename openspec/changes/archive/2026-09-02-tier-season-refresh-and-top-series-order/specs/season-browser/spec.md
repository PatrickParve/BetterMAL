## MODIFIED Requirements

### Requirement: Cache-first read with visit-triggered background refresh
The system SHALL render the season page from the cached listing first and refresh that season from MAL in the background, updating the page in place once the refresh completes. Opening a season SHALL trigger a background refresh regardless of whether that season is past, current, or upcoming — refresh is driven by the user visiting a season, never by a timer or schedule, so that a deployment which is not running continuously never misses a refresh window.

A season SHALL be fetched at most once per **minimum interval set by that season's own age**, since an old season's listing changes far less than a current one's and a request spent re-asking for it is a request wasted. The interval SHALL be measured from the season's **start** — the first day of its quarter, per MyAnimeList's fixed convention that winter begins in January, spring in April, summer in July, and fall in October — to the current local date, counted in whole elapsed years:

| Whole years since the season started | Minimum days between fetches |
|---|---|
| Under one — including a season that has not started yet | 1 |
| One | 3 |
| Two, three, or four | 5 |
| Five or more | 10 |

An interval of one day SHALL mean exactly what the rule meant before this tiering existed: a season whose last successful fetch falls on the current local date SHALL be served from cache with no MAL request, however many times it is opened. For a longer interval, a season SHALL be served from cache until the stated number of local days has elapsed since its last successful fetch.

A season that has **never** been fetched SHALL be fetched on first visit whatever its age — the interval SHALL be consulted only when there is a previous fetch to measure from, so no season is ever left uncached by this rule.

A season that has **not started yet** SHALL fall in the one-day tier. This is load-bearing rather than incidental: the forward horizon's ceiling re-probes daily (see "MAL's forward season horizon"), and a longer interval on a future season would freeze the ceiling for days at a time.

Age tiers SHALL be evaluated per season, not per visit, so two seasons opened in the same minute may be subject to different intervals.

At most one refresh per season SHALL additionally be in flight at a time, so concurrent tabs collapse into a single fetch. A fetch that fails SHALL NOT count as a fetch for interval purposes — the next visit retries, whatever the season's age. MAL answering that it has no listing for the season is not a failure and SHALL count as a fetch (see "MAL's forward season horizon"). The refresh SHALL be triggered only by a change of the selected season, not by changing the sort or filter controls. There SHALL be no user-facing control that triggers a refresh, and none that changes the interval or displays it. A failed refresh SHALL leave the cached listing and the displayed page intact and SHALL NOT be surfaced as a page error.

A refresh SHALL report which of four outcomes it had — anime were fetched, MAL has no listing for the season, the season was skipped as already fresh enough for its age, or the fetch failed — so the page can tell them apart rather than reading a single "did anything change" flag. The page SHALL leave its never-cached loading state once a refresh attempt has settled, whatever its outcome, so a season with nothing cached can never be left loading indefinitely.

A season with no anime to show SHALL say which of three situations it is in: MAL has no listing for it yet, MAL lists it but nothing matches the current filters, or its first fetch failed and will be retried on the next visit. The third of these states the failure of that first fetch only — it SHALL NOT be shown for a season that already has a cached listing, which continues to render its cached anime with no error.

#### Scenario: Cached season renders before the refresh completes
- **WHEN** I open a season that already has a cached listing
- **THEN** the cached anime are displayed immediately without waiting for any MAL request

#### Scenario: Past season refreshes on visit
- **WHEN** I open a fully-past season whose last fetch is older than its interval
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

#### Scenario: An old season with nothing cached still fetches on the first visit
- **WHEN** I open a season that started twelve years ago and has never been fetched
- **THEN** it is fetched immediately, because the ten-day interval applies only between fetches and there is no previous fetch to measure from

#### Scenario: Loading state ends when the first fetch finds no MAL listing
- **WHEN** I open a season that has never been cached and MAL reports it has no listing for that season
- **THEN** the loading state ends and the page says MyAnimeList has not listed that season yet, rather than continuing to load or claiming no anime were found

#### Scenario: Loading state ends when the first fetch fails
- **WHEN** I open a season that has never been cached and its first fetch fails
- **THEN** the loading state ends and the page says the season could not be loaded and will be retried, rather than continuing to load or claiming the season is empty

#### Scenario: A cached season with no matching filters
- **WHEN** I open a season that has a cached listing and my current filters exclude every anime in it
- **THEN** the page reports that no anime match, not that MAL has no listing for the season

#### Scenario: Revisiting a recent season already fetched today
- **WHEN** I open a season that started less than a year ago and was already fetched earlier on the current local day, whether moments ago or hours ago
- **THEN** no second MAL fetch is started and the page is served from the cache

#### Scenario: Revisiting a recent season the next day
- **WHEN** I open a season that started less than a year ago whose last successful fetch was on an earlier local date
- **THEN** a background refresh runs for it

#### Scenario: A season just over a year old waits three days
- **WHEN** I open a season that started thirteen months ago and was fetched two local days ago
- **THEN** no MAL fetch is started; and when I open it again on the third day after that fetch, a refresh runs

#### Scenario: A three-year-old season waits five days
- **WHEN** I open a season that started three years ago and was fetched four local days ago
- **THEN** no MAL fetch is started; and when I open it again on the fifth day after that fetch, a refresh runs

#### Scenario: A decade-old season waits ten days
- **WHEN** I open a season that started eight years ago and was fetched nine local days ago
- **THEN** no MAL fetch is started; and when I open it again on the tenth day after that fetch, a refresh runs

#### Scenario: A future season is still re-probed daily
- **WHEN** I open a season whose start date has not yet arrived and whose last fetch was on an earlier local date
- **THEN** a refresh runs for it, because a season that has not started is in the one-day tier

#### Scenario: A failed fetch does not consume the interval
- **WHEN** a refresh fails and I open that season again the same day
- **THEN** the refresh is retried, because only a successful fetch counts toward the interval

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

#### Scenario: No control exposes the interval
- **WHEN** I look for a way to force a refresh or to change how often a season is refreshed
- **THEN** the page offers neither, and does not state when the season was last fetched

### Requirement: MAL's forward season horizon
MyAnimeList publishes season listings only a bounded distance into the future and answers `404` for a season it has not opened yet; anime announced beyond that point carry no season classification at all and are unreachable through the season endpoint. The system SHALL treat that `404` as a fact about the season — MAL has no listing for it — and SHALL NOT treat it as a fetch failure.

A season MAL reports no listing for SHALL be recorded as fetched, with the same timestamp semantics as a successful fetch, so the season's own refresh interval applies to it and the page leaves its loading state. For a future season that interval is one day, so an unopened season is re-probed daily; an old season MAL has no listing for is re-probed on its own longer interval like any other season of its age. The record SHALL be superseded by a later fetch that does return a listing, so a season MAL opens later stops being marked as unlisted without any manual intervention. A season marked as unlisted SHALL keep whatever listing it already had cached — the mark records MAL's answer, it does not delete anime.

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

#### Scenario: The re-probe is never delayed by the age tiers
- **WHEN** a season at or beyond the ceiling has been marked unlisted and a new local day begins
- **THEN** the next visit re-fetches it, because a season that has not started yet is in the one-day tier

#### Scenario: Ceiling never falls below the current season
- **WHEN** MAL answers `404` for every season from the current season forward
- **THEN** the current season remains navigable
