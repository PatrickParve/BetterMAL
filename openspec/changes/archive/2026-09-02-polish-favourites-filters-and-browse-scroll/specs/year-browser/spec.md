## MODIFIED Requirements

### Requirement: Year page presentation matches the season page
The Year page SHALL present its results using the same card content, the same grid, and the same header arrangement as the season browser, so the two pages differ only in the period they cover and the controls that select it.

Each card SHALL show the anime's title, picture, type and episode count at the leading edge of its meta line, and its MAL score at the trailing edge of that same line, under the same rules — `?` for an unknown episode count, nothing in the score slot for an anime with no MAL score, and no score furniture at all while the global hide-scores toggle is on.

The results grid SHALL span the full content width with no unused right-hand gutter, at a card count per row that is fixed at ordinary desktop widths and adapts below the mobile breakpoint. The header SHALL place the year step navigation in the horizontal center, the year dropdown immediately to its right, and the sort and filter controls after them.

Results SHALL load and reveal exactly as the season page's do: the year's whole listing — the union of its four seasons under the selected sort and filters — is read in one request and revealed progressively as the grid is scrolled, with nothing capping how many of the year's anime can be reached, no request issued to reveal more, the revealed count restored on a back/forward navigation, a fresh visit opening on the first screenful, and no refresh able to leave the grid showing fewer anime than it was showing.

#### Scenario: Rendering a year card
- **WHEN** a year's anime are displayed
- **THEN** each card shows its title, picture, type and episode count at the start of its meta line, and its MAL score at the end of that same line

#### Scenario: Hiding scores removes the card score entirely
- **WHEN** the global hide-scores toggle is on
- **THEN** no Year page card shows a MAL score, a placeholder, or a reveal control, and the type and episode count are unmoved

#### Scenario: The grid fills the content width
- **WHEN** year results are displayed at any window width
- **THEN** the cards in each full row together span the content width, with no large empty space to the right of the grid

#### Scenario: Header control placement
- **WHEN** I view the Year page header
- **THEN** the arrows and year label sit centered with the year dropdown directly to their right, and the sort and filter controls after them

#### Scenario: Scrolling loads more
- **WHEN** I scroll to the end of the loaded year results
- **THEN** more results appear automatically

#### Scenario: A whole year is reachable
- **WHEN** I keep scrolling a year holding well over a thousand anime
- **THEN** I reach its last anime, with no ceiling short of the year's own size, and no read was issued to reveal them

#### Scenario: A restored year keeps what it had revealed
- **WHEN** I scroll a year well past its first screenful of cards, open one of them, and go back
- **THEN** the grid shows the same number of cards it showed when I left, both immediately and after its background refresh lands

#### Scenario: Returning to where I was in a year
- **WHEN** I go back to a year I had scrolled far down
- **THEN** the page is at the position I left it at and stays there once the refresh completes

#### Scenario: A refresh cannot shorten a scrolled grid
- **WHEN** I have scrolled a year deep into its listing and its visit-triggered refresh then completes
- **THEN** the grid still shows everything it was showing, rather than being cut back to a screenful

### Requirement: Year sorting
The system SHALL allow sorting the year's anime by popularity, MAL score, alphabetically, and my score, using the same rules and the same labels the season browser uses, applied across the whole year at once rather than within each season.

Sorting by popularity SHALL place unranked anime — MAL popularity rank absent or zero — after all ranked anime, then ranked anime by ascending rank, then by title. Sorting by my score SHALL split the results into two ordered groups: first every anime I have scored, by my score descending — ties broken by my ranking, best-ranked first, per the `anime-ranking` capability, then by popularity, then by title for anime my ranking does not cover; then every remaining anime by popularity under the same unranked-last rule. The page SHALL render a visual break labelled `Unwatched` between the two groups, shown only when both groups have at least one anime.

Every one of these orderings SHALL be produced server-side over the year's whole listing and SHALL reach the page as a per-anime ordering position rather than as a rule the page applies itself, exactly as the season browser's do. Changing the sort SHALL therefore take effect immediately, with no read and no loading state, and SHALL count as a fresh view of the page, returning the grid to the top on its first screenful.

#### Scenario: Sorting across the whole year
- **WHEN** I sort a year by MAL score
- **THEN** the highest-scored anime of the year leads, regardless of which of the year's seasons it aired in

#### Scenario: Sorting by my score
- **WHEN** I sort a year by my score
- **THEN** the anime I have scored appear first in descending score order, followed by an `Unwatched` divider, followed by the remaining anime in popularity order

#### Scenario: Equal scores follow my ranking
- **WHEN** I sort a year by my score and several of its anime share a score
- **THEN** they appear in my ranking's order rather than in popularity order

#### Scenario: My-score grouping holds all the way down
- **WHEN** I sort a year by my score and scroll to the end of the year
- **THEN** no scored anime appears after the `Unwatched` divider, anywhere in the grid

#### Scenario: No scored anime in the year
- **WHEN** I sort by my score in a year where I have scored nothing
- **THEN** all anime are shown in popularity order with no `Unwatched` divider

#### Scenario: Unranked anime sort last by popularity
- **WHEN** I sort a year by popularity and some anime have no popularity rank
- **THEN** the ranked anime appear first in ascending rank order and the unranked anime appear last

#### Scenario: Sorting is instant
- **WHEN** I change the sort on a year that is already loaded
- **THEN** the grid re-orders immediately with no read and no loading state

#### Scenario: A sort is still a fresh view
- **WHEN** I change the sort after scrolling deep into a year
- **THEN** the re-sorted grid opens at the top on its first screenful

### Requirement: Year type and in-my-list filters
The system SHALL provide, beside the Year page's sort control, the same two filters the season browser provides: a multi-select Type filter using the same control and display labels, offering only the media types actually present in the year; and an "In my list" checkbox, checked by default, which when unchecked excludes every anime that has an entry in my list.

Both filters SHALL be applied to the year's already-loaded listing rather than through a read, so each takes effect immediately with no loading state, and each SHALL exclude exactly the anime it excludes today, with the count the page reports following the filtered set. The types offered SHALL be derived from the whole listing rather than from what the current selection leaves visible. Both SHALL be part of the page's URL state so they survive back-navigation, and toggling either SHALL count as a fresh view of the page, returning the grid to the top on its first screenful.

#### Scenario: Filtering a year to one type
- **WHEN** I select Movie in the Year page's type filter
- **THEN** only movie entries are shown for that year, and the count the page reports reflects only movies

#### Scenario: The filter holds all the way down
- **WHEN** I select a type and scroll to the end of the year
- **THEN** every card shown is of a matching type

#### Scenario: Only present types are offered
- **WHEN** a year's results contain no music videos
- **THEN** the type filter does not offer Music as an option for that year

#### Scenario: Selecting a type does not shrink the type options
- **WHEN** I select one type and the results narrow to it
- **THEN** the type filter still offers the year's other types, so I can change or clear my selection

#### Scenario: Both filters are instant
- **WHEN** I change the type selection or toggle "In my list" on a year that is already loaded
- **THEN** the grid updates immediately with no read and no loading state

#### Scenario: Excluding anime in my list
- **WHEN** I uncheck "In my list"
- **THEN** every anime that has an entry in my list is removed from the results, the reported count reflects the smaller set, and scrolling continues to show only anime not yet in my list

#### Scenario: Filters persist through back-navigation
- **WHEN** I set a type filter, uncheck "In my list", open an anime, then press the browser Back button
- **THEN** both selections are still applied and the filtered results are shown
