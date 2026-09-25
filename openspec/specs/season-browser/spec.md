# season-browser Specification

## Purpose
The season-browser capability governs browsing every anime MAL classifies to a season, not just my list, using MAL's own season and type classification and a cache-first read that refreshes in the background on each visit. It bounds requests to MAL's own forward season horizon and refuses anything beyond it, and it excludes hentai from the listing whenever the hide-NSFW setting is on. Year-browser derives its own listing entirely from this capability's cached seasons.
## Requirements
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

### Requirement: MAL's forward season horizon
MyAnimeList publishes season listings only a bounded distance into the future and answers `404` for a season it has not opened yet; anime announced beyond that point carry no season classification at all and are unreachable through the season endpoint. The system SHALL treat that `404` as a fact about the season — MAL has no listing for it — and SHALL NOT treat it as a fetch failure.

A season MAL reports no listing for SHALL be recorded as fetched, with the same timestamp semantics as a successful fetch, so the season's own refresh interval applies to it and the page leaves its loading state. For a future season that interval is one day, so an unopened season is re-probed daily; an old season MAL has no listing for is re-probed on its own longer interval like any other season of its age. The record SHALL be superseded by a later fetch that does return a listing, so a season MAL opens later stops being marked as unlisted without any manual intervention. A season marked as unlisted SHALL keep whatever listing it already had cached — the mark records MAL's answer, it does not delete anime.

The system SHALL expose the furthest season the user may navigate to (the navigable ceiling), computed as: the current season plus MAL's two-season forward window; raised to any later season that already has a cached listing, so a wider window, once observed, stays reachable; and lowered past any trailing season that MAL answered `404` for on the current local date, so the day's observed horizon is respected. The ceiling SHALL never fall below the current season. A `404` mark SHALL constrain the ceiling only on the local day it was made, so that the ceiling re-probes daily and rolls forward on its own as MAL opens each new season.

**Discovering that MAL has opened a further season.** The window's own arithmetic never reaches past the current season plus two, so a season MAL opens beyond it can only be found by asking. The system SHALL therefore probe a single season — **the current season plus three**, one past the forward window — when a Season or Year page is visited, without the user navigating to it and without naming it in any request.

The probe's target SHALL be that fixed season rather than whichever season currently follows the ceiling, so that a successful probe ends the probing instead of moving the target one further out: a target that creeps outward with the ceiling would ask, every day thereafter, about a season MAL will not open for months. The probe SHALL be skipped entirely when its target is already at or below the outer ceiling.

The probe SHALL run only during the **final month of the current season**, and at most **once per local day**, which is when MyAnimeList opens the season three quarters ahead. Outside that month the system SHALL make no probe request at all. This interval is the probe's own rule and SHALL NOT be the age-based refresh interval, which places every future season in the one-day tier — right for a season being read, wrong for a speculative question. The probe's gate SHALL be applied before the fetch path's own, so the stricter of the two always governs. Past its gate the probe SHALL use the same fetch path a visit to that season would: the same single-flight guard, the same caching and pruning, and the same treatment of `404` as a fact rather than a failure.

A probe that returns anime SHALL cache them, which raises the outer ceiling to that season by the rule above, after which no further probe SHALL be made until the calendar moves the current season on. A probe answered `404` SHALL change nothing but that season's fetch record: because the probed season lies beyond the window the ceiling's `404` step-back considers, a probe SHALL NOT be able to lower either ceiling.

Not probing SHALL cost nothing but time: a season never probed simply stays hidden until the calendar brings it inside the forward window. The probe SHALL never block or delay what the page renders, except as "Season selection" provides for a URL addressing the probe's own target, and a failed probe SHALL NOT be surfaced.

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

#### Scenario: A visit in the last month probes the season past the window
- **WHEN** I open a Season or Year page during the final month of the current season, and the current season plus three has not been asked about today
- **THEN** that one season is fetched from MAL in the background, without my navigating to it and without the page waiting on it

#### Scenario: No probing outside the last month
- **WHEN** I open a Season or Year page during the first or second month of the current season
- **THEN** no probe request is made, whatever the ceiling is

#### Scenario: A successful probe raises the horizon
- **WHEN** the probed season returns anime
- **THEN** they are cached, the ceiling extends to that season, and the arrows and dropdowns offer it from then on

#### Scenario: A successful probe ends the probing
- **WHEN** a probe has succeeded and I open more Season and Year pages on later days of the same current season
- **THEN** no further probe request is made, because the target is already at or below the ceiling

