## MODIFIED Requirements

### Requirement: Infinite scroll
The season page SHALL load the selected season's **whole** listing — every anime the cache holds for it under the selected sort and filters — in a single read, and SHALL reveal that listing progressively as it is scrolled, adding a screenful at a time. Nothing SHALL cap how many of the season's anime can be reached: scrolling to the end of the grid SHALL reach the last anime the season holds.

Revealing SHALL be a client-side act over the already-loaded listing, not a further read: scrolling SHALL NOT issue requests, and the number of anime revealed SHALL NOT be limited by how many any one read returns.

How much of the listing has been revealed SHALL be part of the page's restorable state per the `page-state-restoration` capability: a back/forward restore SHALL bring back the same number of cards the page was showing when it was left, so the scroll position restored alongside them is reachable rather than clamped to the top. A fresh visit — including the fresh visit a sort or filter change amounts to — SHALL open on the first screenful at the top of the grid.

Because the whole listing is read at once, no refresh of the page SHALL be able to leave it showing fewer anime than it was showing before: neither the background refresh that follows a restore, nor the visit-triggered MAL refresh, SHALL shorten a grid that has been scrolled.

#### Scenario: Scrolling loads more
- **WHEN** I scroll to the end of the loaded season results
- **THEN** more results appear automatically

#### Scenario: Nothing is capped
- **WHEN** I keep scrolling a season holding several hundred anime
- **THEN** I reach its last anime, with no ceiling short of the season's own size

#### Scenario: Scrolling costs no requests
- **WHEN** I scroll from the top of a season's grid to its end
- **THEN** the results were all already loaded and no further read was issued to reveal them

#### Scenario: A restored season keeps what it had revealed
- **WHEN** I scroll a season well past its first screenful of cards, open one of them, and go back
- **THEN** the grid shows the same number of cards it showed when I left, both immediately and after its background refresh lands

#### Scenario: Returning to where I was in a season
- **WHEN** I go back to a season I had scrolled far down
- **THEN** the page is at the position I left it at and stays there once the refresh completes

#### Scenario: A refresh cannot shorten a scrolled grid
- **WHEN** I have scrolled a season deep into its listing and its visit-triggered refresh then completes
- **THEN** the grid still shows everything it was showing, rather than being cut back to a screenful

#### Scenario: A sort change starts again at the top
- **WHEN** I change the sort on a season I had scrolled deep into
- **THEN** the re-sorted grid opens on its first screenful at the top

### Requirement: Season filtering
The system SHALL allow filtering/sorting season anime by popularity, score, alphabetical, and my score, where my score is meaningful only for anime also in my list. When sorting by popularity, anime that are unranked — MAL popularity rank absent or zero — SHALL be ordered after all ranked anime (a rank of `0` is not treated as "most popular"), then ranked anime by ascending rank (1 = most popular), then by title.

When sorting by my score, the results SHALL be split into two ordered groups: first every anime I have scored, ordered by my score descending — equal scores broken by my ranking, best-ranked first, per the `anime-ranking` capability, then by popularity, then by title for anime my ranking does not cover; then every remaining anime, ordered by popularity using the same unranked-last rule as the popularity sort. The page SHALL render a visual break with the label `Unwatched` between the two groups, shown only when both groups have at least one anime.

Every one of these orderings SHALL be **produced server-side**, over the season's whole listing, and SHALL reach the page as a per-anime ordering position rather than as a rule the page applies itself — so the page can re-sort what it has already loaded without any ordering rule being expressed twice, and so the order it shows is by construction the order the server computed.

Changing the sort SHALL therefore take effect **immediately**, with no read, no loading state, and the same results and order the same sort produces today. It SHALL still count as a fresh view of the page: the grid SHALL return to the top, showing its first screenful.

#### Scenario: Sorting by my score
- **WHEN** I sort by my score
- **THEN** the anime I have scored appear first in descending score order, followed by an `Unwatched` divider, followed by the remaining anime in popularity order

#### Scenario: Equal scores follow my ranking
- **WHEN** I sort by my score in a season where I gave three anime the same score
- **THEN** those three appear in my ranking's order rather than in popularity order

