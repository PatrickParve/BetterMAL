# year-browser Specification

## Purpose
The year-browser capability lets a user browse all anime MAL classifies to a given year — winter, spring, summer, and fall combined — as one listing, without leaving my list unfiltered by browsing a full season at a time. It is derived entirely from the season browser's own cached listings: there is no year-level MAL request and no year-level cache. The Year page mirrors the season browser's presentation, sorting, filtering, and cache-first refresh behaviour at a year's grain instead of a season's.
## Requirements
### Requirement: A year is its four seasons combined
The system SHALL show, on the Year page, every anime whose MAL season classification (`start_season`) falls in any of the selected year's four seasons — winter, spring, summer, and fall of that year — as one combined listing, not four.

The year SHALL be derived from the cached season listings the season browser already maintains, and SHALL NOT be fetched, classified, or stored as a period of its own. There is no year-level MAL request and no year-level cache: selecting 2020 means reading winter 2020, spring 2020, summer 2020, and fall 2020 together.

Because the season browser files each anime under exactly one season, an anime SHALL appear exactly once in a year's listing. A long-running anime SHALL appear in the year it premiered and SHALL NOT reappear in later years it continued airing through.

#### Scenario: Combining a year's seasons
- **WHEN** I open the Year page on 2020
- **THEN** the anime cached under winter 2020, spring 2020, summer 2020, and fall 2020 are shown together in one grid

#### Scenario: No duplicates across the year
- **WHEN** a year's combined listing is shown
- **THEN** each anime appears exactly once, since MAL files it under a single season

#### Scenario: A long-running anime appears in its premiere year only
- **WHEN** an anime premiered in fall 2019 and continued airing throughout 2020
- **THEN** it appears on the 2019 Year page and not on the 2020 one

#### Scenario: A year with one season cached
- **WHEN** only summer 2020 has ever been cached and I open the Year page on 2020
- **THEN** summer 2020's anime are shown, rather than the page reporting the year as unavailable because the other three seasons hold nothing

### Requirement: Year selection
The system SHALL allow changing the selected year, and SHALL keep the selected year, sort, and filter state in the page's URL so it persists through back-navigation from an anime detail page. Opening the Year page from the navbar with no year specified SHALL default to the current year.

The system SHALL provide both previous/next arrows that step one year at a time and a dropdown that jumps directly to a year, so a year is reachable in one action rather than by repeated stepping.

Forward navigation SHALL stop at the navigable year ceiling defined by "The year ceiling follows the season horizon": at the ceiling the next-year control SHALL be disabled, and the dropdown SHALL NOT offer any year later than it. Backward navigation SHALL be unaffected, and SHALL be floored at the same earliest year the season browser's quick-jump offers. The API itself refuses a year outside the range the season browser defines (see the `season-browser` requirement "Requests outside the seasons MAL could list are refused"), so what these controls prevent reaching is also enforced server-side.

A year addressed directly in the URL SHALL be rendered as asked rather than silently rewritten, so an old link or a bookmark is never redirected; the dropdown SHALL keep that year among its options so the control always has a valid value, and forward movement from there SHALL remain blocked. A URL-addressed year past the navigable ceiling but inside the API's range SHALL render like any other year. A URL-addressed year outside that range SHALL still not be rewritten. The API refuses it, so no request reaches MAL, and the page SHALL show the state it shows for a year that could not be loaded.

#### Scenario: Changing year with the arrows
- **WHEN** I press the next-year arrow
- **THEN** the page shows the following year's anime and the selection is reflected in the URL

#### Scenario: Jumping to a year
- **WHEN** I choose a year from the dropdown
- **THEN** the page jumps directly to that year without stepping through the intervening years

#### Scenario: Selection persists through back-navigation
- **WHEN** I pick a year, open an anime, then press the browser Back button
- **THEN** I return to the year I had selected, not the current year

#### Scenario: Navbar defaults to the current year
- **WHEN** I open the Year page from the navbar with no year in the URL
- **THEN** it defaults to the current year

#### Scenario: Stepping forward stops at the ceiling
- **WHEN** I am viewing the furthest navigable year
- **THEN** the next-year control is disabled and I cannot step past it, while the previous-year control still works

