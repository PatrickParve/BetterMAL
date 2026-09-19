## MODIFIED Requirements

### Requirement: My list two-level sorting
The system SHALL order my list by a primary sort key with an optional tiebreaker key, both chosen in the Sort group, so orderings such as "my score, then MAL score" or "episodes watched, then MAL score" are expressible directly.

Both selectors SHALL offer: Alphabetical, My score, MAL score, Popularity, Episodes watched, Progress, Total episodes, Type, Start date, and Finish date. The tiebreaker selector SHALL additionally offer "none", which is its default, and SHALL NOT offer the key already chosen as primary. Airing status SHALL NOT be offered as a sort key in either selector; airing status remains a filter (see "My list airing-status filter").

**Alphabetical** SHALL order entries by the title the row displays — the anime's English title when MyAnimeList has one, and its original title otherwise — so an alphabetical list reads in the order of the names on screen rather than in the order of names that are not displayed. The same displayed title SHALL be used wherever alphabetical order applies as a fallback.

**Popularity** SHALL order entries by their anime's MAL popularity rank — the popularity figure the app shows for an anime, where rank 1 is the anime with the most MAL members.

Each key SHALL have a natural direction — descending for My score, MAL score, Episodes watched, Progress, Total episodes, Start date and Finish date; most popular first (popularity rank 1 first, ascending by rank) for Popularity; and ascending for Alphabetical and Type.

A direction control beside the primary key SHALL flip the primary key between its natural direction and the reverse. It SHALL name, in words, the order it is currently producing for the current key — **Highest first** / **Lowest first** for My score, MAL score and Progress; **Most first** / **Fewest first** for Episodes watched and Total episodes; **Most popular first** / **Least popular first** for Popularity; **A–Z** / **Z–A** for Alphabetical and Type; **Newest first** / **Oldest first** for Start date and Finish date — rather than acting as a bare "Reverse" toggle, and SHALL hold one width whichever of these it shows. Every offered key SHALL have such a direction, so the control is never shown as unavailable. The tiebreaker SHALL always apply in its own natural direction.

Entries missing the value being sorted on — no score, no start or finish date, an unknown total or an unknown popularity rank — SHALL sort last regardless of the direction chosen, rather than leading the list when the direction is flipped. When the primary and tiebreaker keys both tie, entries SHALL fall back to alphabetical order by displayed title, so the same list always renders in the same order.

Where **My score** is the primary or the tiebreaker key, entries left tied on it SHALL be separated by my ranking — best-ranked first — before that alphabetical fallback, per the `anime-ranking` capability. Rank SHALL apply as a tiebreaker does: always in its own natural direction, so flipping the primary direction to lowest-score-first still orders each score's entries best-ranked first. A tied entry with no rank SHALL sort after every ranked entry of the same score.

#### Scenario: Airing status is not a sort key
- **WHEN** I open either the primary sort selector or the tiebreaker selector
- **THEN** neither offers Airing status, in any form, while the airing-status filter is still offered in the Filter group

#### Scenario: Alphabetical follows the displayed title
- **WHEN** I sort alphabetically and my list holds an entry whose English title is "Frieren: Beyond Journey's End" and whose original title is "Sousou no Frieren"
- **THEN** its row sits among the F's, where the title on the row puts it, not among the S's

#### Scenario: Alphabetical falls back to the original title
- **WHEN** I sort alphabetically and one entry's anime has no English title on MyAnimeList
- **THEN** it is placed by its original title, which is also the title its row shows

#### Scenario: Sorting by my score then MAL score
- **WHEN** I choose My score as the primary sort and MAL score as the tiebreaker
- **THEN** entries are ordered by my score highest first, and entries sharing the same score of mine are ordered among themselves by MAL score highest first

#### Scenario: Sorting by my score alone follows my ranking
- **WHEN** I choose My score as the primary sort with no tiebreaker
- **THEN** entries sharing a score appear in my ranking's order rather than alphabetically

#### Scenario: Ranking breaks a tie the tiebreaker could not
- **WHEN** I sort by My score with MAL score as the tiebreaker and two entries share both scores
- **THEN** they are ordered by my ranking rather than alphabetically

#### Scenario: Sorting by progress then MAL score
- **WHEN** I choose Episodes watched as the primary sort and MAL score as the tiebreaker
- **THEN** entries are ordered by episodes watched highest first, with ties broken by MAL score