#### Scenario: The target does not creep outward
- **WHEN** the ceiling has been raised to the current season plus three
- **THEN** the current season plus four is never probed; the target moves only when the calendar moves the current season on

#### Scenario: A probe cannot lower the ceiling
- **WHEN** the probed season is answered `404`
- **THEN** only its fetch record changes: the navigable ceiling and the outer ceiling are exactly what they were before the probe

#### Scenario: Probing costs one request a day
- **WHEN** I visit several Season and Year pages, in several tabs, on the same local day
- **THEN** the probed season is fetched at most once across all of them

#### Scenario: Never probing loses nothing but time
- **WHEN** no page is opened during the final month of a season, so its probe never runs
- **THEN** the unprobed season becomes reachable anyway once the calendar brings it inside the forward window

#### Scenario: A failed probe is invisible
- **WHEN** the probe's MAL request fails
- **THEN** the page renders exactly as it would have, no error is surfaced, and the probe is retried on a later visit because a failed fetch does not consume the interval

### Requirement: Season selection
The system SHALL allow changing the selected season, and SHALL keep the selected season, sort, and filter state in the page's URL so it persists through back-navigation from an anime detail page. Opening the Season page from the navbar with no season specified SHALL default to the current season. The system SHALL provide a quick-jump control to select a specific year and season directly, in addition to stepping one season at a time.

Forward navigation SHALL stop at the navigable ceiling defined by "MAL's forward season horizon": at the ceiling the next-season control SHALL be disabled, and the quick-jump controls SHALL NOT offer any season beyond it — neither a year later than the ceiling's year, nor a season later than the ceiling's season within that year. Backward navigation SHALL be unaffected, and SHALL be floored at winter of the earliest year in MyAnimeList's season archive — the same earliest year the API accepts, so every season the API would serve is reachable from the controls alone.

The quick-jump's year list SHALL be derived from the addressable range described below and SHALL NOT be widened, lengthened or otherwise sized by the year currently in the URL. No part of rendering the page SHALL allocate per-year, per-season or per-option work proportional to a value taken from the URL.

**The addressable range.** A season SHALL be addressable when it falls, in season order, between winter of the archive's earliest year and the navigable ceiling — that is, a URL SHALL reach exactly what the step and quick-jump controls offer and nothing further. There SHALL NOT be a second, wider ceiling for URLs, so the page and its own controls never disagree about whether a season exists.

Because the navigable ceiling never falls below the current season, a season between the archive's earliest year and the current season SHALL be admitted immediately, without waiting on the ceiling. A season later than the current season SHALL be admitted or replaced only once the ceiling is known; until then the page SHALL render nothing and SHALL request nothing for it. If the ceiling cannot be determined, the current season plus MAL's forward season window SHALL be assumed, so a failed bounds request never walls off the seasons that window covers.

A season addressed directly in the URL that **is** addressable SHALL be rendered as asked rather than silently rewritten, so an old link or a bookmark is never redirected; forward movement from there SHALL remain blocked.

The ceiling SHALL be read once per visit, so a ceiling that retreats while a season is being read — because MAL answered `404` for it during that same visit — SHALL NOT replace the season out from under the reader; the page SHALL report that MAL has not listed it, as it does for any unlisted season. Addressing that season again after the ceiling has retreated SHALL be replaced like any other season past the ceiling, and SHALL become addressable again when the ceiling springs back on the next local day.

**The one season a URL may ask about.** A URL addressing exactly the horizon probe's target (see "MAL's forward season horizon") SHALL trigger that probe and be decided on its result: if MAL returns anime the season is cached, the ceiling rises to cover it, and the page renders it like any other season; if not, it is replaced like any other season past the ceiling. This is the only season past the ceiling for which any request is made, and the only case in which the page waits on MAL before deciding. The probe's once-per-local-day gate SHALL still apply, so a season already answered today is decided on that answer without asking again; its last-month-of-the-season window SHALL NOT apply, since a URL is an explicit question rather than a background guess. Every season beyond the probe's target SHALL be replaced with no request of any kind.

A season addressed in the URL that is **not** addressable, and a URL whose year or season parameter is absent, malformed or not a whole number, SHALL be replaced with the current season. The replacement SHALL take effect before the page requests anything: no cached read, no refresh, no MAL request and no fetch-log write SHALL be made for the rejected season. The replacement SHALL substitute the current entry in the browser's history rather than adding one, so going back does not return to the rejected URL, and SHALL preserve every other parameter in the query string. The system SHALL NOT show a message, an error state or any other notice about the replacement.

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

