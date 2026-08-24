## ADDED Requirements

### Requirement: Search results fall back to locally stored anime when the live search fails
When the live MAL search cannot be reached or fails for a query on the full search results page, the system SHALL search the anime it has stored locally instead of returning nothing, and SHALL present those matches as the page's results.

The locally stored set SHALL be every anime the app holds in its own storage, however it came to be there — anime in my list, anime cached from a season or a top-anime listing, and anime cached from any earlier search or detail view alike. Membership of my list SHALL NOT be a condition.

Matching SHALL follow the same rules the search already uses: a query SHALL match an anime whose title or English title contains it, case-insensitively, and a double-quoted query SHALL match only an anime whose title or English title exactly equals the quoted text.

Fallback results SHALL be presented exactly as live results are: the same cards, with picture, title, media type, episode count, and MAL score; the same continuous scroll; the same Type filter, offering only the media types present among the fallback results; and the same result count line.

Under the default relevance order, fallback results SHALL be ranked with exact title matches first, then anime whose title or English title begins with the query, then the remaining matches, each of those groups ordered by MAL popularity with unranked anime last and then by title, case-insensitively. The Popularity, MAL score, Alphabetical, and My score sorts SHALL each order the fallback results by the same rule they order live results by.

Matched series SHALL continue to be shown under the default relevance order, to the same maximum of three, since they are drawn from the app's own stored series rather than from the live search.

The page SHALL state that the fallback is in effect: it SHALL show a message saying the MAL search could not be reached and that the results shown are the app's locally stored matches. The message SHALL be shown whenever the fallback answered the query, including when the fallback itself found nothing, so an empty result reads as "nothing stored here matches" rather than as "this does not exist".

When the live MAL search succeeds, nothing SHALL change: the results, their ordering, the series shown, the count, and the absence of any such message SHALL all be exactly as they are today. A live search that succeeds and legitimately returns no matches SHALL show the ordinary empty state, with no fallback message.

#### Scenario: The live search is unreachable
- **WHEN** I submit a query and the live MAL search fails
- **THEN** the page lists the anime stored locally whose title or English title contains the query, presented as ordinary result cards

#### Scenario: The fallback says why
- **WHEN** the fallback has answered my query
- **THEN** the page shows a message saying the MAL search could not be reached and that these are the locally stored matches

#### Scenario: An empty fallback still explains itself
- **WHEN** the live search fails and nothing stored locally matches my query
- **THEN** the page still shows the message saying the MAL search could not be reached, rather than only an ordinary "no results" state

#### Scenario: Anime outside my list are included
- **WHEN** the fallback answers a query that matches an anime the app cached from a season listing but which is not in my list
- **THEN** that anime is among the results

#### Scenario: Fallback relevance ordering
- **WHEN** the fallback answers a query that one stored anime matches exactly, several match by prefix, and several more match only in the middle of their titles
- **THEN** the exact match is listed first, the prefix matches next, and the remaining matches after them, each group ordered by popularity

#### Scenario: Sorting fallback results
- **WHEN** the fallback has answered my query and I choose the MAL score sort
- **THEN** the local matches are reordered by MAL score, exactly as live results would be

#### Scenario: Filtering fallback results by type
- **WHEN** the fallback has answered my query and I select Movie in the type filter
- **THEN** only movie cards remain, and the count line reflects only the movie count

#### Scenario: An exact-match query against local storage
- **WHEN** the live search fails and I submit a double-quoted query
- **THEN** only stored anime whose title or English title exactly equals the quoted text are listed

#### Scenario: Series still lead the fallback
- **WHEN** the fallback answers a query that matches a stored series, under the default order
- **THEN** the matched series cards are shown first, before the anime cards, as they are for a live search

#### Scenario: A working search is untouched
- **WHEN** I submit a query and the live MAL search succeeds
- **THEN** the results are exactly the live search's results in their usual order, and no fallback message is shown

#### Scenario: A working search with no matches
- **WHEN** the live MAL search succeeds and genuinely returns no matches for my query
- **THEN** the page shows its ordinary empty state and does not claim the MAL search failed