#### Scenario: Sorting by popularity
- **WHEN** I choose Popularity as the primary sort
- **THEN** entries are ordered from most popular to least popular — an anime with popularity rank 12 before one with rank 340, and that before one with rank 5,000

#### Scenario: Reversing the popularity sort
- **WHEN** I flip the direction control while sorted by Popularity
- **THEN** entries are ordered from least popular to most popular, and the control reads Least popular first

#### Scenario: Popularity as the tiebreaker
- **WHEN** I choose My score as the primary sort and Popularity as the tiebreaker
- **THEN** entries sharing a score of mine are ordered among themselves most popular first

#### Scenario: Unknown popularity sorts last
- **WHEN** the list is sorted by Popularity, in either direction, and some entries' anime have no popularity rank recorded
- **THEN** those entries appear at the end of the list

#### Scenario: The direction control names the order
- **WHEN** I sort by My score
- **THEN** the direction control reads Highest first, and flipping it makes it read Lowest first and orders the list lowest score first

#### Scenario: The direction control is always available
- **WHEN** I switch the primary sort key to each key in turn
- **THEN** the direction control names an order for every one of them and is never shown as unavailable

#### Scenario: The direction control keeps its width
- **WHEN** I switch the sort key between Alphabetical, Popularity and Start date
- **THEN** the direction control reads A–Z, Most popular first and Newest first in turn, and the tiebreaker beside it does not move

#### Scenario: Reversing the primary direction
- **WHEN** I flip the direction control while sorted by My score
- **THEN** entries are ordered by my score lowest first, and the tiebreaker still applies in its own natural direction

#### Scenario: Ranking is not reversed with the primary key
- **WHEN** I flip the direction control while sorted by My score
- **THEN** within each score the entries are still ordered best-ranked first

#### Scenario: An unranked entry among ranked ones
- **WHEN** a scored Plan-to-watch entry shares a score with ranked entries in a my-score sort
- **THEN** it appears after all of them

#### Scenario: Missing values sort last in either direction
- **WHEN** the list is sorted by a key some entries have no value for, in either direction
- **THEN** the entries with no value appear at the end of the list

#### Scenario: Tiebreaker cannot repeat the primary key
- **WHEN** My score is the primary sort key
- **THEN** the tiebreaker selector does not offer My score

#### Scenario: Fully tied entries keep a stable order
- **WHEN** two entries tie on both the primary and tiebreaker keys, and neither carries a rank
- **THEN** they appear in alphabetical order by their displayed titles, and that order is the same every time the list renders

