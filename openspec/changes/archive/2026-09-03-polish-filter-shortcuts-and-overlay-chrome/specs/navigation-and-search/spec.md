## MODIFIED Requirements

### Requirement: Full search results page
The system SHALL provide a dedicated search results page that lists every anime matching a submitted query, presented the same way as the seasonal page (picture, title, media type, episode count, and MAL score per card, laid out exactly as a season card lays them out, using the same content-width-filling grid layout and the same fixed per-row card count at ordinary desktop widths). The MAL score SHALL follow the season card's rules in full, including showing nothing at all — no value, no placeholder, no reveal control — while the global hide-scores toggle is on. Results SHALL default to the order returned by the search API (closest match first) and SHALL additionally be sortable by Popularity, MAL score, Alphabetical, and My score; the Popularity sort SHALL place unranked anime (MAL popularity rank absent or zero) last. A double-quoted query SHALL constrain results to exact title matches. The query and sort SHALL be held in the URL so back-navigation restores the same view.

Under the default relevance order, matched series (see "Series appear in search results") SHALL be shown FIRST, ahead of the anime cards, to a maximum of 3. Under any other sort — Popularity, MAL score, Alphabetical, or My score — series SHALL be omitted, since those orderings are defined over per-anime figures a series does not have. The result count shown on the page SHALL continue to count anime only.

A series card SHALL NOT show a MAL score in that slot: its badge line already occupies the slot an anime card uses for its meta line, and a series has no single MAL score of its own.

Results SHALL be loaded with continuous (infinite) scroll rather than numbered pages: an initial chunk renders immediately and further chunks are appended automatically as the user scrolls toward the end of the loaded results, with no pagination controls anywhere on the page. Sorting SHALL be applied server-side across the whole candidate result set before it is chunked, so the order is global rather than per-chunk and an item never moves between chunks as more load. Changing the sort SHALL reset the accumulated results to the first chunk. Series cards SHALL all be shown up front rather than participating in chunked reveal.

The system SHALL provide a multi-select Type filter on the search results page, using the same control and display labels as My List's type filter, offering only the media types actually present among the currently loaded candidate results. Selecting one or more types SHALL immediately narrow the displayed anime cards to matching types, applied over the already-loaded candidate set without issuing a new search request. The filter SHALL distinguish **All** from **None** as the `page-header-design` capability defines: on **All** — its state on a fresh visit — no type restriction applies; on **None**, no anime card passes the filter. **All**, **None**, and a partial selection SHALL each be distinctly representable in the page's URL state, with a URL naming no type filter meaning **All**. The result count line SHALL reflect the type-filtered anime count rather than the full unfiltered candidate count. Series cards SHALL be unaffected by the type filter and SHALL continue to be shown under the default relevance order regardless of which types are selected, since a series is not itself a single media-typed row.

When the type filter leaves no anime card to show but the query itself matched anime, the page SHALL say that no anime match the current filters rather than showing an empty grid in silence or reporting that nothing was found for the query. Any series cards the query matched SHALL still be listed alongside that message, since the type filter does not apply to them.

#### Scenario: Viewing full results for a query
- **WHEN** I submit a search
- **THEN** the search page shows all matching anime as cards with picture, title, type, episode count, and MAL score, in the API's relevance order by default

#### Scenario: A search card's score sits where a season card's does
- **WHEN** I compare a search results card with a season card for the same anime
- **THEN** both show the type and episode count at the start of the meta line and the MAL score at its end, laid out identically

#### Scenario: Hiding scores removes the search card score entirely
- **WHEN** the global hide-scores toggle is on and I view search results
- **THEN** no card shows a MAL score, a placeholder, or a reveal control

#### Scenario: A series card has no score slot
- **WHEN** a series card is shown among the results
- **THEN** its badge line occupies the meta line and no MAL score is shown on it

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

#### Scenario: Filtering results by type
- **WHEN** I select Movie in the search page's type filter
- **THEN** only movie cards remain visible among the loaded results, and the result count line reflects only the movie count

#### Scenario: Only present types are offered
- **WHEN** the loaded search results contain no music videos
- **THEN** the type filter does not offer Music as an option

#### Scenario: All applies no type restriction
- **WHEN** the search page's type filter is on All
- **THEN** anime cards of every loaded type are shown

#### Scenario: None empties the grid and says so
- **WHEN** I press **None** in the search page's type filter on a query that matched anime
- **THEN** no anime cards are shown and the page says no anime match the current filters, rather than saying no anime were found

#### Scenario: Series survive None
- **WHEN** the type filter is on **None** and the query matched a stored series
- **THEN** that series card is still listed

### Requirement: Search submission keeps the query text
The system SHALL let the user submit the current search either by pressing Enter in the search box or by clicking a magnifier button beside it, navigating to the search results page for that query. The submitted text SHALL remain in the search box (and SHALL be restored from the URL when the search page is loaded directly or reloaded) rather than being cleared.

Submitting SHALL dismiss the type-ahead dropdown and leave the search field unfocused, so the results page is shown with nothing over it and no cursor left in the field. The dismissal SHALL hold against a search request that was already in flight when the search was submitted: a response arriving afterwards for the submitted query SHALL NOT reopen the dropdown over the results.

Dismissing the dropdown SHALL be scoped to the query that was dismissed, not sticky: typing anything after a submission SHALL show suggestions again as it does today, and returning focus to the field with results already in hand SHALL reopen them.

#### Scenario: Submit via Enter
- **WHEN** I press Enter with text in the search box
- **THEN** I am taken to the search results page for that query and the text stays in the box

#### Scenario: Submit via the magnifier button
- **WHEN** I click the magnifier button beside the search box
- **THEN** I am taken to the search results page for the current query

#### Scenario: The dropdown does not follow me to the results
- **WHEN** I press Enter while the type-ahead dropdown is showing suggestions
- **THEN** the results page is shown with no dropdown over it

#### Scenario: The field gives up focus
- **WHEN** I submit a search
- **THEN** the search field is no longer focused and no cursor is left in it

#### Scenario: A late response does not reopen the dropdown
- **WHEN** I press Enter before the suggestions for that query have arrived, and the response arrives once the results page is showing
- **THEN** no dropdown opens over the results

#### Scenario: Typing again brings suggestions back
- **WHEN** I submit a search and then type another character in the field
- **THEN** the dropdown opens again with suggestions for the new query

#### Scenario: Query restored on the search page
- **WHEN** I load or reload the search page for a query (e.g. via a direct `/search?q=…` link)
- **THEN** the search box shows that query
