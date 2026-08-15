## MODIFIED Requirements

### Requirement: Full search results page
The system SHALL provide a dedicated search results page that lists every anime matching a submitted query, presented the same way as the seasonal page (picture, title, media type, and episode count per card, using the same content-width-filling grid layout and the same fixed per-row card count at ordinary desktop widths). Results SHALL default to the order returned by the search API (closest match first) and SHALL additionally be sortable by Popularity, MAL score, Alphabetical, and My score; the Popularity sort SHALL place unranked anime (MAL popularity rank absent or zero) last. A double-quoted query SHALL constrain results to exact title matches. The query and sort SHALL be held in the URL so back-navigation restores the same view.

Under the default relevance order, matched series (see "Series appear in search results") SHALL be shown FIRST, ahead of the anime cards, to a maximum of 3. Under any other sort — Popularity, MAL score, Alphabetical, or My score — series SHALL be omitted, since those orderings are defined over per-anime figures a series does not have. The result count shown on the page SHALL continue to count anime only.

Results SHALL be loaded with continuous (infinite) scroll rather than numbered pages: an initial chunk renders immediately and further chunks are appended automatically as the user scrolls toward the end of the loaded results, with no pagination controls anywhere on the page. Sorting SHALL be applied server-side across the whole candidate result set before it is chunked, so the order is global rather than per-chunk and an item never moves between chunks as more load. Changing the sort SHALL reset the accumulated results to the first chunk. Series cards SHALL all be shown up front rather than participating in chunked reveal.

The system SHALL provide a multi-select Type filter on the search results page, using the same control and display labels as My List's type filter, offering only the media types actually present among the currently loaded candidate results. Selecting one or more types SHALL immediately narrow the displayed anime cards to matching types, applied over the already-loaded candidate set without issuing a new search request. With no type selected, no type restriction applies. The result count line SHALL reflect the type-filtered anime count rather than the full unfiltered candidate count. Series cards SHALL be unaffected by the type filter and SHALL continue to be shown under the default relevance order regardless of which types are selected, since a series is not itself a single media-typed row.

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

#### Scenario: Filtering results by type
- **WHEN** I select Movie in the search page's type filter
- **THEN** only movie cards remain visible among the loaded results, and the result count line reflects only the movie count

#### Scenario: Only present types are offered
- **WHEN** the loaded search results contain no music videos
- **THEN** the type filter does not offer Music as an option

#### Scenario: Clearing the type filter
- **WHEN** no type is selected in the search page's type filter
- **THEN** anime cards of every loaded type are shown
