## ADDED Requirements

### Requirement: Search result text is not clipped by its own line box
No line of text in a search result SHALL be clipped by the line box it is laid out in. This covers every row of the type-ahead dropdown — a result's title and, on a series row, the "Series" badge line beneath it — and every card on the results page, including a series card's badge line.

Where a line's height is pinned to a fixed value (as the dropdown's rows are, to keep every row the same height), that value SHALL be at least the height of the glyphs the line renders, across the whole range of the app's fluid root font size, so that no ascender, descender, or bracket is shaved off along the line's top or bottom edge. A fixed row height SHALL likewise leave room for the lines it contains at that largest size, rather than squeezing them.

Sizing a line to its glyphs SHALL NOT be achieved by letting the dropdown's rows differ in height from one another, which the "A series result is marked as a series" requirement forbids: the row's own height SHALL absorb the difference so that a dropdown of five rows stays one height whichever of those rows are series.

#### Scenario: A dropdown title is drawn in full
- **WHEN** the type-ahead dropdown shows a result whose title contains tall or bracketed characters, at any window width
- **THEN** every character is drawn whole, with nothing cut off along the top or bottom of the line

#### Scenario: A series badge line is drawn in full
- **WHEN** a series result is shown, in the dropdown or as a results-page card
- **THEN** the "Series" pill's text and the entry-count text beside it are drawn whole, with nothing cut off along the top of the line

#### Scenario: Rows stay uniform after the fix
- **WHEN** the dropdown lists both series rows and anime rows
- **THEN** every row is still the same height and the dropdown does not change height as the matches change while typing

## MODIFIED Requirements

### Requirement: Full search results page
The system SHALL provide a dedicated search results page that lists every anime matching a submitted query, presented the same way as the seasonal page (picture, title, media type, episode count, and MAL score per card, laid out exactly as a season card lays them out, using the same content-width-filling grid layout and the same fixed per-row card count at ordinary desktop widths). The MAL score SHALL follow the season card's rules in full, including showing nothing at all — no value, no placeholder, no reveal control — while the global hide-scores toggle is on. Results SHALL default to the order returned by the search API (closest match first) and SHALL additionally be sortable by Popularity, MAL score, Alphabetical, and My score; the Popularity sort SHALL place unranked anime (MAL popularity rank absent or zero) last. A double-quoted query SHALL constrain results to exact title matches. The query and sort SHALL be held in the URL so back-navigation restores the same view.

Under the default relevance order, matched series (see "Series appear in search results") SHALL be shown FIRST, ahead of the anime cards, to a maximum of 3. Under any other sort — Popularity, MAL score, Alphabetical, or My score — series SHALL be omitted, since those orderings are defined over per-anime figures a series does not have. The result count shown on the page SHALL continue to count anime only.

A series card SHALL NOT show a MAL score in that slot: its badge line already occupies the slot an anime card uses for its meta line, and a series has no single MAL score of its own.

Results SHALL be loaded with continuous (infinite) scroll rather than numbered pages: an initial chunk renders immediately and further chunks are appended automatically as the user scrolls toward the end of the loaded results, with no pagination controls anywhere on the page. Sorting SHALL be applied server-side across the whole candidate result set before it is chunked, so the order is global rather than per-chunk and an item never moves between chunks as more load. Changing the sort SHALL reset the accumulated results to the first chunk. Series cards SHALL all be shown up front rather than participating in chunked reveal.

The system SHALL provide a multi-select Type filter on the search results page, using the same control and display labels as My List's type filter, offering only the media types actually present among the currently loaded candidate results. Selecting one or more types SHALL immediately narrow the displayed anime cards to matching types, applied over the already-loaded candidate set without issuing a new search request. With no type selected, no type restriction applies. The result count line SHALL reflect the type-filtered anime count rather than the full unfiltered candidate count. Series cards SHALL be unaffected by the type filter and SHALL continue to be shown under the default relevance order regardless of which types are selected, since a series is not itself a single media-typed row.

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

#### Scenario: Clearing the type filter
- **WHEN** no type is selected in the search page's type filter
- **THEN** anime cards of every loaded type are shown