#### Scenario: Stepping back stops at the archive's first season
- **WHEN** I am viewing winter of the archive's earliest year
- **THEN** the previous-season control is disabled and I cannot step past it

#### Scenario: Quick-jump reaches the whole archive
- **WHEN** I open the year quick-jump
- **THEN** it offers every year from the archive's earliest year through the ceiling's year, so a season from the 1920s is reachable without typing a URL

#### Scenario: Quick-jump does not offer seasons past the horizon
- **WHEN** I open the year and season quick-jump controls
- **THEN** they offer nothing later than the navigable ceiling — the year list ends at the ceiling's year, and within that year the season list ends at the ceiling's season

#### Scenario: Switching year clamps a season past the horizon
- **WHEN** I am viewing spring of some year and use the year quick-jump to select the ceiling's year, whose ceiling season is winter
- **THEN** the selected season clamps to winter of that year rather than landing on a season past the ceiling

#### Scenario: Addressing the probe's target checks it
- **WHEN** I open a link to the season the horizon probe targets, it has not been asked about today, and MAL now lists it
- **THEN** it is fetched, cached, and shown as asked, and the arrows and dropdowns offer it from then on

#### Scenario: The probe's target is replaced when MAL still has nothing
- **WHEN** I open a link to the probe's target and MAL answers `404`
- **THEN** the current season is shown with the URL replaced, no message is shown, and the ceiling is unchanged

#### Scenario: Only the probe's target earns a request
- **WHEN** I open a link to a season further out than the probe's target
- **THEN** the current season is shown with the URL replaced and no request is made for the addressed season

#### Scenario: A URL past the horizon is replaced
- **WHEN** I open a bookmarked link to a season past the navigable ceiling, such as one MAL answered `404` for earlier today
- **THEN** the current season is shown with the URL replaced, exactly as for any other season the controls do not offer

#### Scenario: A retreating ceiling does not interrupt the season being read
- **WHEN** I am viewing the season at the ceiling and this visit's own refresh is answered `404` by MAL, retreating the ceiling
- **THEN** the page stays on that season and reports that MyAnimeList has not listed it, rather than replacing it with the current season while I am reading it

#### Scenario: The horizon springs back the next day
- **WHEN** a season was replaced because the ceiling had retreated past it, and a new local day begins
- **THEN** the same URL is addressable again and the season is fetched afresh

#### Scenario: A future season admitted only once the ceiling is known
- **WHEN** I open a link to a season later than the current one
- **THEN** nothing is read or refreshed for it until the ceiling is known, and it is then either shown or replaced with the current season

#### Scenario: A season before the archive is replaced
- **WHEN** I open a link to a season earlier than winter of the archive's earliest year, such as fall 1916
- **THEN** the page shows the current season, the URL is replaced with it, no message is shown, and no read, refresh or MAL request is made for 1916

#### Scenario: An impossible year is replaced without rendering it
- **WHEN** I open a link such as a season in the year 32932734
- **THEN** the page shows the current season with its URL replaced, no message is shown, no request is made, and no control is built from the rejected year

#### Scenario: A malformed season parameter is replaced
- **WHEN** I open a link whose year is not a whole number or whose season is not one of winter, spring, summer or fall
- **THEN** the page shows the current season and the URL is replaced with it

#### Scenario: A replacement does not leave a step back into the bad URL
- **WHEN** a season I addressed is replaced with the current season and I press the browser Back button
- **THEN** I go back to wherever I came from, not to the rejected season

#### Scenario: A replacement keeps the rest of the query
- **WHEN** the link I opened is out of range but also carries a sort and a type filter
- **THEN** the current season is shown with that sort and type filter still applied

#### Scenario: A linked season older than the current year renders
- **WHEN** I follow an anime detail page's link to a season decades older than the current one
- **THEN** that season is shown and refreshed like any other, and its year is among the quick-jump's options

#### Scenario: Header control placement
- **WHEN** I view the season header
- **THEN** the arrows and season label sit centered in the header with the year/season quick-jump dropdowns directly to their right, rather than the quick-jump sitting alone at the far right edge

### Requirement: Season card content
The system SHALL show, on each season card, the anime's title, picture, episode count, type (TV/movie/etc), and MAL score.

The type and episode count SHALL sit at the leading edge of the card's meta line and the MAL score at its trailing edge, on that same line and sharing its baseline, so a column of cards reads as one row of figures per card rather than as a stack of separate lines. The score SHALL be rendered in the app's MAL colour, in the same two-decimal format every other MAL score uses.

