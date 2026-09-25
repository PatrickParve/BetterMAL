## ADDED Requirements

### Requirement: A loading indicator appears only once a load has taken noticeable time
Every page, page section and data overlay that shows a loading indicator while its data is being read SHALL show nothing in the indicator's place until the read has been in flight for 300 milliseconds. Once shown, the indicator SHALL fade in rather than appear at full strength. A read that settles within the delay SHALL therefore go straight from what was on screen before to its content, empty message or failure state, with no loading indicator in between.

The delay SHALL apply only to the indicator. The page's own header and controls, and any content the page already holds, SHALL render exactly as soon as they do today. Content SHALL be shown the moment it arrives, never held back to finish a fade or to meet a minimum display time.

Anything a page shows only while work is in flight SHALL obey the same rule, including the season and year pages' passive "Updating…" indicator: a refresh that settles within the delay SHALL leave no trace on screen.

For users who ask for reduced motion, the indicator SHALL still wait for the delay and then appear without animating.

A back/forward restore SHALL continue to show no loading state at all, as the page-state-restoration capability already requires.

#### Scenario: A fast load shows no loading text
- **WHEN** I open the Series page and its list arrives 40 milliseconds later
- **THEN** the page shows its header and then its cards, and "Loading…" is never drawn

#### Scenario: A slow load still says it is loading
- **WHEN** I open a page whose read takes two seconds
- **THEN** a loading indicator fades in once 300 milliseconds have passed and stays until the content arrives

#### Scenario: Content arriving just after the delay does not flash
- **WHEN** a read settles a few milliseconds after the loading indicator began to fade in
- **THEN** the content replaces the indicator at once, before the indicator has become noticeably visible

#### Scenario: A skipped season refresh leaves no trace
- **WHEN** I open a season whose visit-triggered refresh is skipped as already fresh and answers within a few milliseconds
- **THEN** the "Updating…" indicator is never drawn

#### Scenario: Reduced motion
- **WHEN** my system asks for reduced motion and a read takes two seconds
- **THEN** the loading indicator appears after the delay without fading

### Requirement: No empty or failure message is shown while its read is in flight
A page SHALL decide between showing its content, an empty message, a "nothing matches the filters" message, a "not listed" message or a failure message only from a read that has settled. While the read that would decide it is still in flight, the page SHALL show its loading presentation and none of those messages, even for a moment.

A "nothing matches the filters" or empty message SHALL be decided from the whole loaded and filtered list, never from the portion of it a progressive reveal has drawn so far.

#### Scenario: The Series page does not claim its filters match nothing
- **WHEN** I open the Series page with no filter set and its list is still loading
- **THEN** "No series match the selected filters" is never shown, and the page goes from loading to its cards

#### Scenario: A background result arriving first does not decide the page
- **WHEN** a page's secondary request settles before the page's own read of its data has landed
- **THEN** the page keeps its loading presentation until its own read has settled, and only then shows what that read found

### Requirement: A progressively revealed grid never reveals less than its first screenful
The Season, Year, Series and Search grids and the My list rows reveal an already-loaded list a screenful at a time as a sentinel below them scrolls into view. Revealing SHALL only ever add to the number of cards or rows drawn. It SHALL NOT reduce that number because the list was empty or still loading when the sentinel was in view. A list that arrives SHALL show at least its first screenful, or everything it holds if that is less, in the same frame in which it arrives.

This SHALL NOT change what counts as a fresh view: a sort or filter change, or a new visit, still opens on the first screenful at the top, and a back/forward restore still brings back the number of cards the page was showing.

#### Scenario: The grid arrives whole
- **WHEN** I open the Season page and its listing arrives while the sentinel below the empty grid is on screen
- **THEN** the first screenful of cards is drawn at once, with no frame showing an empty grid

#### Scenario: Scrolling still reveals more
- **WHEN** I scroll to the end of a revealed grid
- **THEN** the next screenful is added exactly as before

### Requirement: A failed read is presented as a failure, never as emptiness
When a page's or a page section's read fails and it has no data from that read to show, it SHALL show a failure state. The failure state SHALL say what could not be loaded and SHALL offer a **Try again** control that re-runs the read. While the app's backend is unreachable, the failure state SHALL say that the server cannot be reached and that the page will load by itself once the server is back.

