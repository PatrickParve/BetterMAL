## MODIFIED Requirements

### Requirement: Year selection
The system SHALL allow changing the selected year, and SHALL keep the selected year, sort, and filter state in the page's URL so it persists through back-navigation from an anime detail page. Opening the Year page from the navbar with no year specified SHALL default to the current year.

The system SHALL provide both previous/next arrows that step one year at a time and a dropdown that jumps directly to a year, so a year is reachable in one action rather than by repeated stepping.

Forward navigation SHALL stop at the navigable year ceiling defined by "The year ceiling follows the season horizon": at the ceiling the next-year control SHALL be disabled, and the dropdown SHALL NOT offer any year later than it. Backward navigation SHALL be unaffected, and SHALL be floored at the same earliest year the season browser's quick-jump offers — the earliest year in MyAnimeList's season archive, which is also the earliest year the API accepts.

The dropdown's year list SHALL be derived from the addressable range described below and SHALL NOT be widened, lengthened or otherwise sized by the year currently in the URL. No part of rendering the page SHALL allocate per-year or per-option work proportional to a value taken from the URL.

**The addressable range.** A year SHALL be addressable when it falls between the archive's earliest year and the navigable year ceiling, inclusive — that is, a URL SHALL reach exactly what the arrows and the dropdown offer and nothing further, placing the furthest addressable year at most one year ahead of the current one. There SHALL NOT be a second, wider ceiling for URLs.

Because the ceiling never falls below the current year, a year between the archive's earliest year and the current year SHALL be admitted immediately, without waiting on the ceiling. A later year SHALL be admitted or replaced only once the ceiling is known; until then the page SHALL render nothing and SHALL request nothing for it. If the ceiling cannot be determined, the year of the current season plus MAL's forward season window SHALL be assumed.

A year addressed directly in the URL that **is** addressable SHALL be rendered as asked rather than silently rewritten, so an old link or a bookmark is never redirected, and forward movement from there SHALL remain blocked.

**The one year a URL may ask about.** A URL addressing the year that **contains** the horizon probe's target (see the `season-browser` requirement "MAL's forward season horizon") SHALL trigger that probe and be decided on its result: if MAL returns anime for the probed season, it is cached, the ceiling rises into that year, and the page renders it; if not, the year is replaced like any other past the ceiling. The probe's once-per-local-day gate SHALL still apply; its last-month window SHALL NOT. Any year later than the one containing the probe's target — and the same year when the probe's target lies in a year already addressable — SHALL be replaced with no request of any kind.

A year addressed in the URL that is **not** addressable, and a URL whose year parameter is absent, malformed or not a whole number, SHALL be replaced with the current year. The replacement SHALL take effect before the page requests anything: no cached read, no refresh, no MAL request and no fetch-log write SHALL be made for the rejected year. The replacement SHALL substitute the current entry in the browser's history rather than adding one, so going back does not return to the rejected URL, and SHALL preserve every other parameter in the query string. The system SHALL NOT show a message, an error state or any other notice about the replacement.

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

#### Scenario: Stepping back stops at the archive's earliest year
- **WHEN** I am viewing the archive's earliest year
- **THEN** the previous-year control is disabled and I cannot step past it

#### Scenario: The dropdown reaches the whole archive
- **WHEN** I open the year dropdown
- **THEN** it offers every year from the archive's earliest year through the ceiling, so a year in the 1920s is reachable without typing a URL

#### Scenario: The dropdown does not offer years past the ceiling
- **WHEN** I open the year dropdown
- **THEN** it offers nothing later than the navigable year ceiling

#### Scenario: Addressing the year holding the probe's target checks it
- **WHEN** the probe's target season falls in the year after the ceiling's, I open that year, and MAL now lists that season
- **THEN** it is fetched and cached, the ceiling moves into that year, and the year is shown as asked

#### Scenario: That year is replaced when MAL still has nothing
- **WHEN** I open that year and the probed season is answered `404`
- **THEN** the current year is shown with the URL replaced and the ceiling is unchanged