#### Scenario: The dropdown does not offer years past the ceiling
- **WHEN** I open the year dropdown
- **THEN** it offers nothing later than the navigable year ceiling

#### Scenario: A URL past the ceiling still renders
- **WHEN** I open a bookmarked link to a year beyond the navigable ceiling that is still inside the range the API accepts
- **THEN** that year is shown as asked, reporting that MyAnimeList has not listed it, its own value is selectable in the dropdown, and forward navigation from it remains blocked

#### Scenario: A URL outside the accepted range is not rewritten
- **WHEN** I open a link to a year the API refuses, such as 9999
- **THEN** the URL is left as it is, the year stays selectable in the dropdown, no request reaches MAL, and the page shows its could-not-be-loaded state

### Requirement: The year ceiling follows the season horizon
The furthest year the user may navigate to SHALL be the year of the season browser's navigable ceiling — a year is navigable exactly when any season in it is. The system SHALL NOT compute a second, independent horizon for years: the season horizon's rules (MAL's forward window, a wider window once observed, and today's observed `404`s) decide both.

Until the ceiling is known, the Year page SHALL assume a default that matches MAL's forward window rather than disabling navigation, so nothing is blocked while the ceiling is being resolved, and SHALL keep that assumption if the ceiling cannot be resolved at all.

#### Scenario: The ceiling year tracks the season ceiling
- **WHEN** the season browser's navigable ceiling is winter 2027
- **THEN** the Year page's furthest navigable year is 2027

#### Scenario: Navigation is not blocked while the ceiling resolves
- **WHEN** I open the Year page before the ceiling has been resolved
- **THEN** year navigation is available against a default ceiling consistent with MAL's forward window, rather than disabled

#### Scenario: The ceiling cannot be resolved
- **WHEN** the request for the navigable ceiling fails
- **THEN** the page keeps its default ceiling and remains navigable, and no error is surfaced

### Requirement: Cache-first read with visit-triggered refresh of the year's seasons
The system SHALL render the Year page from the cached listings first, and SHALL refresh that year's four seasons from MAL in the background, updating the page in place once the refresh completes.

Each of the four seasons SHALL be refreshed by the same mechanism a season-page visit uses, and SHALL be subject to the same rules: at most one successful fetch per season per **that season's own age-based interval** (see the `season-browser` capability), at most one refresh per season in flight at a time, and a failed fetch not counting toward the interval. A season already fetched within its interval — whether by this year's refresh, an earlier year visit, or a season-page visit — SHALL NOT be fetched again. Refreshing a year SHALL therefore never cost more MAL requests than visiting its four season pages would have.

Because a year's four seasons start three months apart, they can fall in **different age tiers**, and a year visit SHALL refresh each season on its own interval rather than treating the year as a single unit. A year may therefore refresh some of its seasons and skip others in the same visit — for instance a year whose fall season is still under a year old while its winter season has passed that boundary. The year SHALL NOT have an interval of its own: there is no year-level fetch stamp, and the year's behaviour is entirely the sum of its four seasons'.

The refresh SHALL be triggered only by a change of the selected year, never by a change of sort or filter, never by a timer or schedule, and never by a user-facing refresh control. Stepping quickly through years with the arrows SHALL refresh only the year settled on.

A failed refresh SHALL leave the cached listing and the displayed page intact and SHALL NOT be surfaced as a page error.

#### Scenario: Cached year renders before the refresh completes
- **WHEN** I open a year whose seasons already have cached listings
- **THEN** the cached anime are displayed immediately without waiting for any MAL request

#### Scenario: Background refresh updates the page
- **WHEN** a year's background refresh finishes with new data
- **THEN** the displayed results are re-read from the cache and updated in place, preserving my scroll position and the number of pages I had already loaded

#### Scenario: Refresh in progress is visible
- **WHEN** a background refresh is running for the year I am viewing
- **THEN** the page shows a passive updating indicator, and it disappears when the refresh completes

#### Scenario: Seasons still fresh for their age are not refetched
- **WHEN** I open a year whose four seasons were all fetched within their own intervals
- **THEN** no MAL request is made for any of them and the page is served from the cache

#### Scenario: A year straddling an age boundary refreshes only part of itself
- **WHEN** I open a year whose fall season started eleven months ago and whose winter season started twenty months ago, and every season was fetched two local days ago
- **THEN** fall is refreshed, because its interval is one day, and winter, spring, and summer are skipped, because theirs is three days and only two have passed

