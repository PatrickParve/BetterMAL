## ADDED Requirements

### Requirement: Title matching ignores case, spacing, punctuation, and accents

Every comparison the search makes between a query and a title SHALL be made over a **normalized** form of both sides, so that a query which is right about the words finds the title whatever the user did with case, spacing, or punctuation.

Normalization SHALL: lower-case the text, fold accents and other diacritical marks to their base letters, and remove every character that is not a letter or a digit — spaces, hyphens, dashes, colons, slashes, full stops, apostrophes, quotation marks, exclamation and question marks, and any other punctuation or symbol. Letters of every script SHALL be kept, so a title in kana or kanji matches exactly as it does today.

This SHALL govern all three match kinds — **exact equality**, **starts-with**, and **contains** — and SHALL apply everywhere search matches a title: the type-ahead dropdown, the full results page, the local fallback used when the live search fails, and series matching (member titles, member English titles, and a series' chosen title alike). Anime and series SHALL use the one normalization rule, so the two can never disagree about whether a query matches.

Normalization SHALL widen only what counts as a match. Match quality ordering (exact ahead of prefix ahead of contains), popularity ordering within each band, the number of series and anime rows shown, and the background series-build trigger SHALL all be unchanged.

A query that contains no letters or digits at all — punctuation or symbols only — normalizes to nothing, and the system SHALL return no matches for it rather than treating it as matching every title.

The live MyAnimeList search request SHALL keep sending the query as the user typed it; normalization governs the system's own matching, not what a third party is asked. An anime that is neither stored locally nor returned by MyAnimeList for the query is therefore still not found.

#### Scenario: Spacing does not matter
- **WHEN** I search for "full metal" and *Fullmetal Alchemist* is stored
- **THEN** it appears in the type-ahead dropdown and on the results page

#### Scenario: Punctuation does not matter
- **WHEN** I search for "re zero" and *Re:ZERO -Starting Life in Another World-* is stored
- **THEN** it matches, and so does a search for "rezero" or "Re-Zero"

#### Scenario: A hyphenated title matches without its hyphen
- **WHEN** I search for "kaguya sama" and *Kaguya-sama: Love is War* is stored
- **THEN** it matches

#### Scenario: Accents do not matter
- **WHEN** I search for "kimi ni todoke" and a stored title spells a word with an accented letter
- **THEN** the accented and unaccented spellings match one another

#### Scenario: A series matches by the same rule
- **WHEN** a stored series has a member titled "Fullmetal Alchemist: Brotherhood" and I search for "full metal alchemist"
- **THEN** the series matches and is listed ahead of the anime results, as any matched series is

#### Scenario: A quoted query is exact about the words, not the punctuation
- **WHEN** I search for `"fullmetal alchemist"` in double quotes
- **THEN** *Fullmetal Alchemist* matches, and *Fullmetal Alchemist: Brotherhood* does not, because a quoted query still means the whole title rather than part of one

#### Scenario: Ranking is unchanged
- **WHEN** several titles match a query, some by prefix and some only in the middle
- **THEN** the prefix matches are still listed first, each band still ordered by popularity with unranked titles last

#### Scenario: A punctuation-only query matches nothing
- **WHEN** I search for "!!!" or "—"
- **THEN** no results are returned, rather than every title matching

#### Scenario: A Japanese-script title is unaffected
- **WHEN** I search using kana or kanji
- **THEN** the same titles match as before the normalization rule existed

## MODIFIED Requirements

### Requirement: Type-ahead search, merged local + live ranked by prefix and popularity
The system SHALL provide a debounced type-ahead search, presented in the navbar's right-hand control group (see "Navbar layout"), that on every query MERGES matches from the local cache with a live MAL search (deduplicated by anime id) rather than short-circuiting on cached matches. It SHALL rank titles whose title or English title STARTS WITH the query ahead of titles that merely CONTAIN the query, order each group by popularity — most popular first, with unranked titles (MAL popularity rank absent or zero) last — and show a dropdown of up to 5 matches with picture and title. A live-search failure SHALL degrade to local matches only rather than erroring. When the query is wrapped in double quotes (`"…"`), the system SHALL instead return only anime whose title or English title EQUALS the quoted text.

Starts-with, contains, and equality SHALL all be evaluated under the normalization the "Title matching ignores case, spacing, punctuation, and accents" requirement defines, so the ranking bands and the quoted-query filter are alike insensitive to case, spacing, punctuation, and accents.

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

### Requirement: Search results fall back to locally stored anime when the live search fails
When the live MAL search cannot be reached or fails for a query on the full search results page, the system SHALL search the anime it has stored locally instead of returning nothing, and SHALL present those matches as the page's results.

The locally stored set SHALL be every anime the app holds in its own storage, however it came to be there — anime in my list, anime cached from a season or a top-anime listing, and anime cached from any earlier search or detail view alike. Membership of my list SHALL NOT be a condition.

Matching SHALL follow the same rules the search already uses: a query SHALL match an anime whose title or English title contains it, and a double-quoted query SHALL match only an anime whose title or English title equals the quoted text — both evaluated under the normalization the "Title matching ignores case, spacing, punctuation, and accents" requirement defines, so the fallback is insensitive to case, spacing, punctuation, and accents exactly as the live path is.

Fallback results SHALL be presented exactly as live results are: the same cards, with picture, title, media type, episode count, and MAL score; the same continuous scroll; the same Type filter, offering only the media types present among the fallback results; and the same result count line.

Under the default relevance order, fallback results SHALL be ranked with exact title matches first, then anime whose title or English title begins with the query, then the remaining matches, each of those groups ordered by MAL popularity with unranked anime last and then by title, case-insensitively. The Popularity, MAL score, Alphabetical, and My score sorts SHALL each order the fallback results by the same rule they order live results by.

Matched series SHALL continue to be shown under the default relevance order, to the same maximum of three, since they are drawn from the app's own stored series rather than from the live search.

The page SHALL state that the fallback is in effect: it SHALL show a message saying the MAL search could not be reached and that the results shown are the app's locally stored matches. The message SHALL be shown whenever the fallback answered the query, including when the fallback itself found nothing, so an empty result reads as "nothing stored here matches" rather than as "this does not exist".

When the live MAL search succeeds, nothing SHALL change: the results, their ordering, the series shown, the count, and the absence of any such message SHALL all be exactly as they are today. A live search that succeeds and legitimately returns no matches SHALL show the ordinary empty state, with no fallback message.

#### Scenario: The live search is unreachable
- **WHEN** I submit a query and the live MAL search fails
- **THEN** the page lists the anime stored locally whose title or English title contains the query, presented as ordinary result cards

#### Scenario: The fallback matches loosely too
- **WHEN** the live search fails and I submit "full metal"
- **THEN** a locally stored *Fullmetal Alchemist* is among the results

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
- **THEN** only stored anime whose title or English title equals the quoted text under the app's normalization rule are listed

#### Scenario: Series still lead the fallback
- **WHEN** the fallback answers a query that matches a stored series, under the default order
- **THEN** the matched series cards are shown first, before the anime cards, as they are for a live search

#### Scenario: A working search is untouched
- **WHEN** I submit a query and the live MAL search succeeds
- **THEN** the results are exactly the live search's results in their usual order, and no fallback message is shown

#### Scenario: A working search with no matches
- **WHEN** the live MAL search succeeds and genuinely returns no matches for my query
- **THEN** the page shows its ordinary empty state and does not claim the MAL search failed
