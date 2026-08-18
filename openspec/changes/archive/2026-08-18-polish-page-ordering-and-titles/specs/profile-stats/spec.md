## MODIFIED Requirements

### Requirement: Most rewatched section
The system SHALL show a "Most rewatched" section on the profile page, positioned directly below the "My top anime" box and rendered in the same style as it: a horizontal strip of poster tiles, each tile linking to that anime's detail page and carrying a badge in the same position as the top-anime strip's score badge.

The section SHALL contain every list entry whose rewatch count is greater than zero, regardless of watch status, and SHALL exclude every entry with a rewatch count of zero. The badge on each tile SHALL show that entry's rewatch count.

The rewatch-count badge SHALL carry the same badge form as the top-anime strip's score badge — the same position, size, pill shape, and dark tint that keeps it legible over any poster, plus a matching border — but SHALL be rendered in white/silver rather than in either score colour role. It SHALL NOT use the "mine" purple or the "MAL" blue, so a rewatch count is never read as a score, while still reading as a deliberate badge rather than as plain text laid over the poster.

The strip SHALL be ordered by rewatch count descending. Ties SHALL be broken alphabetically by title, case-insensitively, and by nothing else — my score SHALL NOT participate in the ordering, so equally-rewatched entries read in one predictable sequence rather than one that shifts whenever a score changes. The strip SHALL NOT be capped at any size — every qualifying entry SHALL be reachable by scrolling the strip to its end.

The section SHALL be read-only: it SHALL NOT offer any reordering control, and its order SHALL be derived entirely from rewatch counts rather than from the persisted top-anime ordering.

#### Scenario: Ordering by rewatch count
- **WHEN** the profile page loads and I have rewatched several anime different numbers of times
- **THEN** the "Most rewatched" strip lists them from most rewatched to least rewatched

#### Scenario: The count badge carries its own colour
- **WHEN** I look at a tile in the "Most rewatched" strip
- **THEN** its rewatch count sits in a white/silver bordered badge of the same shape, size, and position as the top-anime strip's score badge, distinct from both score colours

#### Scenario: A rewatch count is not mistaken for a score
- **WHEN** I look at the "My top anime" and "Most rewatched" strips together
- **THEN** the top-anime badges read as scores in the purple "mine" role and the rewatch badges read as counts in white/silver, so the two are never confused

#### Scenario: The badge stays legible over any poster
- **WHEN** a rewatched tile's poster is bright, pale, or busy behind the badge
- **THEN** the badge's count remains legible against it

#### Scenario: Entries never rewatched are excluded
- **WHEN** an anime on my list has a rewatch count of zero
- **THEN** it does not appear in the "Most rewatched" strip

#### Scenario: Rewatched but not completed
- **WHEN** an anime has a rewatch count above zero and a status other than completed
- **THEN** it still appears in the "Most rewatched" strip

#### Scenario: Breaking a tie
- **WHEN** two anime have the same rewatch count
- **THEN** they appear in alphabetical order by title, regardless of how I have scored them

#### Scenario: A score change does not reorder the strip
- **WHEN** I change my score on an anime in the "Most rewatched" strip without changing its rewatch count
- **THEN** its position in the strip is unchanged

#### Scenario: Uncapped list scrolls to the end
- **WHEN** I have more rewatched anime than fit across the width of the box
- **THEN** the strip scrolls horizontally and reaches the last of them, with none dropped from the list

#### Scenario: No reorder control
- **WHEN** I look at the "Most rewatched" box
- **THEN** it offers no control for editing the order

#### Scenario: Opening an anime from the strip
- **WHEN** I click a tile in the "Most rewatched" strip
- **THEN** that anime's detail page opens