#### Scenario: An old year costs fewer requests than a recent one
- **WHEN** I open a year whose four seasons all started more than five years ago and were all fetched six local days ago
- **THEN** no MAL request is made for any of them, where the same visit to a current year would have refreshed all four

#### Scenario: A season-page visit satisfies the year's refresh
- **WHEN** I browse summer 2020 on the season page and then open the Year page on 2020 the same day
- **THEN** summer 2020 is not fetched a second time, and only the year's other three seasons are considered for fetching

#### Scenario: Stepping quickly through years
- **WHEN** I step through several years in quick succession with the arrows
- **THEN** only the year I settle on is refreshed, and the years I passed through are not

#### Scenario: Changing sort or filter does not refetch
- **WHEN** I change the sort order, the type filter, or the "In my list" checkbox without changing the year
- **THEN** the results are re-read from the cache and no MAL fetch is started

#### Scenario: Failed refresh keeps the cached page
- **WHEN** a year's background refresh fails
- **THEN** the page keeps showing the cached listings and the failure is not surfaced as a page error

#### Scenario: A failed season does not consume its interval
- **WHEN** one of a year's seasons fails to fetch and I open that year again the same day
- **THEN** that season is retried, because only a successful fetch counts toward its interval

### Requirement: A year refresh reports one combined outcome
A year's refresh SHALL report a single outcome, folded from its four seasons' outcomes, so the page can decide what to render without reasoning about seasons it does not display. The fold SHALL be, in order of precedence:

- **fetched** — at least one season fetched anime from MAL;
- otherwise **skipped** — at least one season was already fresh enough for its age;
- otherwise **notListed** — MAL reported no listing for any season of the year;
- otherwise **failed** — every season's fetch failed.

The precedence SHALL make each year outcome mean for a year what the corresponding season outcome means for a season: new data warrants a re-read; a year with any season still current is current, not unlisted and not broken; a year MAL has opened no part of is honestly unlisted; and only a year where nothing at all succeeded is reported as failed.

#### Scenario: One season brings new data
- **WHEN** one of a year's seasons fetches new anime and the other three were still fresh
- **THEN** the year's outcome is `fetched` and the page re-reads its results

#### Scenario: Nothing to do
- **WHEN** every season of a year was already fetched within its own interval
- **THEN** the year's outcome is `skipped` and no MAL request is made

#### Scenario: A year MAL has not opened
- **WHEN** MAL reports no listing for any of a future year's four seasons
- **THEN** the year's outcome is `notListed`

#### Scenario: A partly-unlisted year is not reported as unlisted
- **WHEN** two of a year's seasons return anime and the other two are not yet listed by MAL
- **THEN** the year's outcome is `fetched`, not `notListed`

#### Scenario: Every season failed
- **WHEN** all four of a year's season fetches fail
- **THEN** the year's outcome is `failed`

#### Scenario: A partial failure is not reported as a failure
- **WHEN** three of a year's seasons fetch successfully and one fails
- **THEN** the year's outcome is `fetched`, the fetched anime are shown, and no error is surfaced

### Requirement: The Year page states which empty situation it is in
A year with no anime to show SHALL say which of three situations it is in: MAL has no listing for any of its seasons yet, listings exist but nothing matches the current filters, or its first fetch failed and will be retried on the next visit.

The page SHALL leave its never-cached loading state once a refresh attempt has settled, whatever its outcome, so a year with nothing cached can never be left loading indefinitely. The failure state SHALL be shown only for a year with no cached listing at all — a year that already has cached anime continues to render them with no error.

#### Scenario: Empty cache waits for the first refresh
- **WHEN** I open a year whose seasons have never been cached
- **THEN** the page shows a loading state until the refresh attempt settles, rather than reporting that no anime were found

#### Scenario: A year MAL has not listed
- **WHEN** I open a year that has never been cached and MAL reports no listing for any of its seasons
- **THEN** the loading state ends and the page says MyAnimeList has not listed that year yet

#### Scenario: A year that could not be loaded
- **WHEN** I open a year that has never been cached and its refresh fails
- **THEN** the loading state ends and the page says the year could not be loaded and will be retried

