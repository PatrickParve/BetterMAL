## MODIFIED Requirements

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
