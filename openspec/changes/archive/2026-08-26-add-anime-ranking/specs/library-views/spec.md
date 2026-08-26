## MODIFIED Requirements

### Requirement: My list two-level sorting
The system SHALL order my list by a primary sort key with an optional tiebreaker key, both chosen in the filter bar, so orderings such as "my score, then MAL score" or "episodes watched, then MAL score" are expressible directly.

Both selectors SHALL offer: Alphabetical, My score, MAL score, Episodes watched, Progress, Total episodes, Airing status, Type, Start date, and Finish date. The tiebreaker selector SHALL additionally offer "none", which is its default, and SHALL NOT offer the key already chosen as primary.

Each key SHALL have a natural direction — descending for My score, MAL score, Episodes watched, Progress, Total episodes, Start date and Finish date; ascending for Alphabetical and Type; and Finished airing → Currently airing → Not yet aired for Airing status. A direction control SHALL flip the primary key between its natural direction and the reverse; the tiebreaker SHALL always apply in its own natural direction.

Entries missing the value being sorted on — no score, no start or finish date, an unknown total or unknown airing status — SHALL sort last regardless of the direction chosen, rather than leading the list when the direction is flipped. When the primary and tiebreaker keys both tie, entries SHALL fall back to alphabetical order, so the same list always renders in the same order.

Where **My score** is the primary or the tiebreaker key, entries left tied on it SHALL be separated by my ranking — best-ranked first — before that alphabetical fallback, per the `anime-ranking` capability. Rank SHALL apply as a tiebreaker does: always in its own natural direction, so flipping the primary direction to lowest-score-first still orders each score's entries best-ranked first. A tied entry with no rank SHALL sort after every ranked entry of the same score.

When Airing status is the primary key, the system SHALL continue to offer the existing control choosing which airing status is shown first, and SHALL order the remaining two statuses after it in their established cycle.

#### Scenario: Sorting by my score then MAL score
- **WHEN** I choose My score as the primary sort and MAL score as the tiebreaker
- **THEN** entries are ordered by my score highest first, and entries sharing the same score of mine are ordered among themselves by MAL score highest first

#### Scenario: Sorting by my score alone follows my ranking
- **WHEN** I choose My score as the primary sort with no tiebreaker
- **THEN** entries sharing a score appear in my ranking's order rather than alphabetically

#### Scenario: Ranking breaks a tie the tiebreaker could not
- **WHEN** I sort by My score with MAL score as the tiebreaker and two entries share both scores
- **THEN** they are ordered by my ranking rather than alphabetically

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
- **THEN** they appear in alphabetical order, and that order is the same every time the list renders

#### Scenario: Airing-status sort keeps its "show first" control
- **WHEN** Airing status is the primary sort key
- **THEN** a control offering which airing status to show first is shown, and choosing one orders that status's entries ahead of the other two

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

#### Scenario: Sorting by airing status
- **WHEN** grouping is off and I sort by airing status
- **THEN** each entry shows a rank number on the left, ordered by the chosen show-first status ahead of the other two

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
