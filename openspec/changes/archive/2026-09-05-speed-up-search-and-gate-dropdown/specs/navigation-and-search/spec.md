## MODIFIED Requirements

### Requirement: Type-ahead search, merged local + live ranked by prefix and popularity
The system SHALL provide a debounced type-ahead search, presented in the navbar's right-hand control group (see "Navbar layout"), that on every query MERGES matches from the local cache with a live MAL search (deduplicated by anime id) rather than short-circuiting on cached matches. It SHALL rank titles whose title or English title STARTS WITH the query ahead of titles that merely CONTAIN the query, order each group by popularity — most popular first, with unranked titles (MAL popularity rank absent or zero) last — and show a dropdown of up to 5 matches with picture and title. A live-search failure SHALL degrade to local matches only rather than erroring. When the query is wrapped in double quotes (`"…"`), the system SHALL instead return only anime whose title or English title EQUALS the quoted text.

Starts-with, contains, and equality SHALL all be evaluated under the normalization the "Title matching ignores case, spacing, punctuation, and accents" requirement defines, so the ranking bands and the quoted-query filter are alike insensitive to case, spacing, punctuation, and accents.

The dropdown's 5 rows are shared with matched series (see "Series appear in search results"): matched series occupy the first rows, up to a maximum of 2, and anime matches fill the rest. When no series matches, all 5 rows are anime, exactly as before.

**The dropdown SHALL NOT wait on the live search to show anything.** Suggestions SHALL be delivered in two stages for every query:

- A **cache-only stage**, answered from the app's own stored anime and stored series with no live request in its path, presented as soon as it arrives. It SHALL be ranked, capped, and shaped exactly as the merged result is — the same prefix-then-contains ordering by popularity, the same 5-row budget, the same maximum of 2 series pinned first — so it is indistinguishable from a merged result apart from which anime it could draw on.
- The **merged stage** described above, which SHALL replace what the cache-only stage put on screen once it arrives.

The cache-only stage SHALL be debounced more eagerly than the merged stage, since it costs a local read rather than a live request. When both stages have answered for the same query, what is shown SHALL be the merged result. A merged result SHALL NOT replace what is on screen once the cache-only stage has moved on to a newer query.

The live search behind the merged stage SHALL request the same candidate set the full search results page requests for the same query, so that the two agree about which anime exist for a query and one live response can serve both (see `mal-api-integration`, "Live search responses are briefly cached and shared").

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
- **THEN** only anime whose title or English title equals the quoted text — compared under the app's normalization rule — are shown

#### Scenario: Live-search failure is non-fatal
- **WHEN** the live MAL search fails (network blip or transient error)
- **THEN** the dropdown still shows any local-cache matches instead of erroring

#### Scenario: Series take the first rows of the dropdown
- **WHEN** my query matches one stored series and several anime
- **THEN** the dropdown shows the series first and fills the remaining rows with the top-ranked anime matches, 5 rows in total

#### Scenario: At most two series in the dropdown
- **WHEN** my query matches more than two stored series
- **THEN** only the two strongest-matching series are shown, leaving at least three rows for anime matches

#### Scenario: Stored matches appear before the live search returns
- **WHEN** I stop typing a query that several stored anime match
- **THEN** those matches are on screen well before the live MAL search for that query has returned, rather than the dropdown staying empty until it does

#### Scenario: Stored series appear in the first stage too
- **WHEN** my query matches a stored series
- **THEN** that series is pinned at the top of the first stage's rows, not added a second later when the live results arrive

#### Scenario: The merged ranking replaces the stored-only rows
- **WHEN** the live search for the query I have stopped typing returns
- **THEN** the dropdown's rows become the merged local + live ranking, which is what it settles on

#### Scenario: Nothing stored matches
- **WHEN** I stop typing a query no stored anime or series matches
- **THEN** the dropdown shows nothing until the merged stage arrives, and then shows the live matches

#### Scenario: A superseded live response is discarded
- **WHEN** a live response arrives for a query I have already typed past, and the first stage has already shown rows for the newer query
- **THEN** the newer rows stay on screen and the superseded response is discarded

### Requirement: Searching schedules a series build for an unknown franchise
When a search's top-ranked anime match belongs to no stored series, the system SHALL schedule that anime's series to be built in the background, so the franchise becomes searchable on a later search without the user having to open its series page.

Scheduling SHALL NOT affect the response: it SHALL NOT delay the search, SHALL NOT add a MAL request to the search request path, and a scheduling or build failure SHALL leave the search results unchanged.

The system SHALL schedule at most one build per search — the top-ranked anime match only — SHALL skip scheduling for queries shorter than three characters, and SHALL NOT re-schedule an anime it has already scheduled since the app started, so a debounced type-ahead does not queue a build per keystroke.

