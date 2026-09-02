# season-browser Specification

## Purpose
TBD - created by archiving change bootstrap-anime-tracker. Update Purpose after archive.
## Requirements
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

#### Scenario: Grid leaves no right-hand gutter
- **WHEN** season results are displayed at any window width
- **THEN** the cards in each full row together span the content width, with no large empty space to the right of the grid

#### Scenario: Adapting to a narrower window
- **WHEN** I narrow the window
- **THEN** fewer cards are placed per row and the cards continue to fill the row width

#### Scenario: Card count stays fixed across ordinary desktop widths
- **WHEN** the page's effective width changes slightly at ordinary desktop sizes (e.g. connecting or disconnecting an external display changes the browser's logical resolution, but the window is still a normal desktop width)
- **THEN** the grid continues to show the same number of cards per row at essentially the same size, rather than jumping to one fewer or one more card per row

#### Scenario: Page fills the display instead of showing a boxed layout
- **WHEN** the browser window is wider than the page's previous fixed design width (an external monitor, or a laptop's own unscaled display)
- **THEN** the page content still fills the available width edge to edge, rather than sitting in a centered column with empty space and a visible border on both sides

### Requirement: Season filtering
The system SHALL allow filtering/sorting season anime by popularity, score, alphabetical, and my score, where my score is meaningful only for anime also in my list. When sorting by popularity, anime that are unranked — MAL popularity rank absent or zero — SHALL be ordered after all ranked anime (a rank of `0` is not treated as "most popular"), then ranked anime by ascending rank (1 = most popular), then by title.

When sorting by my score, the results SHALL be split into two ordered groups: first every anime I have scored, ordered by my score descending — equal scores broken by my ranking, best-ranked first, per the `anime-ranking` capability, then by popularity, then by title for anime my ranking does not cover; then every remaining anime, ordered by popularity using the same unranked-last rule as the popularity sort. The page SHALL render a visual break with the label `Unwatched` between the two groups, shown only when both groups have at least one anime.

Every one of these orderings SHALL be **produced server-side**, over the season's whole listing, and SHALL reach the page as a per-anime ordering position rather than as a rule the page applies itself — so the page can re-sort what it has already loaded without any ordering rule being expressed twice, and so the order it shows is by construction the order the server computed.

Changing the sort SHALL therefore take effect **immediately**, with no read, no loading state, and the same results and order the same sort produces today. It SHALL still count as a fresh view of the page: the grid SHALL return to the top, showing its first screenful.

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
The system SHALL provide a multi-select Type filter beside the season sort control, using the same control and display labels as My List's type filter, offering only the media types actually present in the season. Selecting one or more types SHALL restrict the season results, and the count the page reports, to matching types; with none selected, no type restriction applies. The selection state SHALL be part of the page's URL state so it survives back-navigation.

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

#### Scenario: Clearing the type filter
- **WHEN** no type is selected in the season page's type filter
- **THEN** entries of every type are shown

#### Scenario: Filter persists through back-navigation
- **WHEN** I select a type, open an anime, then press the browser Back button
- **THEN** the same type selection is still applied and the filtered results are shown

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
The system SHALL provide a persisted user setting, presented on the Settings page as a "Hide NSFW" checkbox and **unchecked by default**, that excludes hentai from the season browser's results and from the year browser's results — the two pages read the same cached season listings, at a season's grain and at a year's, so the exclusion follows the data rather than the page.

The filter SHALL exclude hentai and nothing else: an anime SHALL be treated as hentai when, and only when, MAL's own `rating` field for it is `rx`. Every other rating — including `r` and `r+` — SHALL remain visible with the setting enabled, so mature and ecchi titles are not swept up by it. An anime whose rating MAL has not yet reported (not yet cached) SHALL be treated as not hentai, so the filter never hides a title it cannot positively identify.

The filter SHALL be applied server-side, before the result count is computed, so the displayed count and infinite scroll stay correct on both pages. It SHALL apply to the season and year browsers only: search results, my list, top anime, the airing schedule, the home dashboard, and anime detail pages SHALL be unaffected by it. The setting SHALL persist across reloads and new tabs, and SHALL take effect on either page without requiring a MAL refetch.

#### Scenario: Default is off
- **WHEN** I open the Settings page without ever having changed this setting
- **THEN** the "Hide NSFW" checkbox is unchecked, and the season page shows hentai alongside everything else

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