#### Scenario: A year not holding the probe's target is replaced outright
- **WHEN** I open a year past the ceiling that does not contain the probe's target
- **THEN** the current year is shown with the URL replaced and no request is made

#### Scenario: A URL past the ceiling is replaced
- **WHEN** I open a bookmarked link to a year beyond the navigable year ceiling
- **THEN** the current year is shown with the URL replaced, exactly as for any other year the controls do not offer

#### Scenario: A future year admitted only once the ceiling is known
- **WHEN** I open a link to a year later than the current one
- **THEN** nothing is read or refreshed for it until the ceiling is known, and it is then either shown or replaced with the current year

#### Scenario: The ceiling rises when MAL opens a further season
- **WHEN** a visit to the furthest addressable year caches a season MAL has opened beyond its default forward window, raising the ceiling
- **THEN** the following year becomes addressable by URL as soon as the dropdown offers it, with no separate rule for the two

#### Scenario: A year before the archive is replaced
- **WHEN** I open a link to a year earlier than the archive's earliest year, such as 1916
- **THEN** the page shows the current year, the URL is replaced with it, no message is shown, and no read, refresh or MAL request is made for 1916

#### Scenario: An impossible year is replaced without rendering it
- **WHEN** I open `/year?year=32932734`
- **THEN** the page shows the current year with its URL replaced, no message is shown, no request is made, and the dropdown is built from the addressable range rather than from 32932734 — so nothing is allocated per year between the archive and it

#### Scenario: A malformed year is replaced
- **WHEN** I open a link whose year parameter is not a whole number
- **THEN** the page shows the current year and the URL is replaced with it

#### Scenario: A replacement does not leave a step back into the bad URL
- **WHEN** a year I addressed is replaced with the current year and I press the browser Back button
- **THEN** I go back to wherever I came from, not to the rejected year

#### Scenario: A replacement keeps the rest of the query
- **WHEN** the link I opened is out of range but also carries a sort and a type filter
- **THEN** the current year is shown with that sort and type filter still applied

### Requirement: Year sorting
The system SHALL allow sorting the year's anime by popularity, MAL score, alphabetically, and my score, using the same rules and the same labels the season browser uses, applied across the whole year at once rather than within each season.

Every ordering that reads a title — the alphabetical sort itself and every title tie-break — SHALL use the **displayed title**: the anime's English title when MyAnimeList has one, and its original title otherwise, exactly as the season browser does, so the order matches the titles the cards show.

Sorting by popularity SHALL place unranked anime — MAL popularity rank absent or zero — after all ranked anime, then ranked anime by ascending rank, then by displayed title. Sorting by my score SHALL split the results into two ordered groups: first every anime I have scored, by my score descending — ties broken by my ranking, best-ranked first, per the `anime-ranking` capability, then by popularity, then by displayed title for anime my ranking does not cover; then every remaining anime by popularity under the same unranked-last rule. The page SHALL render a visual break labelled `Unwatched` between the two groups, shown only when both groups have at least one anime.

Every one of these orderings SHALL be produced server-side over the year's whole listing and SHALL reach the page as a per-anime ordering position rather than as a rule the page applies itself, exactly as the season browser's do. Changing the sort SHALL therefore take effect immediately, with no read and no loading state, and SHALL count as a fresh view of the page, returning the grid to the top on its first screenful.

#### Scenario: Alphabetical follows the displayed title
- **WHEN** I sort a year alphabetically and it contains an anime whose English title begins with a different letter than its original title
- **THEN** it is placed by the English title, the one its card shows

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

### Requirement: Cache-first read with visit-triggered refresh of the year's seasons
The system SHALL render the Year page from the cached listings first, and SHALL refresh in the background those of that year's four seasons that fall within the range MAL could list (see the `season-browser` requirement "Requests outside the seasons MAL could list are refused"), updating the page in place once the refresh completes. A season of that year beyond the outer ceiling SHALL NOT be fetched: MAL cannot have opened it, so asking would spend a request on a certain `404`. Discovering that MAL has opened a further season is the horizon probe's job, not the year refresh's (see the `season-browser` requirement "MAL's forward season horizon").

