## MODIFIED Requirements

### Requirement: Navbar layout
The system SHALL provide a navbar with two groups of controls: a left group of page links and a right group holding the search field and the account/preference controls. There SHALL be no third, centred group — the search field belongs to the right group.

The left group's links SHALL be, in order: **Home, My List, Recap, Top, Season, Airing**. Recap SHALL sit directly to the right of My List, since a recap is a view of the same list.

The right group's controls SHALL be, in order from left to right: **the search field, the hide/unhide MAL-score toggle, Profile, and the Settings (gear icon) button** — so Settings sits at the navbar's far right edge, Profile immediately to its left, then the score toggle, then the search field. Read right-to-left from the edge, the order is Settings, Profile, score toggle, search field.

Every control SHALL keep the behaviour, hover treatment, and accessible labelling it has today; only the ordering and the search field's group membership change. The search field SHALL keep its own width within the right group rather than being squeezed to the width of a button, and its type-ahead dropdown SHALL stay anchored beneath the field in its new position.

At window widths too narrow for one row, the navbar MAY wrap the search field onto its own row, and SHALL keep the two groups' internal orderings when it does.

#### Scenario: Navigating via the navbar
- **WHEN** I click a navbar button
- **THEN** I am taken to the corresponding page (Home, My List, Recap, Top, Season, Airing, Profile, or Settings)

#### Scenario: Left group order
- **WHEN** the navbar renders
- **THEN** its left links read Home, My List, Recap, Top, Season, Airing from left to right, with Recap directly right of My List

#### Scenario: Right group order
- **WHEN** the navbar renders
- **THEN** its right controls read search field, score toggle, Profile, Settings from left to right, with Settings at the far right edge

#### Scenario: The search field is not centred
- **WHEN** the navbar renders at a width wide enough for one row
- **THEN** the search field sits in the right-hand group rather than centred between the two groups

#### Scenario: My List is reachable from the navbar
- **WHEN** the navbar renders
- **THEN** a "My List" button appears in the left button group and navigates to the my-list page

#### Scenario: Opening settings from the gear
- **WHEN** I click the Settings gear icon
- **THEN** I am taken to the settings page

#### Scenario: The dropdown follows the field
- **WHEN** I type into the relocated search field
- **THEN** the type-ahead dropdown appears anchored beneath the field, fully within the window rather than clipped at its right edge

### Requirement: Type-ahead search, merged local + live ranked by prefix and popularity
The system SHALL provide a debounced type-ahead search, presented in the navbar's right-hand control group (see "Navbar layout"), that on every query MERGES matches from the local cache with a live MAL search (deduplicated by anime id) rather than short-circuiting on cached matches. It SHALL rank titles whose title or English title STARTS WITH the query ahead of titles that merely CONTAIN the query, order each group by popularity — most popular first, with unranked titles (MAL popularity rank absent or zero) last — and show a dropdown of up to 5 matches with picture and title. A live-search failure SHALL degrade to local matches only rather than erroring. When the query is wrapped in double quotes (`"…"`), the system SHALL instead return only anime whose title or English title EXACTLY equals the quoted text.

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
