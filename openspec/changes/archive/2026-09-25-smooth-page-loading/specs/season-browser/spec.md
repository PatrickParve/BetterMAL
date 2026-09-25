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

The visit's refresh SHALL be requested once the page's own read of the season's cached listing has settled successfully, and its outcome SHALL be judged against what that read found. A refresh outcome SHALL NOT decide what the page shows while the page's own read is still in flight. A skipped refresh SHALL lead to a further read of the cached listing only when the page's settled read found nothing cached for the season, the case where another tab's fetch landed in between; otherwise the listing SHALL be read once per visit. When the page's own read comes back from a back/forward restore snapshot, it has already settled, and the refresh SHALL be requested at once, as today.

The passive updating indicator SHALL follow the `page-load-states` capability: it SHALL appear only once a refresh has been running for that capability's delay, so a refresh that is skipped almost at once leaves no trace on screen.

While a season with nothing cached waits on its first fetch from MAL, its loading state SHALL say that the season is being fetched from MyAnimeList.

A season with no anime to show SHALL say which of three situations it is in: MAL has no listing for it yet, MAL lists it but nothing matches the current filters, or its first fetch from MyAnimeList failed and will be retried on the next visit. The third of these states the failure of that first fetch only — it SHALL NOT be shown for a season that already has a cached listing, which continues to render its cached anime with no error. None of the three SHALL be shown until the page's own read has settled.

A failure of the page's own read of the cached listing, for example because the app's server cannot be reached, is a different situation. It SHALL be presented as the `page-load-states` capability's failure state, with Try again and the automatic retry when the server is reachable again. No refresh SHALL be requested for a visit whose read has not succeeded. Once a retried read succeeds, that visit's refresh SHALL be requested as on any visit, subject to the season's interval. Try again SHALL NOT be offered for a failed first fetch from MyAnimeList, since no control may trigger a refresh.

#### Scenario: Cached season renders before the refresh completes
- **WHEN** I open a season that already has a cached listing
- **THEN** the cached anime are displayed immediately without waiting for any MAL request

#### Scenario: A season fetched today opens without an error flash
- **WHEN** I open a season that has a cached listing and was already fetched today, so its refresh is skipped almost at once
- **THEN** the page goes from its header straight to the grid, never showing a "could not be loaded", "not listed" or "no anime match" message and never showing the updating indicator, and the cached listing is read once

#### Scenario: Past season refreshes on visit
- **WHEN** I open a fully-past season whose last fetch is older than its interval
- **THEN** a background refresh runs for it just as it would for the current season, and the page updates once it completes

#### Scenario: Background refresh updates the page
- **WHEN** a background refresh finishes
- **THEN** the displayed results are re-read from the cache and updated in place, preserving my scroll position and the number of pages I had already loaded

#### Scenario: Refresh in progress is visible
- **WHEN** a background refresh for the season I am viewing has been running for longer than the loading indicator's delay
- **THEN** the page shows a passive updating indicator, so results changing underneath me are explained, and the indicator disappears when the refresh completes

#### Scenario: Empty cache waits for the first fetch
- **WHEN** I open a season that has never been cached
- **THEN** the page shows a loading state saying the season is being fetched from MyAnimeList until the first fetch completes, rather than reporting that no anime were found

#### Scenario: An old season with nothing cached still fetches on the first visit
- **WHEN** I open a season that started twelve years ago and has never been fetched
- **THEN** it is fetched immediately, because the ten-day interval applies only between fetches and there is no previous fetch to measure from

#### Scenario: Loading state ends when the first fetch finds no MAL listing
- **WHEN** I open a season that has never been cached and MAL reports it has no listing for that season
- **THEN** the loading state ends and the page says MyAnimeList has not listed that season yet, rather than continuing to load or claiming no anime were found

#### Scenario: Loading state ends when the first fetch fails
- **WHEN** I open a season that has never been cached and its first fetch from MyAnimeList fails
- **THEN** the loading state ends and the page says the season could not be fetched from MyAnimeList and will be retried next time, rather than continuing to load or claiming the season is empty, and it offers no Try again control

#### Scenario: The page's own read fails
- **WHEN** I open a season while the app's server cannot be reached
- **THEN** the page shows the failure state with Try again, no refresh is requested, and once the server is reachable again the page reads the season by itself and then requests the visit's refresh

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

#### Scenario: Another tab's fetch lands between the read and the refresh
- **WHEN** my read of a season finds nothing cached, another tab's fetch of that season then lands, and my refresh is skipped as already fresh
- **THEN** the page reads the cached listing again and shows it, rather than staying on its loading state or reporting a failure

#### Scenario: Changing sort or filter does not refetch
- **WHEN** I change the sort order or the "In my list" checkbox without changing the season
- **THEN** the results are re-read from the cache and no MAL fetch is started

#### Scenario: Failed refresh keeps the cached page
- **WHEN** a background refresh fails (MAL is unreachable or returns an error)
- **THEN** the page keeps showing the cached listing and the failure is not surfaced as a page error

#### Scenario: No control exposes the interval
- **WHEN** I look for a way to force a refresh or to change how often a season is refreshed
- **THEN** the page offers neither, and does not state when the season was last fetched
