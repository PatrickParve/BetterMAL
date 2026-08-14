## ADDED Requirements

### Requirement: Series appear in search results
The system SHALL match stored series against a search query alongside individual anime, in both the type-ahead dropdown and the full search results page.

A series SHALL match when ANY of its members' title or English title matches the query under the same rules applied to anime titles — starts-with, contains, or (for a double-quoted query) exact equality. A matched series SHALL be presented using its ROOT member's title and picture, the same identity the series page itself uses, regardless of which member matched.

Each matched series SHALL take the strongest match quality of any of its members (exact ahead of prefix, prefix ahead of contains) and, as a tie-break, the best popularity rank among its matching members. Matched series SHALL be listed AHEAD of anime results.

A series SHALL be matched only from stored series — searching SHALL NOT trigger a live MAL fetch or a synchronous series build.

#### Scenario: Searching a franchise name surfaces its series
- **WHEN** I search for "attack on titan" and that series is stored
- **THEN** the Attack on Titan series appears in the results, above the individual anime entries, showing the root entry's title and picture

#### Scenario: Matching on a later entry's title
- **WHEN** I search for text that appears only in a non-root member's title (e.g. "final season")
- **THEN** the series still matches, and is shown under the root entry's title and picture rather than the matching member's

#### Scenario: Exact-match query and series
- **WHEN** I wrap a query in double quotes
- **THEN** a series matches only when one of its members' title or English title exactly equals the quoted text

#### Scenario: Search never blocks on building a series
- **WHEN** I search for a franchise whose series has never been built
- **THEN** the results return at the usual speed with anime matches only, rather than waiting for a series to be built

### Requirement: A series result is marked as a series
The system SHALL mark every series result so it is never mistaken for a single anime. On the line BELOW the title, a series result SHALL show a "Series" badge together with the number of entries in the series, counting every member — main line and extras alike.

On the full results page this badge line SHALL occupy the same slot an anime card uses for its media-type and episode-count line, so series cards and anime cards keep identical geometry within the grid.

#### Scenario: Badge on a dropdown result
- **WHEN** a series appears in the type-ahead dropdown
- **THEN** its row shows the series title with a "Series" badge and the entry count on the line beneath it

#### Scenario: Badge on a results-page card
- **WHEN** a series appears on the full search results page
- **THEN** its card shows the "Series" badge and entry count where an anime card shows its type and episode count, and the card is the same size and shape as the anime cards around it

#### Scenario: Entry count covers extras too
- **WHEN** a series has four main-line entries and three extras
- **THEN** its badge line reports seven entries

### Requirement: A series result opens the series page
The system SHALL navigate to the series page when a series result is activated, rather than to any individual anime's detail page.

#### Scenario: Opening a series from the dropdown
- **WHEN** I click a series row in the type-ahead dropdown
- **THEN** I am taken to that series' page

#### Scenario: Opening a series from the results page
- **WHEN** I click a series card on the search results page
- **THEN** I am taken to that series' page

### Requirement: Searching schedules a series build for an unknown franchise
When a search's top-ranked anime match belongs to no stored series, the system SHALL schedule that anime's series to be built in the background, so the franchise becomes searchable on a later search without the user having to open its series page.

Scheduling SHALL NOT affect the response: it SHALL NOT delay the search, SHALL NOT add a MAL request to the search request path, and a scheduling or build failure SHALL leave the search results unchanged.

The system SHALL schedule at most one build per search — the top-ranked anime match only — SHALL skip scheduling for queries shorter than three characters, and SHALL NOT re-schedule an anime it has already scheduled since the app started, so a debounced type-ahead does not queue a build per keystroke.

#### Scenario: An unknown franchise becomes searchable later
- **WHEN** I search for a franchise whose series has never been built, and later search for it again
- **THEN** the series has been built in the background in the meantime and now appears in the results

#### Scenario: Scheduling never slows the search
- **WHEN** a search schedules a background build
- **THEN** the results return exactly as fast as they would have otherwise, and contain the same anime matches

#### Scenario: A failed background build is invisible
- **WHEN** a scheduled build fails
- **THEN** the search that scheduled it is unaffected and no error is shown

#### Scenario: Typing does not queue a build per keystroke
- **WHEN** I type a franchise name one character at a time into the type-ahead
- **THEN** each candidate anime is scheduled at most once, not once per keystroke

#### Scenario: Very short queries schedule nothing
- **WHEN** my query is one or two characters long
- **THEN** no background build is scheduled

## MODIFIED Requirements

### Requirement: Type-ahead search, merged local + live ranked by prefix and popularity
The system SHALL provide a centered, debounced type-ahead search that, on every query, MERGES matches from the local cache with a live MAL search (deduplicated by anime id) rather than short-circuiting on cached matches. It SHALL rank titles whose title or English title STARTS WITH the query ahead of titles that merely CONTAIN the query, order each group by popularity — most popular first, with unranked titles (MAL popularity rank absent or zero) last — and show a dropdown of up to 5 matches with picture and title. A live-search failure SHALL degrade to local matches only rather than erroring. When the query is wrapped in double quotes (`"…"`), the system SHALL instead return only anime whose title or English title EXACTLY equals the quoted text.