"Per search" counts a query, not a request. Where a query is answered in two stages (see "Type-ahead search, merged local + live ranked by prefix and popularity"), only the merged stage SHALL schedule: the cache-only stage SHALL schedule nothing, since its top-ranked match is drawn from stored anime alone and is not necessarily the query's top-ranked match.

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

#### Scenario: The cache-only stage schedules nothing
- **WHEN** the type-ahead's cache-only stage answers a query
- **THEN** it schedules no build, and the query still schedules at most one — from its merged stage

### Requirement: Search submission keeps the query text
The system SHALL let the user submit the current search either by pressing Enter in the search box or by clicking a magnifier button beside it, navigating to the search results page for that query. The submitted text SHALL remain in the search box (and SHALL be restored from the URL when the search page is loaded directly or reloaded) rather than being cleared.

Submitting SHALL dismiss the type-ahead dropdown and leave the search field unfocused, so the results page is shown with nothing over it and no cursor left in the field.

**Only a user interaction with the search field SHALL open the dropdown** — typing in it, or focusing it. Nothing else SHALL: not a search response arriving, not a navigation, and not the search page restoring the submitted query into the field from the URL. This SHALL hold whatever was in flight and whatever the type-ahead's debounce was holding at the moment of submission, so pressing Enter before suggestions for the typed query have been requested at all is no different from pressing it after they have been shown.

While the dropdown is dismissed, the type-ahead SHALL issue no search requests, and SHALL abandon any it has outstanding — so a submitted search does not compete with the results page's own search for the same query.

Dismissal SHALL NOT be sticky: typing anything after a submission SHALL show suggestions again as it does today, and returning focus to the field SHALL show suggestions for whatever the field currently contains — searching for them if they are not already in hand, and never showing suggestions fetched for a different query than the one now in the field.

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

#### Scenario: Submitting before the debounce has even fired
- **WHEN** I type a query and press Enter faster than the type-ahead's debounce, so suggestions for the typed query are requested only after I have left for the results page
- **THEN** no dropdown opens over the results, and none appears when the search page writes the query back into the field

#### Scenario: A dismissed dropdown stops searching
- **WHEN** I submit a search while a type-ahead request for that query is in flight
- **THEN** that request is abandoned rather than left to compete with the results page's own search

#### Scenario: Typing again brings suggestions back
- **WHEN** I submit a search and then type another character in the field
- **THEN** the dropdown opens again with suggestions for the new query

#### Scenario: Refocusing brings suggestions back
- **WHEN** I submit a search and then click back into the search field without typing
- **THEN** the dropdown opens with suggestions for the query in the field

#### Scenario: Refocusing never shows another query's suggestions
- **WHEN** the text in the field changed while the dropdown was dismissed and I then focus the field
- **THEN** the dropdown shows suggestions for the text now in the field, never the ones fetched for what it held before

#### Scenario: Query restored on the search page
- **WHEN** I load or reload the search page for a query (e.g. via a direct `/search?q=…` link)
- **THEN** the search box shows that query

### Requirement: Full search results page
The system SHALL provide a dedicated search results page that lists the anime matching a submitted query, presented the same way as the seasonal page (picture, title, media type, episode count, and MAL score per card, laid out exactly as a season card lays them out, using the same content-width-filling grid layout and the same fixed per-row card count at ordinary desktop widths). The MAL score SHALL follow the season card's rules in full, including showing nothing at all — no value, no placeholder, no reveal control — while the global hide-scores toggle is on. Results SHALL default to the order returned by the search API (closest match first) and SHALL additionally be sortable by Popularity, MAL score, Alphabetical, and My score; the Popularity sort SHALL place unranked anime (MAL popularity rank absent or zero) last. A double-quoted query SHALL constrain results to exact title matches. The query and sort SHALL be held in the URL so back-navigation restores the same view.

The page SHALL work over a **bounded candidate set: the 60 highest-relevance anime the search finds for the query**. A query matching more than 60 anime SHALL show the 60 most relevant rather than all of them. The same 60 SHALL be what every other behaviour on the page is defined over — the sorts reorder those 60, the Type filter offers only the media types present among them and narrows within them, the count line counts them, and continuous scroll reveals them and then stops. The system SHALL NOT fetch more candidates than the page can display: the number requested from the live search, the number the endpoint will return, and the number the page asks for SHALL be one and the same figure.

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

#### Scenario: A query with more matches than the page holds
- **WHEN** I submit a query that matches more anime than the page's candidate set holds
- **THEN** the 60 most relevant are listed, the count line reads 60, and scrolling past them loads nothing further

#### Scenario: Nothing is fetched that cannot be shown
- **WHEN** the page runs a search for a query
- **THEN** the live search is asked for exactly the number of candidates the page can display, rather than a larger set that is ranked and then partly discarded

#### Scenario: The type filter works within the candidate set
- **WHEN** I filter by a media type on a query whose candidate set is full
- **THEN** the types offered and the cards shown are drawn from those candidates, and the count line reflects the filtered count among them
