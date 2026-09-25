## MODIFIED Requirements

### Requirement: Cache-first read with visit-triggered refresh of the year's seasons
The system SHALL render the Year page from the cached listings first, and SHALL refresh in the background those of that year's four seasons that fall within the range MAL could list (see the `season-browser` requirement "Requests outside the seasons MAL could list are refused"), updating the page in place once the refresh completes. A season of that year beyond the outer ceiling SHALL NOT be fetched: MAL cannot have opened it, so asking would spend a request on a certain `404`. Discovering that MAL has opened a further season is the horizon probe's job, not the year refresh's (see the `season-browser` requirement "MAL's forward season horizon").

Because a year is navigable only when at least one of its seasons is within that range, at least one season SHALL always be refreshed for a year the page can display; a year that somehow yields no season to refresh SHALL be reported as not listed rather than as failed.

Each season refreshed SHALL use the same mechanism a season-page visit uses, and SHALL be subject to the same rules: at most one successful fetch per season per **that season's own age-based interval** (see the `season-browser` capability), at most one refresh per season in flight at a time, and a failed fetch not counting toward the interval. A season already fetched within its interval — whether by this year's refresh, an earlier year visit, or a season-page visit — SHALL NOT be fetched again. Refreshing a year SHALL therefore never cost more MAL requests than visiting its season pages would have.

Because a year's four seasons start three months apart, they can fall in **different age tiers**, and a year visit SHALL refresh each season on its own interval rather than treating the year as a single unit. A year may therefore refresh some of its seasons and skip others in the same visit — for instance a year whose fall season is still under a year old while its winter season has passed that boundary. The year SHALL NOT have an interval of its own: there is no year-level fetch stamp, and the year's behaviour is entirely the sum of its seasons'.

The refresh SHALL be triggered only by a change of the selected year, never by a change of sort or filter, never by a timer or schedule, and never by a user-facing refresh control. Stepping quickly through years with the arrows SHALL refresh only the year settled on.

The visit's refresh SHALL be requested once the page's own read of the year's cached listings has settled successfully, and its outcome SHALL be judged against what that read found, exactly as the `season-browser` capability requires of a season. A skipped refresh SHALL lead to a further read only when the settled read found nothing cached for any of the year's seasons; otherwise the year's listing SHALL be read once per visit. No refresh SHALL be requested for a visit whose read has not succeeded. A read that comes back from a back/forward restore snapshot has already settled, so its refresh SHALL be requested at once.

The passive updating indicator SHALL follow the `page-load-states` capability, appearing only once a refresh has been running for that capability's delay.

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

#### Scenario: A year already fresh opens without an error flash
- **WHEN** I open a year whose seasons all have cached listings and are all within their intervals, so its refresh is skipped almost at once
- **THEN** the page goes from its header straight to the grid, never showing a "could not be loaded", "not listed" or "no anime match" message and never showing the updating indicator, and the year's listing is read once

#### Scenario: Background refresh updates the page
- **WHEN** a year's background refresh finishes with new data
- **THEN** the displayed results are re-read from the cache and updated in place, preserving my scroll position and the number of pages I had already loaded

#### Scenario: Refresh in progress is visible
- **WHEN** a background refresh for the year I am viewing has been running for longer than the loading indicator's delay
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

### Requirement: The Year page states which empty situation it is in
A year with no anime to show SHALL say which of three situations it is in: MAL has no listing for any of its seasons yet, listings exist but nothing matches the current filters, or its first fetch from MyAnimeList failed and will be retried on the next visit. None of the three SHALL be shown until the page's own read of the year's cached listings has settled.

The page SHALL leave its never-cached loading state once a refresh attempt has settled, whatever its outcome, so a year with nothing cached can never be left loading indefinitely. While a year with nothing cached waits on its first fetch, its loading state SHALL say that the year is being fetched from MyAnimeList. The MyAnimeList failure state SHALL be shown only for a year with no cached listing at all — a year that already has cached anime continues to render them with no error — and SHALL offer no Try again control, since no control may trigger a refresh.

A failure of the page's own read of the cached listings, for example because the app's server cannot be reached, SHALL be presented as the `page-load-states` capability's failure state, with Try again and the automatic retry when the server is reachable again.

#### Scenario: Empty cache waits for the first refresh
- **WHEN** I open a year whose seasons have never been cached
- **THEN** the page shows a loading state saying the year is being fetched from MyAnimeList until the refresh attempt settles, rather than reporting that no anime were found

#### Scenario: A year MAL has not listed
- **WHEN** I open a year that has never been cached and MAL reports no listing for any of its seasons
- **THEN** the loading state ends and the page says MyAnimeList has not listed that year yet

#### Scenario: A year that could not be loaded
- **WHEN** I open a year that has never been cached and its refresh fails
- **THEN** the loading state ends and the page says the year could not be fetched from MyAnimeList and will be retried next time, with no Try again control

#### Scenario: The page's own read fails
- **WHEN** I open a year while the app's server cannot be reached
- **THEN** the page shows the failure state with Try again, no refresh is requested, and once the server is reachable again the page reads the year by itself

#### Scenario: A cached year with no matching filters
- **WHEN** I open a year that has cached listings and my current filters exclude every anime in it
- **THEN** the page reports that no anime match, not that MAL has no listing for the year
