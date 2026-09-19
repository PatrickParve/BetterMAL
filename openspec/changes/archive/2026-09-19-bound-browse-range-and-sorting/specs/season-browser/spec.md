## MODIFIED Requirements

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