#### Scenario: A cached year with no matching filters
- **WHEN** I open a year that has cached listings and my current filters exclude every anime in it
- **THEN** the page reports that no anime match, not that MAL has no listing for the year

### Requirement: Year sorting
The system SHALL allow sorting the year's anime by popularity, MAL score, alphabetically, and my score, using the same rules and the same labels the season browser uses, applied across the whole year at once rather than within each season.

Sorting by popularity SHALL place unranked anime — MAL popularity rank absent or zero — after all ranked anime, then ranked anime by ascending rank, then by title. Sorting by my score SHALL split the results into two ordered groups: first every anime I have scored, by my score descending — ties broken by my ranking, best-ranked first, per the `anime-ranking` capability, then by popularity, then by title for anime my ranking does not cover; then every remaining anime by popularity under the same unranked-last rule. The page SHALL render a visual break labelled `Unwatched` between the two groups, shown only when both groups have at least one anime.

Every one of these orderings SHALL be produced server-side over the year's whole listing and SHALL reach the page as a per-anime ordering position rather than as a rule the page applies itself, exactly as the season browser's do. Changing the sort SHALL therefore take effect immediately, with no read and no loading state, and SHALL count as a fresh view of the page, returning the grid to the top on its first screenful.

#### Scenario: Sorting across the whole year
- **WHEN** I sort a year by MAL score
- **THEN** the highest-scored anime of the year leads, regardless of which of the year's seasons it aired in

#### Scenario: Sorting by my score
- **WHEN** I sort a year by my score
- **THEN** the anime I have scored appear first in descending score order, followed by an `Unwatched` divider, followed by the remaining anime in popularity order

#### Scenario: Equal scores follow my ranking
- **WHEN** I sort a year by my score and several of its anime share a score
- **THEN** they appear in my ranking's order rather than in popularity order

#### Scenario: My-score grouping holds all the way down
- **WHEN** I sort a year by my score and scroll to the end of the year
- **THEN** no scored anime appears after the `Unwatched` divider, anywhere in the grid

#### Scenario: No scored anime in the year
- **WHEN** I sort by my score in a year where I have scored nothing
- **THEN** all anime are shown in popularity order with no `Unwatched` divider

#### Scenario: Unranked anime sort last by popularity
- **WHEN** I sort a year by popularity and some anime have no popularity rank
- **THEN** the ranked anime appear first in ascending rank order and the unranked anime appear last

#### Scenario: Sorting is instant
- **WHEN** I change the sort on a year that is already loaded
- **THEN** the grid re-orders immediately with no read and no loading state

#### Scenario: A sort is still a fresh view
- **WHEN** I change the sort after scrolling deep into a year
- **THEN** the re-sorted grid opens at the top on its first screenful

### Requirement: Year type and in-my-list filters
The system SHALL provide, beside the Year page's sort control, the same two filters the season browser provides: a multi-select Type filter using the same control and display labels, offering only the media types actually present in the year; and an "In my list" checkbox, checked by default, which when unchecked excludes every anime that has an entry in my list.

The Type filter SHALL distinguish **All** from **None** exactly as the season browser's does: on **All** — its state on a fresh visit — no type restriction applies; on **None**, no anime passes the type filter and the page SHALL report that no anime match the current filters rather than that the year has no listing. **All**, **None**, and a partial selection SHALL each be distinctly representable in the page's URL state, with a URL naming no type filter meaning **All**.

Both filters SHALL be applied to the year's already-loaded listing rather than through a read, so each takes effect immediately with no loading state, and each SHALL exclude exactly the anime it excludes today, with the count the page reports following the filtered set. The types offered SHALL be derived from the whole listing rather than from what the current selection leaves visible. Both SHALL be part of the page's URL state so they survive back-navigation, and toggling either SHALL count as a fresh view of the page, returning the grid to the top on its first screenful.

#### Scenario: Filtering a year to one type
- **WHEN** I select Movie in the Year page's type filter
- **THEN** only movie entries are shown for that year, and the count the page reports reflects only movies

#### Scenario: The filter holds all the way down
- **WHEN** I select a type and scroll to the end of the year
- **THEN** every card shown is of a matching type