An anime with no MAL score SHALL show nothing in that slot rather than a placeholder — an unrated or not-yet-aired title simply has no figure to report there, and the type and episode count keep their own position regardless.

While the global hide-scores toggle is on, the card SHALL show no score at all: no value, no placeholder, and no per-score reveal control, so a season of cards under hiding carries no score furniture. The type and episode count SHALL stay exactly where they are.

#### Scenario: Rendering a season card
- **WHEN** season anime are displayed
- **THEN** each card shows the anime's title, picture, episode count and type at the start of its meta line, and its MAL score at the end of that same line

#### Scenario: Unknown episode count on a season card
- **WHEN** a season anime's total episode count is unknown
- **THEN** the card shows the episode count as `?`

#### Scenario: An anime with no MAL score
- **WHEN** a season anime has no MAL score
- **THEN** its card's meta line shows only the type and episode count, with nothing in the score slot

#### Scenario: Hiding scores removes the card score entirely
- **WHEN** the global hide-scores toggle is on
- **THEN** no season card shows a MAL score, a placeholder, or a reveal control, and the type and episode count are unmoved

### Requirement: Season grid fills the content width
The season results grid SHALL size its cards so that each row spans the full width of the page content, leaving no unused gutter to the right of the last card in a row, and SHALL show cover images larger than the fixed-width browse card. The number of cards per row SHALL adapt to the available width, but SHALL be a fixed count at ordinary desktop widths (above the mobile breakpoint) rather than one derived by dividing the container width by a minimum card size — the latter can flip the column count from a small, incidental change in the page's effective width (e.g. a scrollbar or a different display's exact resolution/scaling) even though nothing about the window's actual size class changed. The page's outer container SHALL likewise fill the available display width rather than being capped to a fixed design width, so the grid (and the rest of the page) render the same way regardless of which display, or display configuration, the browser is on.

Every card SHALL occupy exactly one column whatever the shape of its picture. A card whose picture is upright or wide SHALL draw it whole inside the card's portrait picture box, under the `artwork-presentation` capability's rule for fixed poster boxes, rather than spanning further columns. No card's picture SHALL leave a row ending short, push a card onto a new row, or move any card when that picture loads.

Where the grid reveals its cards progressively as it is scrolled, each reveal SHALL end on a complete row whenever more cards remain to be revealed. A reveal step whose cards do not fill its last row, as when the column count below the desktop breakpoint does not divide the step, SHALL NOT leave cards alone on that row waiting for the next reveal to fill in beside them. The grid SHALL reveal as many further cards as that row needs, and SHALL do so again whenever a change of the grid's width leaves the last revealed row short. Only the last row of the whole list MAY be short, since that is where the list ends. Every grid that shares this grid's form, including the Year, Search and Series browser grids, SHALL reveal by this same rule.

#### Scenario: Grid leaves no right-hand gutter
- **WHEN** season results are displayed at any window width
- **THEN** the cards in each full row together span the content width, with no large empty space to the right of the grid

#### Scenario: A wide picture keeps its card to one column
- **WHEN** the card whose turn falls in a row's last column holds a wide picture
- **THEN** that card takes the row's last column like any other card, shows its whole picture inside its portrait picture box, and the row spans the content width

#### Scenario: Adapting to a narrower window
- **WHEN** I narrow the window
- **THEN** fewer cards are placed per row and the cards continue to fill the row width

#### Scenario: Card count stays fixed across ordinary desktop widths
- **WHEN** the page's effective width changes slightly at ordinary desktop sizes (e.g. connecting or disconnecting an external display changes the browser's logical resolution, but the window is still a normal desktop width)
- **THEN** the grid continues to show the same number of cards per row at essentially the same size, rather than jumping to one fewer or one more card per row

#### Scenario: Page fills the display instead of showing a boxed layout
- **WHEN** the browser window is wider than the page's previous fixed design width (an external monitor, or a laptop's own unscaled display)
- **THEN** the page content still fills the available width edge to edge, rather than sitting in a centered column with empty space and a visible border on both sides

#### Scenario: A reveal ends on a full row
- **WHEN** a Season, Year, Search or Series browser grid shows a column count that does not divide its reveal step, and reveals its next cards
- **THEN** the last revealed row is still complete, with no card on it alone, and the cards continue in the page's sort order

