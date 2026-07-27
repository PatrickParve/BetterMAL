## MODIFIED Requirements

### Requirement: Full season listing with live-then-cached fetch
The system SHALL show all anime whose MAL season classification (`start_season`) is the selected season (not just my list), fetching the season live from the API when it has never been fetched before and caching the results for subsequent visits. Season membership SHALL follow MAL's own `start_season` — the field MAL uses to build its per-season listings — which can differ from the calendar quarter the anime's start date falls in; the system SHALL NOT re-derive an anime's season from its start date. Each anime SHALL appear in exactly one season (the season MAL files it under); a long-running anime SHALL NOT appear in seasons after its premiere. The cached listing SHALL be the single source of truth read back to the page (no start-date re-filtering at read time).

Reading a season's page SHALL NOT block on a live fetch: the cached listing is served immediately, and any live fetch happens outside the read path (see "Cache-first read with visit-triggered background refresh"). Re-fetching a season that already has a cached listing SHALL add anime MAL now returns for that season and that are not yet cached, and SHALL update each returned anime's listing fields (title, picture, episode count, type, MAL score, popularity rank), so entries and scores missing or stale from an earlier fetch are corrected on the next refresh.

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

### Requirement: Season selection
The system SHALL allow changing the selected season, and SHALL keep the selected season, sort, and filter state in the page's URL so it persists through back-navigation from an anime detail page. Opening the Season page from the navbar with no season specified SHALL default to the current season. The system SHALL provide a quick-jump control to select a specific year and season directly, in addition to stepping one season at a time.

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

#### Scenario: Header control placement
- **WHEN** I view the season header
- **THEN** the arrows and season label sit centered in the header with the year/season quick-jump dropdowns directly to their right, rather than the quick-jump sitting alone at the far right edge

### Requirement: Season filtering
The system SHALL allow filtering/sorting season anime by popularity, score, alphabetical, and my score, where my score is meaningful only for anime also in my list. When sorting by popularity, anime that are unranked — MAL popularity rank absent or zero — SHALL be ordered after all ranked anime (a rank of `0` is not treated as "most popular"), then ranked anime by ascending rank (1 = most popular), then by title.

When sorting by my score, the results SHALL be split into two ordered groups: first every anime I have scored, ordered by my score descending (equal scores broken by popularity, then title); then every remaining anime, ordered by popularity using the same unranked-last rule as the popularity sort. The grouping SHALL be produced server-side so it holds across paginated loads, and the page SHALL render a visual break with the label `Unwatched` between the two groups, shown only when both groups have at least one anime.

#### Scenario: Sorting by my score
- **WHEN** I sort by my score
- **THEN** the anime I have scored appear first in descending score order, followed by an `Unwatched` divider, followed by the remaining anime in popularity order

#### Scenario: My-score grouping holds across pages
- **WHEN** I sort by my score and scroll far enough to load additional pages
- **THEN** no scored anime appears after the `Unwatched` divider — the grouping is consistent across every loaded page

#### Scenario: No scored anime in the season
- **WHEN** I sort by my score in a season where I have scored nothing
- **THEN** all anime are shown in popularity order with no `Unwatched` divider

#### Scenario: Unranked anime sort last by popularity
- **WHEN** I sort by popularity and some anime have no popularity rank (rank absent or zero)
- **THEN** the ranked anime appear first in ascending rank order and the unranked anime appear last, rather than an unranked anime sorting to the top

## ADDED Requirements

### Requirement: Cache-first read with visit-triggered background refresh
The system SHALL render the season page from the cached listing first and refresh that season from MAL in the background, updating the page in place once the refresh completes. Opening a season SHALL trigger a background refresh regardless of whether that season is past, current, or upcoming — refresh is driven by the user visiting a season, never by a timer or schedule, so that a deployment which is not running continuously never misses a refresh window.

A season SHALL be fetched at most once per local calendar day: if its last successful fetch falls on the current local date, revisiting it SHALL be served from cache with no MAL request, however many times it is opened. At most one refresh per season SHALL additionally be in flight at a time, so concurrent tabs collapse into a single fetch. A fetch that fails SHALL NOT count as the day's fetch — the next visit retries. The refresh SHALL be triggered only by a change of the selected season, not by changing the sort or filter controls. There SHALL be no user-facing control that triggers a refresh. A failed refresh SHALL leave the cached listing and the displayed page intact and SHALL NOT be surfaced as a page error.

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

### Requirement: In-my-list inclusion filter
The system SHALL provide an "In my list" checkbox beside the season sort control, checked by default. When it is unchecked, anime that have an entry in my list SHALL be excluded from the season results and from the result count used for paging, so only anime not yet in my list are shown. The checkbox state SHALL be part of the page's URL state and SHALL apply server-side so that paging and infinite scroll stay correct.

#### Scenario: Default includes my list
- **WHEN** I open a season
- **THEN** the "In my list" checkbox is checked and anime in my list are shown alongside the rest

#### Scenario: Excluding anime in my list
- **WHEN** I uncheck "In my list"
- **THEN** every anime that has an entry in my list is removed from the results, the result count reflects the smaller set, and scrolling continues to load only anime not in my list

#### Scenario: Filter persists through back-navigation
- **WHEN** I uncheck "In my list", open an anime, then press the browser Back button
- **THEN** the checkbox is still unchecked and the filtered results are shown

### Requirement: Season grid fills the content width
The season results grid SHALL size its cards so that each row spans the full width of the page content, leaving no unused gutter to the right of the last card in a row, and SHALL show cover images larger than the fixed-width browse card. The number of cards per row SHALL adapt to the available width.

#### Scenario: Grid leaves no right-hand gutter
- **WHEN** season results are displayed at any window width
- **THEN** the cards in each full row together span the content width, with no large empty space to the right of the grid

#### Scenario: Adapting to a narrower window
- **WHEN** I narrow the window
- **THEN** fewer cards are placed per row and the cards continue to fill the row width

## REMOVED Requirements

### Requirement: Daily refresh limited to current and upcoming seasons
**Reason**: Two separate defects. The once-per-local-day refresh ran inside the page read, so the page blocked on MAL to render. And restricting refresh to the current/upcoming season froze every past season permanently — not only its membership but its cached MAL scores and popularity ranks, which no other job updates for anime outside my list (`MetadataRefreshService` is my-list-only and score-only), so score and popularity sorts on a past season ranked by values captured the day it was first cached. It is replaced by "Cache-first read with visit-triggered background refresh", which refreshes any season on visit, off the read path.

**Migration**: No data migration and no change to `SeasonFetchLog`. The once-per-local-day comparison is retained verbatim; what is removed is the season-class gate that sat in front of it, so the daily rule now applies to every season instead of only the current and upcoming ones. A season last fetched on an earlier date refreshes on its next visit; one already fetched on the current local date refreshes the following day.
