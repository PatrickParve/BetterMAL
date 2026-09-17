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

The cache-only stage's view of the app's stored anime MAY be served from an index held in memory rather than read from storage for each query, provided that index stays consistent with what is stored. That index SHALL reflect every saved change to a stored anime's title, English title, picture or popularity — an anime newly stored, or one of those fields rewritten on an anime already stored — from the next query after the change is saved. It SHALL NOT be allowed to go stale on a clock: only a saved change SHALL be able to make it out of date, never the passage of time, and a query SHALL NOT be answered from a partially built index. Nothing about the two stages, their two debounce rates, their ranking, their 5-row budget or their series rows SHALL differ according to whether a query was answered from such an index or from storage directly.

#### Scenario: Prefix matches ranked by popularity
- **WHEN** I type a query that is the start of several anime titles
- **THEN** the dropdown shows up to 5 matches, listing titles that start with the query first, ordered by popularity (e.g. typing "attack" surfaces the popular "Attack on Titan" entries, not a single incidental cached title)

#### Scenario: Contains-matches fill remaining slots
- **WHEN** fewer than 5 anime titles start with the query
- **THEN** anime whose title contains the query (but does not start with it) fill the remaining slots, also ordered by popularity

#### Scenario: A newly stored anime is matchable on the next query
- **WHEN** an anime the app had not stored before is stored by browsing a season, and I then type a query its title matches
- **THEN** the cache-only stage lists it, rather than leaving it out until some later change or restart

#### Scenario: A renamed or re-pictured anime is shown as stored
- **WHEN** a stored anime's title, English title, picture or popularity is rewritten by a refresh, an import or my own picture choice, and I then type a query that matches it
- **THEN** the cache-only stage shows the rewritten values and ranks it by the rewritten popularity, not by what it held before

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