#### Scenario: A resize keeps the last row full
- **WHEN** I narrow the window so that the grid's last revealed row no longer ends complete
- **THEN** the grid reveals the cards that complete that row, without waiting for the grid to be scrolled further

### Requirement: Season filtering
The system SHALL allow filtering/sorting season anime by popularity, score, alphabetical, and my score, where my score is meaningful only for anime also in my list. When sorting by popularity, anime that are unranked — MAL popularity rank absent or zero — SHALL be ordered after all ranked anime (a rank of `0` is not treated as "most popular"), then ranked anime by ascending rank (1 = most popular), then by displayed title.

Every ordering that reads a title — the alphabetical sort itself, and the title tie-break of the popularity, MAL-score and my-score sorts — SHALL use the **displayed title**: the anime's English title when MyAnimeList has one, and its original title otherwise. This is the same title the card shows, so an alphabetical listing reads in the order of the names on screen rather than in the order of names that are not displayed.

When sorting by my score, the results SHALL be split into two ordered groups: first every anime I have scored, ordered by my score descending — equal scores broken by my ranking, best-ranked first, per the `anime-ranking` capability, then by popularity, then by displayed title for anime my ranking does not cover; then every remaining anime, ordered by popularity using the same unranked-last rule as the popularity sort. The page SHALL render a visual break with the label `Unwatched` between the two groups, shown only when both groups have at least one anime.

Every one of these orderings SHALL be **produced server-side**, over the season's whole listing, and SHALL reach the page as a per-anime ordering position rather than as a rule the page applies itself — so the page can re-sort what it has already loaded without any ordering rule being expressed twice, and so the order it shows is by construction the order the server computed.

Changing the sort SHALL therefore take effect **immediately**, with no read, no loading state, and the same results and order the same sort produces today. It SHALL still count as a fresh view of the page: the grid SHALL return to the top, showing its first screenful.

#### Scenario: Alphabetical follows the displayed title
- **WHEN** I sort a season alphabetically and it contains an anime whose English title is "Frieren: Beyond Journey's End" and whose original title is "Sousou no Frieren"
- **THEN** it sits among the F's, where the card's own title puts it, not among the S's

#### Scenario: Alphabetical falls back to the original title
- **WHEN** I sort a season alphabetically and one of its anime has no English title on MyAnimeList
- **THEN** it is placed by its original title, which is also the title its card shows

#### Scenario: Sorting by my score
- **WHEN** I sort by my score
- **THEN** the anime I have scored appear first in descending score order, followed by an `Unwatched` divider, followed by the remaining anime in popularity order

#### Scenario: Equal scores follow my ranking
- **WHEN** I sort by my score in a season where I gave three anime the same score
- **THEN** those three appear in my ranking's order rather than in popularity order

#### Scenario: An unranked scored anime among ranked ones
- **WHEN** a scored anime the ranking does not cover shares a score with ranked anime
- **THEN** the ranked ones come first and it follows them, ordered by popularity among any other unranked anime of that score

#### Scenario: My-score grouping holds all the way down
- **WHEN** I sort by my score and scroll to the end of the season
- **THEN** no scored anime appears after the `Unwatched` divider, anywhere in the grid

#### Scenario: No scored anime in the season
- **WHEN** I sort by my score in a season where I have scored nothing
- **THEN** all anime are shown in popularity order with no `Unwatched` divider

#### Scenario: Unranked anime sort last by popularity
- **WHEN** I sort by popularity and some anime have no popularity rank (rank absent or zero)
- **THEN** the ranked anime appear first in ascending rank order and the unranked anime appear last, rather than an unranked anime sorting to the top

#### Scenario: Tied anime break by the displayed title
- **WHEN** two anime tie on the key being sorted on and their English and original titles order differently
- **THEN** they are separated by their displayed titles, matching the order their cards read in

#### Scenario: Sorting is instant
- **WHEN** I change the sort on a season that is already loaded
- **THEN** the grid re-orders immediately with no read and no loading state

#### Scenario: A sort is still a fresh view
- **WHEN** I change the sort after scrolling deep into a season
- **THEN** the re-sorted grid opens at the top on its first screenful

### Requirement: In-my-list inclusion filter
The system SHALL provide an "In my list" checkbox beside the season sort control, checked by default. When it is unchecked, anime that have an entry in my list SHALL be excluded from the season results and from the result count the page reports, so only anime not yet in my list are shown. The checkbox state SHALL be part of the page's URL state.