A failed read SHALL NOT be presented as an empty result. A page SHALL NOT claim, because a read failed, that my list is empty, that a search matched nothing, that a week has nothing airing, that a period has nothing to recap or that a section has no entries. A page SHALL NOT be left blank, and SHALL NOT keep showing its loading indicator after its read has failed.

This covers the Home page, the Airing page, My list, the Search page, the Recap page, the Top anime page, the Profile page and each of its sections, the Settings page, the anime detail page, the series page, and the Season, Year and Series pages. On the Season and Year pages it covers a failure of the page's own read of its cached listing; a failed first fetch from MyAnimeList keeps the message those pages' own specifications give it.

Data already on screen SHALL stay on screen when a later read of it fails, including the silent background refresh after a back/forward restore, as today.

The failure state SHALL look the same on every page, apart from what it names.

#### Scenario: My list does not say it is empty when it failed to load
- **WHEN** I open My list while the backend cannot be reached
- **THEN** the page says my list could not be loaded and offers Try again, rather than "Nothing here yet"

#### Scenario: Home is not left blank
- **WHEN** the Home page's read fails
- **THEN** the page says the dashboard could not be loaded and offers Try again, rather than showing nothing below the navbar

#### Scenario: Search does not claim nothing matched
- **WHEN** a search results page's read fails
- **THEN** the page says the results could not be loaded, rather than "No anime found"

#### Scenario: A profile section does not claim to be empty
- **WHEN** the Profile page loads but its Top series section's read fails
- **THEN** that section says it could not be loaded and offers Try again, while the rest of the page shows normally

#### Scenario: Try again loads the page
- **WHEN** a page is showing its failure state, the backend is reachable again, and I press Try again
- **THEN** the page re-runs its read with its normal loading presentation and shows its content

#### Scenario: The failure names the unreachable server
- **WHEN** a page's read fails while the backend is unreachable
- **THEN** its failure state says the server cannot be reached and that the page will load once the server is back

### Requirement: A failed read reloads by itself once the server is reachable again
When the app's backend becomes reachable again after being unreachable, every mounted page and page section whose latest read failed SHALL re-run that read by itself, with its normal loading presentation, without my pressing anything or navigating.

A read SHALL be retried this way at most once per failure. If the automatic retry fails too, the page SHALL keep its failure state with Try again and SHALL NOT retry by itself again until I press Try again or open the page afresh. Pressing Try again SHALL allow one further automatic retry. This SHALL NOT start any retry loop against a backend that keeps failing the same read.

A page whose data is on screen SHALL NOT be re-read because the backend became reachable again.

#### Scenario: The page comes back with the server
- **WHEN** I opened the Airing page while the backend was down, it shows its failure state, and the backend comes back
- **THEN** the connection notice clears and the Airing page loads its week by itself

#### Scenario: A read that keeps failing is not hammered
- **WHEN** a page's automatic retry after reconnecting fails again with a server error
- **THEN** the page shows its failure state with Try again and makes no further request for that read on its own

#### Scenario: Loaded pages are left alone
- **WHEN** the backend recovers from an outage while I am reading a page whose data had already loaded
- **THEN** that page issues no read because of the recovery

### Requirement: Stepping to another period keeps the current content until the next arrives
When I step the Season page to another season, the Year page to another year, or the Airing page to another week, the page SHALL keep showing the content it was showing until the new period's data arrives, rather than collapsing to an empty page or a loading message in between. If the new period's read is still in flight after the loading indicator's delay, the held content SHALL be shown visibly muted and not interactive, so it is not mistaken for the new period. The held content SHALL be replaced the moment the new period's data arrives, or by the new period's failure state if its read fails.

The header and controls SHALL show the new period from the moment I step to it.

This SHALL NOT change what a fresh view of the new period opens on: it still opens at the top, on its first screenful.

#### Scenario: Stepping seasons does not collapse the page
- **WHEN** I press the next-season arrow and the next season's listing arrives 50 milliseconds later
- **THEN** the previous season's grid stays in place until the new grid replaces it, with no empty frame and no loading text

#### Scenario: A slow step is marked as such
- **WHEN** I step to another week on the Airing page and its read is still in flight after 300 milliseconds
- **THEN** the previous week's schedule is shown muted and not interactive until the new week arrives
