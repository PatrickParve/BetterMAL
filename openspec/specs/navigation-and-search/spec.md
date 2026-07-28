# navigation-and-search Specification

## Purpose
TBD - created by archiving change bootstrap-anime-tracker. Update Purpose after archive.
## Requirements
### Requirement: Navbar layout
The system SHALL provide a navbar whose left buttons are, in order, Home, My List, Top, Season, and Airing, and right controls Profile, the hide/unhide MAL-score toggle, and a Settings (gear icon) button.

#### Scenario: Navigating via the navbar
- **WHEN** I click a navbar button
- **THEN** I am taken to the corresponding page (Home, My List, Top, Season, Airing, Profile, or Settings)

#### Scenario: My List is reachable from the navbar
- **WHEN** the navbar renders
- **THEN** a "My List" button appears in the left button group and navigates to the my-list page

#### Scenario: Opening settings from the gear
- **WHEN** I click the Settings gear icon
- **THEN** I am taken to the settings page

### Requirement: Clickable anime cards everywhere
The system SHALL make anime cards clickable everywhere they appear, linking to that anime's detail page.

#### Scenario: Clicking any card
- **WHEN** I click an anime card on any page
- **THEN** I am taken to that anime's detail page

### Requirement: English title preferred for display
The system SHALL, wherever an anime title is displayed (Home, Season, Top, anime detail, Profile, My List, Airing, and search results), show the anime's English title when one is available, falling back to the default title otherwise.

#### Scenario: English title available
- **WHEN** an anime has a stored English title and its title is displayed anywhere
- **THEN** the English title is shown

#### Scenario: No English title
- **WHEN** an anime has no English title
- **THEN** the default title is shown

### Requirement: Type-ahead search, merged local + live ranked by prefix and popularity
The system SHALL provide a centered, debounced type-ahead search that, on every query, MERGES matches from the local cache with a live MAL search (deduplicated by anime id) rather than short-circuiting on cached matches. It SHALL rank titles whose title or English title STARTS WITH the query ahead of titles that merely CONTAIN the query, order each group by popularity — most popular first, with unranked titles (MAL popularity rank absent or zero) last — and show a dropdown of up to 5 matches with picture and title. A live-search failure SHALL degrade to local matches only rather than erroring. When the query is wrapped in double quotes (`"…"`), the system SHALL instead return only anime whose title or English title EXACTLY equals the quoted text.

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

### Requirement: Search returns NSFW-rated titles
The system SHALL include NSFW-rated anime (MAL ratings `r+` and `rx`) in every search result set — both the navbar type-ahead dropdown and the full search results page — by opting the live MAL search request into NSFW results, exactly as the season listing and user-animelist requests already do. A title that appears on the season page SHALL be findable by searching for it. No user setting SHALL suppress NSFW titles from search results.

#### Scenario: Searching for an NSFW-rated title
- **WHEN** I search for an anime MAL rates `r+` or `rx`
- **THEN** it appears in the type-ahead dropdown and on the search results page, rather than the search returning no match

#### Scenario: Season and search agree
- **WHEN** an NSFW-rated anime is listed on the season page
- **THEN** searching for that anime's title finds it

#### Scenario: Hide-NSFW setting does not apply to search
- **WHEN** the "Hide NSFW" setting is enabled and I search for a hentai title
- **THEN** it still appears in the search results, because the setting scopes to the season browser only

### Requirement: Full search results page
The system SHALL provide a dedicated search results page that lists every anime matching a submitted query, presented the same way as the seasonal page (picture, title, media type, and episode count per card, using the same content-width-filling grid layout and the same fixed per-row card count at ordinary desktop widths). Results SHALL default to the order returned by the search API (closest match first) and SHALL additionally be sortable by Popularity, MAL score, Alphabetical, and My score; the Popularity sort SHALL place unranked anime (MAL popularity rank absent or zero) last. A double-quoted query SHALL constrain results to exact title matches. The query and sort SHALL be held in the URL so back-navigation restores the same view.

Results SHALL be loaded with continuous (infinite) scroll rather than numbered pages: an initial chunk renders immediately and further chunks are appended automatically as the user scrolls toward the end of the loaded results, with no pagination controls anywhere on the page. Sorting SHALL be applied server-side across the whole candidate result set before it is chunked, so the order is global rather than per-chunk and an item never moves between chunks as more load. Changing the sort SHALL reset the accumulated results to the first chunk.

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

### Requirement: Search submission keeps the query text
The system SHALL let the user submit the current search either by pressing Enter in the search box or by clicking a magnifier button beside it, navigating to the search results page for that query. The submitted text SHALL remain in the search box (and SHALL be restored from the URL when the search page is loaded directly or reloaded) rather than being cleared.

#### Scenario: Submit via Enter
- **WHEN** I press Enter with text in the search box
- **THEN** I am taken to the search results page for that query and the text stays in the box

#### Scenario: Submit via the magnifier button
- **WHEN** I click the magnifier button beside the search box
- **THEN** I am taken to the search results page for the current query

#### Scenario: Query restored on the search page
- **WHEN** I load or reload the search page for a query (e.g. via a direct `/search?q=…` link)
- **THEN** the search box shows that query

