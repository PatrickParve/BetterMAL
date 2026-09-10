## MODIFIED Requirements

### Requirement: My list two-level sorting
The system SHALL order my list by a primary sort key with an optional tiebreaker key, both chosen in the filter bar, so orderings such as "my score, then MAL score" or "episodes watched, then MAL score" are expressible directly.

Both selectors SHALL offer: Alphabetical, My score, MAL score, Popularity, Episodes watched, Progress, Total episodes, Airing status, Type, Start date, and Finish date. The tiebreaker selector SHALL additionally offer "none", which is its default, and SHALL NOT offer the key already chosen as primary.

**Popularity** SHALL order entries by their anime's MAL popularity rank — the popularity figure the app shows for an anime, where rank 1 is the anime with the most MAL members.

Each key SHALL have a natural direction — descending for My score, MAL score, Episodes watched, Progress, Total episodes, Start date and Finish date; most popular first (popularity rank 1 first, ascending by rank) for Popularity; ascending for Alphabetical and Type; and Finished airing → Currently airing → Not yet aired for Airing status. A direction control SHALL flip the primary key between its natural direction and the reverse; the tiebreaker SHALL always apply in its own natural direction.

Entries missing the value being sorted on — no score, no start or finish date, an unknown total, an unknown popularity rank or unknown airing status — SHALL sort last regardless of the direction chosen, rather than leading the list when the direction is flipped. When the primary and tiebreaker keys both tie, entries SHALL fall back to alphabetical order, so the same list always renders in the same order.

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

#### Scenario: Sorting by progress then MAL score
- **WHEN** I choose Episodes watched as the primary sort and MAL score as the tiebreaker
- **THEN** entries are ordered by episodes watched highest first, with ties broken by MAL score

#### Scenario: Sorting by popularity
- **WHEN** I choose Popularity as the primary sort
- **THEN** entries are ordered from most popular to least popular — an anime with popularity rank 12 before one with rank 340, and that before one with rank 5,000

#### Scenario: Reversing the popularity sort
- **WHEN** I flip the direction control while sorted by Popularity
- **THEN** entries are ordered from least popular to most popular

#### Scenario: Popularity as the tiebreaker
- **WHEN** I choose My score as the primary sort and Popularity as the tiebreaker
- **THEN** entries sharing a score of mine are ordered among themselves most popular first

#### Scenario: Unknown popularity sorts last
- **WHEN** the list is sorted by Popularity, in either direction, and some entries' anime have no popularity rank recorded
- **THEN** those entries appear at the end of the list

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