#### Scenario: Only present types are offered
- **WHEN** a year's results contain no music videos
- **THEN** the type filter does not offer Music as an option for that year

#### Scenario: Selecting a type does not shrink the type options
- **WHEN** I select one type and the results narrow to it
- **THEN** the type filter still offers the year's other types, so I can change or clear my selection

#### Scenario: Both filters are instant
- **WHEN** I change the type selection or toggle "In my list" on a year that is already loaded
- **THEN** the grid updates immediately with no read and no loading state

#### Scenario: All applies no type restriction
- **WHEN** the Year page's type filter is on All
- **THEN** entries of every type are shown

#### Scenario: None empties the grid and says so
- **WHEN** I press **None** in the Year page's type filter
- **THEN** no cards are shown and the page reports that no anime match the current filters, rather than that the year is not listed

#### Scenario: Excluding anime in my list
- **WHEN** I uncheck "In my list"
- **THEN** every anime that has an entry in my list is removed from the results, the reported count reflects the smaller set, and scrolling continues to show only anime not yet in my list

#### Scenario: Filters persist through back-navigation
- **WHEN** I set a type filter, uncheck "In my list", open an anime, then press the browser Back button
- **THEN** both selections are still applied and the filtered results are shown

#### Scenario: None persists through back-navigation
- **WHEN** I press **None**, navigate away, and press the browser Back button
- **THEN** the filter is still on **None** rather than back on All

### Requirement: Year page presentation matches the season page
The Year page SHALL present its results using the same card content, the same grid, and the same header arrangement as the season browser, so the two pages differ only in the period they cover and the controls that select it.

Each card SHALL show the anime's title, picture, type and episode count at the leading edge of its meta line, and its MAL score at the trailing edge of that same line, under the same rules — `?` for an unknown episode count, nothing in the score slot for an anime with no MAL score, and no score furniture at all while the global hide-scores toggle is on.

The results grid SHALL span the full content width with no unused right-hand gutter, at a card count per row that is fixed at ordinary desktop widths and adapts below the mobile breakpoint. The header SHALL place the year step navigation in the horizontal center, the year dropdown immediately to its right, and the sort and filter controls after them.

Results SHALL load and reveal exactly as the season page's do: the year's whole listing — the union of its four seasons under the selected sort and filters — is read in one request and revealed progressively as the grid is scrolled, with nothing capping how many of the year's anime can be reached, no request issued to reveal more, the revealed count restored on a back/forward navigation, a fresh visit opening on the first screenful, and no refresh able to leave the grid showing fewer anime than it was showing.

#### Scenario: Rendering a year card
- **WHEN** a year's anime are displayed
- **THEN** each card shows its title, picture, type and episode count at the start of its meta line, and its MAL score at the end of that same line

#### Scenario: Hiding scores removes the card score entirely
- **WHEN** the global hide-scores toggle is on
- **THEN** no Year page card shows a MAL score, a placeholder, or a reveal control, and the type and episode count are unmoved

#### Scenario: The grid fills the content width
- **WHEN** year results are displayed at any window width
- **THEN** the cards in each full row together span the content width, with no large empty space to the right of the grid

#### Scenario: Header control placement
- **WHEN** I view the Year page header
- **THEN** the arrows and year label sit centered with the year dropdown directly to their right, and the sort and filter controls after them

#### Scenario: Scrolling loads more
- **WHEN** I scroll to the end of the loaded year results
- **THEN** more results appear automatically

#### Scenario: A whole year is reachable
- **WHEN** I keep scrolling a year holding well over a thousand anime
- **THEN** I reach its last anime, with no ceiling short of the year's own size, and no read was issued to reveal them

#### Scenario: A restored year keeps what it had revealed
- **WHEN** I scroll a year well past its first screenful of cards, open one of them, and go back
- **THEN** the grid shows the same number of cards it showed when I left, both immediately and after its background refresh lands

#### Scenario: Returning to where I was in a year
- **WHEN** I go back to a year I had scrolled far down
- **THEN** the page is at the position I left it at and stays there once the refresh completes

#### Scenario: A refresh cannot shorten a scrolled grid
- **WHEN** I have scrolled a year deep into its listing and its visit-triggered refresh then completes
- **THEN** the grid still shows everything it was showing, rather than being cut back to a screenful