The dropdown's 5 rows are shared with matched series (see "Series appear in search results"): matched series occupy the first rows, up to a maximum of 2, and anime matches fill the rest. When no series matches, all 5 rows are anime, exactly as before.

#### Scenario: Prefix matches ranked by popularity
- **WHEN** I type a query that is the start of several anime titles
- **THEN** the dropdown shows up to 5 matches, listing titles that start with the query first, ordered by popularity (e.g. typing "attack" surfaces the popular "Attack on Titan" entries, not a single incidental cached title)

#### Scenario: Contains-matches fill remaining slots
- **WHEN** fewer than 5 anime titles start with the query
- **THEN** anime whose title contains the query (but does not start with it) fill the remaining slots, also ordered by popularity

#### Scenario: Uncached titles found via live search
- **WHEN** a matching anime is not in the local cache
- **THEN** it still appears because the live MAL search results are merged in (e.g. searching "paradise" surfaces "Hell's Paradise")

#### Scenario: Exact match with quotes
- **WHEN** I wrap the query in double quotes
- **THEN** only anime whose title or English title exactly equals the quoted text are shown

#### Scenario: Live-search failure is non-fatal
- **WHEN** the live MAL search fails (network blip or transient error)
- **THEN** the dropdown still shows any local-cache matches instead of erroring

#### Scenario: Series take the first rows of the dropdown
- **WHEN** my query matches one stored series and several anime
- **THEN** the dropdown shows the series first and fills the remaining rows with the top-ranked anime matches, 5 rows in total

#### Scenario: At most two series in the dropdown
- **WHEN** my query matches more than two stored series
- **THEN** only the two strongest-matching series are shown, leaving at least three rows for anime matches

### Requirement: Full search results page
The system SHALL provide a dedicated search results page that lists every anime matching a submitted query, presented the same way as the seasonal page (picture, title, media type, and episode count per card, using the same content-width-filling grid layout and the same fixed per-row card count at ordinary desktop widths). Results SHALL default to the order returned by the search API (closest match first) and SHALL additionally be sortable by Popularity, MAL score, Alphabetical, and My score; the Popularity sort SHALL place unranked anime (MAL popularity rank absent or zero) last. A double-quoted query SHALL constrain results to exact title matches. The query and sort SHALL be held in the URL so back-navigation restores the same view.

Under the default relevance order, matched series (see "Series appear in search results") SHALL be shown FIRST, ahead of the anime cards, to a maximum of 3. Under any other sort — Popularity, MAL score, Alphabetical, or My score — series SHALL be omitted, since those orderings are defined over per-anime figures a series does not have. The result count shown on the page SHALL continue to count anime only.

Results SHALL be loaded with continuous (infinite) scroll rather than numbered pages: an initial chunk renders immediately and further chunks are appended automatically as the user scrolls toward the end of the loaded results, with no pagination controls anywhere on the page. Sorting SHALL be applied server-side across the whole candidate result set before it is chunked, so the order is global rather than per-chunk and an item never moves between chunks as more load. Changing the sort SHALL reset the accumulated results to the first chunk. Series cards SHALL all be shown up front rather than participating in chunked reveal.

#### Scenario: Viewing full results for a query
- **WHEN** I submit a search
- **THEN** the search page shows all matching anime as cards with picture, title, type, and episode count, in the API's relevance order by default

#### Scenario: Search grid matches the season grid's layout
- **WHEN** search results are displayed at any window width
- **THEN** the cards in each full row together span the content width the same way the season page's grid does, with no large empty space to the right of the grid

#### Scenario: Sorting results
- **WHEN** I choose a sort option on the search page
- **THEN** the results reorder by Relevance, Popularity, MAL score, Alphabetical, or My score as selected, starting again from the first chunk

#### Scenario: Scrolling loads more results
- **WHEN** I scroll to the end of the loaded search results and more matches exist
- **THEN** the next chunk is appended automatically below the ones already shown, without any page controls and without replacing what is already on screen

#### Scenario: No pagination controls
- **WHEN** a query returns more matches than fit in one chunk
- **THEN** no page numbers, next/previous buttons, or other pagination controls are shown

#### Scenario: Sort order holds across loaded chunks
- **WHEN** I sort by any option and scroll far enough to load several chunks
- **THEN** the results remain in one continuous sorted order across the chunk boundaries, rather than each chunk being sorted on its own

#### Scenario: End of results
- **WHEN** every match for the query has been loaded
- **THEN** scrolling further loads nothing more and no error or empty-state message replaces the results

#### Scenario: Exact match on the results page
- **WHEN** I submit a double-quoted query
- **THEN** the results page lists only anime whose title or English title exactly equals the quoted text

#### Scenario: View restored on back-navigation
- **WHEN** I open an anime from the results and navigate back
- **THEN** the same query and sort are restored from the URL

#### Scenario: Series lead the relevance ordering
- **WHEN** I submit a query matching a stored series and view the results in the default order
- **THEN** the series cards appear first, before the anime cards, with at most three shown

#### Scenario: Series are omitted under other sorts
- **WHEN** I switch the results page to Popularity, MAL score, Alphabetical, or My score
- **THEN** only anime cards are listed, with no series among them

#### Scenario: The result count reports anime only
- **WHEN** a query matches one series and twenty anime
- **THEN** the count line reads twenty results, and the series card is shown in addition to those twenty