The filter SHALL be applied to the season's already-loaded listing rather than through a read, so it takes effect immediately with no loading state, and SHALL exclude exactly the same anime it excludes today. Toggling it SHALL count as a fresh view of the page, returning the grid to the top on its first screenful.

#### Scenario: Default includes my list
- **WHEN** I open a season
- **THEN** the "In my list" checkbox is checked and anime in my list are shown alongside the rest

#### Scenario: Excluding anime in my list
- **WHEN** I uncheck "In my list"
- **THEN** every anime that has an entry in my list is removed from the results, the reported count reflects the smaller set, and scrolling continues to show only anime not in my list

#### Scenario: The filter is instant
- **WHEN** I toggle "In my list" on a season that is already loaded
- **THEN** the grid updates immediately with no read and no loading state

#### Scenario: Filter persists through back-navigation
- **WHEN** I uncheck "In my list", open an anime, then press the browser Back button
- **THEN** the checkbox is still unchecked and the filtered results are shown

### Requirement: Season type filter
The system SHALL provide a multi-select Type filter beside the season sort control, using the same control and display labels as My List's type filter, offering only the media types actually present in the season. Selecting one or more types SHALL restrict the season results, and the count the page reports, to matching types.

The filter SHALL distinguish **All** from **None** as the `page-header-design` capability defines: on **All** — its state on a fresh visit — no type restriction applies; on **None**, no anime passes the type filter and the page SHALL report that no anime match the current filters, using the same message it shows when other filters exclude everything rather than reporting that the season has no listing.

The selection state SHALL be part of the page's URL state so it survives back-navigation, and **All**, **None**, and a partial selection SHALL each be distinctly representable there: a URL that names no type filter at all SHALL mean **All**, so links made before this distinction existed continue to mean what they meant.

The filter SHALL be applied to the season's already-loaded listing rather than through a read, so it takes effect immediately with no loading state. The types it offers SHALL be derived from the whole listing, not from what the current selection leaves visible, so selecting one type SHALL NOT remove the others from the picker. Toggling the filter SHALL count as a fresh view of the page, returning the grid to the top on its first screenful.

#### Scenario: Filtering a season to one type
- **WHEN** I select Movie in the season page's type filter
- **THEN** only movie entries are shown for that season, and the count the page reports reflects only movies

#### Scenario: The filter holds all the way down
- **WHEN** I select a type and scroll to the end of the season
- **THEN** every card shown is of a matching type

#### Scenario: Only present types are offered
- **WHEN** a season contains no music videos
- **THEN** the type filter does not offer Music as an option for that season

#### Scenario: Selecting a type does not shrink the type options
- **WHEN** I select one type and the results narrow to it
- **THEN** the type filter still offers the season's other types, so I can change or clear my selection

#### Scenario: The filter is instant
- **WHEN** I change the type selection on a season that is already loaded
- **THEN** the grid updates immediately with no read and no loading state

#### Scenario: All applies no type restriction
- **WHEN** the season page's type filter is on All
- **THEN** entries of every type are shown

#### Scenario: None empties the grid and says so
- **WHEN** I press **None** in the season page's type filter
- **THEN** no cards are shown and the page reports that no anime match the current filters, rather than that the season is not listed

#### Scenario: A link with no type filter means All
- **WHEN** I open a season page from a link whose address names no type filter
- **THEN** the type filter is on All and anime of every type are shown

#### Scenario: Filter persists through back-navigation
- **WHEN** I select a type, open an anime, then press the browser Back button
- **THEN** the same type selection is still applied and the filtered results are shown

#### Scenario: None persists through back-navigation
- **WHEN** I press **None**, navigate away, and press the browser Back button
- **THEN** the filter is still on **None** rather than back on All

### Requirement: Infinite scroll
The season page SHALL load the selected season's **whole** listing — every anime the cache holds for it under the selected sort and filters — in a single read, and SHALL reveal that listing progressively as it is scrolled, adding a screenful at a time. Nothing SHALL cap how many of the season's anime can be reached: scrolling to the end of the grid SHALL reach the last anime the season holds.

Revealing SHALL be a client-side act over the already-loaded listing, not a further read: scrolling SHALL NOT issue requests, and the number of anime revealed SHALL NOT be limited by how many any one read returns.

How much of the listing has been revealed SHALL be part of the page's restorable state per the `page-state-restoration` capability: a back/forward restore SHALL bring back the same number of cards the page was showing when it was left, so the scroll position restored alongside them is reachable rather than clamped to the top. A fresh visit — including the fresh visit a sort or filter change amounts to — SHALL open on the first screenful at the top of the grid.