### Requirement: My list grouped and ordered by status
The system SHALL present my list as one list grouped and ordered as Currently watching → Rewatching → On hold → Plan to watch → Completed → Dropped, where each entry shows picture, title, type (TV/movie), progress, my score, MAL score (respecting the hide/unhide toggle), and an edit button. Rewatching sits directly after Currently watching because both are runs in progress. For entries in the **Plan to watch** group, each row SHALL additionally show an airing-status indicator alongside the type — **Not aired**, **Airing**, or **Aired** (mapped from the anime's `not_yet_aired`, `currently_airing`, and `finished_airing` values) — so the user can tell at a glance whether a queued show is already out, still airing, or has not yet started; when the airing status is unknown, no indicator is shown.

Rows outside Plan to watch SHALL also show the airing-status indicator while the user is working with airing status — that is, while the airing-status filter has a selection — since the indicator is the value being filtered on. Outside that case, rows in other status groups SHALL NOT show the indicator.

#### Scenario: Rendering the grouped list
- **WHEN** the my-list page loads
- **THEN** entries appear grouped in the order Currently watching, Rewatching, On hold, Plan to watch, Completed, Dropped, each showing picture, title, type, progress, my score, MAL score, and an edit button

#### Scenario: Rewatching sits with the in-progress groups
- **WHEN** my list holds both Rewatching and Completed entries
- **THEN** the Rewatching group appears directly after Currently watching, not beside Completed

#### Scenario: Plan-to-watch row shows airing status
- **WHEN** the Plan to watch group renders an entry whose anime has a known airing status
- **THEN** that row shows an airing-status indicator (Not aired, Airing, or Aired) next to the type

#### Scenario: Airing status shown while filtering by it
- **WHEN** the airing-status filter has a selection
- **THEN** every row with a known airing status shows the indicator, whatever its watch status

#### Scenario: Airing status otherwise only on Plan to watch
- **WHEN** no airing-status filter is selected
- **THEN** rows outside Plan to watch show the type without an airing-status indicator

#### Scenario: Unknown airing status shows no indicator
- **WHEN** an entry's anime has no known airing status
- **THEN** its row shows the type with no airing-status indicator

### Requirement: My list airing-status filter
The system SHALL provide a multi-select airing-status filter in the my-list filter bar offering Finished airing, Currently airing, Not yet aired, and — when the list contains one — entries whose airing status is unknown. The filter SHALL be available under every status tab, including All, not only under Plan to watch. It SHALL be the only airing-status control on the page: airing status narrows the list but does not order it.

The filter SHALL distinguish **All** from **None** as the `page-header-design` capability defines: on **All** — its state on a fresh visit — no airing restriction applies; with one or more statuses selected, only entries of those statuses are shown; on **None**, no entry passes the airing filter and the list reports that nothing matches the current filters.

The rows' airing-status indicator SHALL be shown whenever this filter is narrowing the list — that is, whenever it is on anything other than **All**.

#### Scenario: Filtering to still-airing shows
- **WHEN** I select Currently airing while the Watching status tab is active
- **THEN** only entries I am watching whose anime is still airing are shown

#### Scenario: Available under every status tab
- **WHEN** any status tab is active, including All
- **THEN** the airing-status filter is offered

#### Scenario: All applies no airing restriction
- **WHEN** the airing-status filter is on All
- **THEN** entries of every airing status are shown

#### Scenario: None excludes every entry
- **WHEN** I press **None** in the airing-status filter
- **THEN** no entries are shown and the page says nothing matches the current filters

#### Scenario: The indicator follows the filter being used
- **WHEN** the airing-status filter is on anything other than All
- **THEN** every row with a known airing status shows the indicator, whatever its watch status

### Requirement: Rank numbers when sorted by score
The system SHALL show rank numbers (e.g. `#1`) on the left of my-list entries when the list is flat — that is, when grouping by status is off — and the primary sort key is anything other than Alphabetical, since a rank against an alphabetical ordering carries no meaning. A grouped list SHALL NOT show rank numbers. In flat mode the system SHALL show a single header line naming the active status filter (or "All") above the list.

The number SHALL be the row's position in the list as currently filtered and sorted, counting from 1, and SHALL NOT be the anime's overall rank from the `anime-ranking` capability — a filtered list numbers what it shows.

The rank SHALL occupy a fixed-width column sized for the longest rank the list can produce (at least three digits), independent of the rank actually shown on a given row. The `#` SHALL start at the same horizontal position on every row, and the poster that follows SHALL start at the same horizontal position on every row, so a rank of any length neither shifts the posters out of alignment nor runs underneath one.

#### Scenario: Ranks on a flat sorted list
- **WHEN** grouping is off and I sort by MAL score, my score, episodes watched, or any key other than Alphabetical
- **THEN** each entry shows a rank number on the left

#### Scenario: Numbers count the filtered list
- **WHEN** grouping is off, I sort by my score, and a status filter hides everything above my 8s
- **THEN** the first row shown is numbered `#1`, whatever overall rank that anime holds

#### Scenario: No ranks when grouped
- **WHEN** grouping by status is on
- **THEN** no rank numbers are shown, whatever the sort key

#### Scenario: No ranks on an alphabetical flat list
- **WHEN** grouping is off and the primary sort key is Alphabetical
- **THEN** no rank numbers are shown

#### Scenario: Flat view shows an active-filter header
- **WHEN** the my-list page is in flat (ungrouped) mode
- **THEN** a header line above the list names the active status filter, or "All" when unfiltered

#### Scenario: Three-digit rank stays clear of the poster
- **WHEN** a ranked row's number reaches three digits (for example `#100`)
- **THEN** the whole number is visible beside the poster rather than overlapping or sliding under it

#### Scenario: Ranks and posters align down the list
- **WHEN** a ranked list contains rows with one-, two-, and three-digit ranks
- **THEN** every row's `#` starts at the same horizontal position and every row's poster starts at the same horizontal position

### Requirement: My list filter bar
The system SHALL present every my-list filter and sort control in one controls block directly below the status filter tabs, above the entries it applies to. The block SHALL appear exactly once on the page — never repeated per status group — and SHALL apply to every group on screen alike.

The block SHALL be divided into two groups, each on its own row and each carrying a visible label:

- a **Filter** group holding the controls that narrow which entries are shown — find in list, the type filter, the airing-status filter, and the score filter — in that order;
- a **Sort** group holding the controls that order and arrange them — the sort key, its direction, the tiebreaker, and the grouping choice — in that order.

No control SHALL sit in the other group's row, and the two groups SHALL stay on separate rows at every viewport width, so narrowing and ordering never read as one strip. Each group SHALL be exposed to assistive technology as a group named by its label.

When a group's controls do not fit on one line they SHALL wrap onto further lines within that group's own row, rather than overflowing, scrolling horizontally, or flowing into the other group. Where the viewport is too narrow to hold a group's label beside its controls, the label SHALL sit above them instead.

Using any control in the block SHALL NOT make another control in the block appear, disappear, or change size. Every control in both groups SHALL be present whatever the others are set to, and a control whose label varies with its value SHALL hold one width across those values, so the controls beside it never shift sideways and no row gains or loses a line because of a selection.

Every control in the block SHALL be rectangular and share the one control height the `page-header-design` capability defines for a filter cluster. The rounded pill shape SHALL be reserved for the status filter tabs, so a control that narrows or orders the list is never mistaken for a status tab.

The page SHALL offer a **Reset filters & sort** action that restores the page's default view — no text query, no type restriction, no airing restriction, no score restriction, alphabetical primary sort in its natural direction, no tiebreaker, grouped by status. The action SHALL be shown only while at least one of those is not at its default. It SHALL NOT change the selected status tabs or dismiss a recap scope, which are separate controls.

Wherever it is shown, the action SHALL be drawn in the app's accent colour as a filled call to action, so it is immediately tellable from the neutral buttons beside it rather than reading as one more of them. It SHALL keep the height, shape and position it would otherwise have, so its appearing or disappearing moves nothing around it, and it SHALL remain legible in both the light and the dark theme.

#### Scenario: One block for the whole page
- **WHEN** the my-list page renders with several status groups on screen
- **THEN** the controls block appears once below the status tabs, and no status group header carries its own copy of any filter or sort control

#### Scenario: Narrowing and ordering are two labelled groups
- **WHEN** the my-list page renders
- **THEN** a row labelled Filter holds find in list, Type, Airing and Score, and a separate row labelled Sort below it holds the sort key, the direction control, the tiebreaker and the grouping choice

#### Scenario: A group wraps within its own row
- **WHEN** the viewport is too narrow to fit the Filter group's controls on one line
- **THEN** they wrap onto further lines under the Filter label, the Sort group stays on its own row below, and the page does not scroll horizontally

#### Scenario: Labels move above on a narrow viewport
- **WHEN** the viewport is too narrow to hold a group's label beside its controls
- **THEN** each group's label sits above that group's controls, and the two groups remain separate

#### Scenario: Choosing a sort key moves nothing
- **WHEN** I change the sort key from Alphabetical to Total episodes, and back
- **THEN** no control appears or disappears, and the direction control and the tiebreaker stay exactly where they were

#### Scenario: Only the status tabs are pills
- **WHEN** I look at the page above the list
- **THEN** the status tabs are the only rounded pills, and the direction control and the grouping choice are rectangular controls matching the selects beside them

#### Scenario: Resetting
- **WHEN** I have narrowed or reordered the list and use **Reset filters & sort**
- **THEN** the text query, type, airing-status and score filters are cleared, the sort returns to alphabetical in its natural direction with no tiebreaker, grouping by status is on, and the selected status tabs are left as they were

#### Scenario: Reset hidden at defaults
- **WHEN** every filter and sort control is at its default value
- **THEN** no **Reset filters & sort** action is shown

#### Scenario: Reset leaves a recap scope in place
- **WHEN** a recap scope is active and I use **Reset filters & sort**
- **THEN** the filters and sort return to their defaults and the list stays scoped to the same recap period

#### Scenario: Reset stands out from the button beside it
- **WHEN** the reset action is shown next to another page-level button
- **THEN** it is filled in the accent colour while the other stays neutral, so the two are tellable apart at a glance

#### Scenario: Reset appearing moves nothing
- **WHEN** I change a filter so the reset action appears
- **THEN** the buttons already on that row stay where they were and the row keeps its height