Because a year is navigable only when at least one of its seasons is within that range, at least one season SHALL always be refreshed for a year the page can display; a year that somehow yields no season to refresh SHALL be reported as not listed rather than as failed.

Each season refreshed SHALL use the same mechanism a season-page visit uses, and SHALL be subject to the same rules: at most one successful fetch per season per **that season's own age-based interval** (see the `season-browser` capability), at most one refresh per season in flight at a time, and a failed fetch not counting toward the interval. A season already fetched within its interval — whether by this year's refresh, an earlier year visit, or a season-page visit — SHALL NOT be fetched again. Refreshing a year SHALL therefore never cost more MAL requests than visiting its season pages would have.

Because a year's four seasons start three months apart, they can fall in **different age tiers**, and a year visit SHALL refresh each season on its own interval rather than treating the year as a single unit. A year may therefore refresh some of its seasons and skip others in the same visit — for instance a year whose fall season is still under a year old while its winter season has passed that boundary. The year SHALL NOT have an interval of its own: there is no year-level fetch stamp, and the year's behaviour is entirely the sum of its seasons'.

The refresh SHALL be triggered only by a change of the selected year, never by a change of sort or filter, never by a timer or schedule, and never by a user-facing refresh control. Stepping quickly through years with the arrows SHALL refresh only the year settled on.

A failed refresh SHALL leave the cached listing and the displayed page intact and SHALL NOT be surfaced as a page error.

#### Scenario: A future year does not ask about seasons MAL cannot have opened
- **WHEN** I open the ceiling's year and only its winter season is within the range MAL could list
- **THEN** winter is refreshed and no MAL request is made for that year's spring, summer or fall

#### Scenario: A past year still refreshes all four seasons
- **WHEN** I open a year that is wholly in the past
- **THEN** all four of its seasons are considered for refresh exactly as before, each on its own interval

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
- **WHEN** I open a year whose seasons were all fetched within their own intervals
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
A year's refresh SHALL report a single outcome, folded from the outcomes of the seasons it refreshed, so the page can decide what to render without reasoning about seasons it does not display. The fold SHALL be, in order of precedence:

- **fetched** — at least one season fetched anime from MAL;
- otherwise **skipped** — at least one season was already fresh enough for its age;
- otherwise **notListed** — MAL reported no listing for any season considered, or no season of the year was within the range MAL could list;
- otherwise **failed** — every season's fetch failed.

The precedence SHALL make each year outcome mean for a year what the corresponding season outcome means for a season: new data warrants a re-read; a year with any season still current is current, not unlisted and not broken; a year MAL has opened no part of is honestly unlisted; and only a year where nothing at all succeeded is reported as failed. A season left out because it is beyond the outer ceiling SHALL contribute no outcome of its own, and SHALL NOT make the year read as failed.

#### Scenario: One season brings new data
- **WHEN** one of a year's seasons fetches new anime and the others were still fresh
- **THEN** the year's outcome is `fetched` and the page re-reads its results

#### Scenario: Nothing to do
- **WHEN** every season of a year was already fetched within its own interval
- **THEN** the year's outcome is `skipped` and no MAL request is made

#### Scenario: A year MAL has not opened
- **WHEN** MAL reports no listing for any of a future year's seasons within the range
- **THEN** the year's outcome is `notListed`

#### Scenario: A partly-unlisted year is not reported as unlisted
- **WHEN** two of a year's seasons return anime and the other two are not yet listed by MAL
- **THEN** the year's outcome is `fetched`, not `notListed`

#### Scenario: Every season failed
- **WHEN** all of a year's considered season fetches fail
- **THEN** the year's outcome is `failed`

#### Scenario: A partial failure is not reported as a failure
- **WHEN** three of a year's seasons fetch successfully and one fails
- **THEN** the year's outcome is `fetched`, the fetched anime are shown, and no error is surfaced

#### Scenario: Skipped-as-out-of-range is not a failure
- **WHEN** a year's spring, summer and fall lie beyond the outer ceiling and its winter is fetched successfully
- **THEN** the year's outcome is `fetched`, decided by winter alone