#### Scenario: An unranked scored anime among ranked ones
- **WHEN** a scored anime the ranking does not cover shares a score with ranked anime
- **THEN** the ranked ones come first and it follows them, ordered by popularity among any other unranked anime of that score

#### Scenario: My-score grouping holds all the way down
- **WHEN** I sort by my score and scroll to the end of the season
- **THEN** no scored anime appears after the `Unwatched` divider, anywhere in the grid

#### Scenario: No scored anime in the season
- **WHEN** I sort by my score in a season where I have scored nothing
- **THEN** all anime are shown in popularity order with no `Unwatched` divider

#### Scenario: Unranked anime sort last by popularity
- **WHEN** I sort by popularity and some anime have no popularity rank (rank absent or zero)
- **THEN** the ranked anime appear first in ascending rank order and the unranked anime appear last, rather than an unranked anime sorting to the top

#### Scenario: Sorting is instant
- **WHEN** I change the sort on a season that is already loaded
- **THEN** the grid re-orders immediately with no read and no loading state

#### Scenario: A sort is still a fresh view
- **WHEN** I change the sort after scrolling deep into a season
- **THEN** the re-sorted grid opens at the top on its first screenful

### Requirement: In-my-list inclusion filter
The system SHALL provide an "In my list" checkbox beside the season sort control, checked by default. When it is unchecked, anime that have an entry in my list SHALL be excluded from the season results and from the result count the page reports, so only anime not yet in my list are shown. The checkbox state SHALL be part of the page's URL state.

The filter SHALL be applied to the season's already-loaded listing rather than through a read, so it takes effect immediately with no loading state, and SHALL exclude exactly the same anime it excludes today. Toggling it SHALL count as a fresh view of the page, returning the grid to the top on its first screenful.

#### Scenario: Default includes my list
- **WHEN** I open a season
- **THEN** the "In my list" checkbox is checked and anime in my list are shown alongside the rest

#### Scenario: Excluding anime in my list
- **WHEN** I uncheck "In my list"
- **THEN** every anime that has an entry in my list is removed from the results, the reported count reflects the smaller set, and scrolling continues to show only anime not in my list

#### Scenario: The filter is instant
- **WHEN** I toggle "In my list" on a season that is already loaded
- **THEN** the grid updates immediately with no read and no loading state

#### Scenario: Filter persists through back-navigation
- **WHEN** I uncheck "In my list", open an anime, then press the browser Back button
- **THEN** the checkbox is still unchecked and the filtered results are shown

### Requirement: Season type filter
The system SHALL provide a multi-select Type filter beside the season sort control, using the same control and display labels as My List's type filter, offering only the media types actually present in the season. Selecting one or more types SHALL restrict the season results, and the count the page reports, to matching types; with none selected, no type restriction applies. The selection state SHALL be part of the page's URL state so it survives back-navigation.

The filter SHALL be applied to the season's already-loaded listing rather than through a read, so it takes effect immediately with no loading state. The types it offers SHALL be derived from the whole listing, not from what the current selection leaves visible, so selecting one type SHALL NOT remove the others from the picker. Toggling the filter SHALL count as a fresh view of the page, returning the grid to the top on its first screenful.

#### Scenario: Filtering a season to one type
- **WHEN** I select Movie in the season page's type filter
- **THEN** only movie entries are shown for that season, and the count the page reports reflects only movies

#### Scenario: The filter holds all the way down
- **WHEN** I select a type and scroll to the end of the season
- **THEN** every card shown is of a matching type

#### Scenario: Only present types are offered
- **WHEN** a season contains no music videos
- **THEN** the type filter does not offer Music as an option for that season

#### Scenario: Selecting a type does not shrink the type options
- **WHEN** I select one type and the results narrow to it
- **THEN** the type filter still offers the season's other types, so I can change or clear my selection

#### Scenario: The filter is instant
- **WHEN** I change the type selection on a season that is already loaded
- **THEN** the grid updates immediately with no read and no loading state

#### Scenario: Clearing the type filter
- **WHEN** no type is selected in the season page's type filter
- **THEN** entries of every type are shown

#### Scenario: Filter persists through back-navigation
- **WHEN** I select a type, open an anime, then press the browser Back button
- **THEN** the same type selection is still applied and the filtered results are shown