Because the whole listing is read at once, no refresh of the page SHALL be able to leave it showing fewer anime than it was showing before: neither the background refresh that follows a restore, nor the visit-triggered MAL refresh, SHALL shorten a grid that has been scrolled.

#### Scenario: Scrolling loads more
- **WHEN** I scroll to the end of the loaded season results
- **THEN** more results appear automatically

#### Scenario: Nothing is capped
- **WHEN** I keep scrolling a season holding several hundred anime
- **THEN** I reach its last anime, with no ceiling short of the season's own size

#### Scenario: Scrolling costs no requests
- **WHEN** I scroll from the top of a season's grid to its end
- **THEN** the results were all already loaded and no further read was issued to reveal them

#### Scenario: A restored season keeps what it had revealed
- **WHEN** I scroll a season well past its first screenful of cards, open one of them, and go back
- **THEN** the grid shows the same number of cards it showed when I left, both immediately and after its background refresh lands

#### Scenario: Returning to where I was in a season
- **WHEN** I go back to a season I had scrolled far down
- **THEN** the page is at the position I left it at and stays there once the refresh completes

#### Scenario: A refresh cannot shorten a scrolled grid
- **WHEN** I have scrolled a season deep into its listing and its visit-triggered refresh then completes
- **THEN** the grid still shows everything it was showing, rather than being cut back to a screenful

#### Scenario: A sort change starts again at the top
- **WHEN** I change the sort on a season I had scrolled deep into
- **THEN** the re-sorted grid opens on its first screenful at the top

### Requirement: Hide-NSFW setting excludes hentai from the season browser
The system SHALL provide a persisted user setting, presented on the Settings page as a "Hide NSFW" checkbox and **checked by default** whenever no choice has been stored — as on the first run of the app in a browser — that excludes hentai from the season browser's results and from the year browser's results — the two pages read the same cached season listings, at a season's grain and at a year's, so the exclusion follows the data rather than the page. Once a choice is stored, the stored choice SHALL govern from then on, whether checked or unchecked; the default SHALL NOT override it.

The filter SHALL exclude hentai and nothing else: an anime SHALL be treated as hentai when, and only when, MAL's own `rating` field for it is `rx`. Every other rating — including `r` and `r+` — SHALL remain visible with the setting enabled, so mature and ecchi titles are not swept up by it. An anime whose rating MAL has not yet reported (not yet cached) SHALL be treated as not hentai, so the filter never hides a title it cannot positively identify.

The filter SHALL be applied server-side, before the result count is computed, so the displayed count and infinite scroll stay correct on both pages. It SHALL apply to the season and year browsers only: search results, my list, top anime, the airing schedule, the home dashboard, and anime detail pages SHALL be unaffected by it. The setting SHALL persist across reloads and new tabs, and SHALL take effect on either page without requiring a MAL refetch.

#### Scenario: Default is on for a first run
- **WHEN** I open the Settings page in a browser that has no stored choice for this setting
- **THEN** the "Hide NSFW" checkbox is checked, and the season page omits every anime MAL rates `rx`

#### Scenario: Unchecking is remembered
- **WHEN** I uncheck "Hide NSFW" and then reload the page or open the app in a new tab
- **THEN** the checkbox is still unchecked and the season page shows hentai alongside everything else, rather than the first-run default re-enabling the filter

#### Scenario: A stored choice is not overridden by the default
- **WHEN** a browser already has a stored "Hide NSFW" choice of unchecked from before this default was introduced
- **THEN** the checkbox remains unchecked there

#### Scenario: Enabling the setting hides hentai from the season page
- **WHEN** I enable "Hide NSFW" and open a season containing hentai
- **THEN** every anime MAL rates `rx` is absent from the results, and the result count reflects the smaller set

#### Scenario: Enabling the setting hides hentai from the year page
- **WHEN** I enable "Hide NSFW" and open a year whose seasons contain hentai
- **THEN** every anime MAL rates `rx` is absent from the year's combined results, and the result count reflects the smaller set

#### Scenario: R and R+ titles stay visible
- **WHEN** "Hide NSFW" is enabled and a season contains anime rated `r` or `r+`
- **THEN** those anime are still shown — only `rx` titles are removed

#### Scenario: Unknown rating is not hidden
- **WHEN** "Hide NSFW" is enabled and a cached season anime has no rating recorded yet
- **THEN** it is still shown, rather than being hidden on suspicion

#### Scenario: Filtering holds across infinite scroll
- **WHEN** "Hide NSFW" is enabled and I scroll far enough to load additional pages of a season or a year
- **THEN** no hentai appears in any loaded page, because the exclusion is applied server-side rather than to each loaded page on the client

#### Scenario: Toggling re-reads from cache only
- **WHEN** I change the "Hide NSFW" setting while viewing a season or a year
- **THEN** the results are re-read from the cache with the new filter applied and no MAL fetch is started

#### Scenario: Setting persists
- **WHEN** I enable "Hide NSFW" and later reload the app or open it in a new tab
- **THEN** the setting is still enabled

#### Scenario: Other pages are unaffected
- **WHEN** "Hide NSFW" is enabled and I use search, my list, top anime, the airing schedule, or the home dashboard
- **THEN** those pages show exactly what they showed before, including any hentai already in my list

### Requirement: Cached anime records carry MAL's content rating
The system SHALL store MAL's `rating` value (`g`, `pg`, `pg_13`, `r`, `r+`, `rx`) on each cached anime record, populated both by the lean listing refresh that backs season browsing and by full detail fetches, so the season browser can filter on it without an extra MAL request. A missing or unrecognized rating SHALL be stored as absent rather than rejected.

#### Scenario: Season refresh records ratings
- **WHEN** a season is refreshed from MAL
- **THEN** each returned anime's rating is stored on its cached record

#### Scenario: Anime cached before ratings were stored
- **WHEN** an anime was cached before the rating field was recorded and its season has not been refreshed since
- **THEN** its rating is absent and it is treated as not hentai, until the next refresh of that season fills it in

#### Scenario: Rating absent from MAL
- **WHEN** MAL returns an anime with no rating value
- **THEN** the cached record stores no rating and the ingest succeeds

### Requirement: Requests outside the seasons MAL could list are refused
The season endpoints (`GET /api/season/{year}/{season}` and `POST /api/season/{year}/{season}/refresh`) and the year endpoints (`GET /api/year/{year}` and `POST /api/year/{year}/refresh`) SHALL refuse a request for a season or year outside the range MAL could list. The response SHALL be `400`, in the same `{ error }` shape as an unknown season name. It SHALL be sent before the cache is read, before anything is requested from MAL, and before any fetch record is written. A refused request SHALL NOT be moved to the nearest valid season, and SHALL NOT be served as an empty listing.

The range SHALL start at winter 1917, the first season in MyAnimeList's season archive. It SHALL end at the **outer ceiling**: the current season plus MAL's two-season forward window, raised to any later season that already has a cached listing. The outer ceiling is the navigable ceiling from "MAL's forward season horizon" before that ceiling is lowered past seasons MAL answered `404` for today. It SHALL NOT follow that lowering. A season MAL answered `404` for today has already been fetched today, so accepting it costs no MAL request. Refusing it would turn the page's own re-read after that `404` into an error. Both ends of the range SHALL be accepted.

A season SHALL be accepted when it falls, in season order, between winter 1917 and the outer ceiling. A season later than the ceiling's season SHALL be refused even when its year is the ceiling's year. A year SHALL be accepted when it is between 1917 and the outer ceiling's year, inclusive, since a year is addressable when any of its seasons is.

The horizon probe described in "MAL's forward season horizon" is the one fetch that deliberately reaches past the outer ceiling, and it is not one of these requests: no client names the season it fetches. The endpoint that triggers it SHALL take no season or year parameter and SHALL derive its target from the ceiling itself, so reaching past the range stays a property of that one fixed rule rather than something a caller can ask for.

The range SHALL be computed again for every request, from the current local date and the cache, and SHALL NOT be stored. It therefore moves forward with the calendar and with newly cached seasons. An unknown season name SHALL still be refused as before, and that check SHALL come first.

The earliest year the Season and Year pages' arrows and dropdowns offer is the earliest year of this range, so every season and year the API accepts is also reachable from the controls. A season or year linked from elsewhere in the app, such as an anime's detail page or a recap, SHALL be readable and refreshable on the same terms as one reached from the controls.

#### Scenario: The probe endpoint names no season
- **WHEN** the horizon probe is triggered by a page visit
- **THEN** the request carries no year or season, and the season fetched is derived server-side from the current ceiling

#### Scenario: A client still cannot reach past the ceiling
- **WHEN** a client requests the season the probe would target, by naming it on the season endpoints
- **THEN** the response is `400`, exactly as for any other season past the outer ceiling
