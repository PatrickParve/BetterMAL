## MODIFIED Requirements

### Requirement: Opinion divergence lists
The system SHALL show two opinion-divergence lists comparing my score against MAL's score for anime I have rated: "They liked it, I didn't" and "I liked it, they didn't".

Membership SHALL be decided on *standardized* scores rather than the raw difference between them. MAL's community averages occupy a far narrower band than personal 1–10 scores, so an identical raw gap on both sides can never populate both lists; each score SHALL therefore be measured in standard deviations from the mean of its own scale. The population for both scales SHALL be the anime I have rated that also carry a MAL average — the same population both lists draw from — and the mean and standard deviation SHALL be recomputed from that population whenever the profile is built, so the rules track my list as it grows rather than sitting on fixed constants.

For each anime in that population, its divergence SHALL be how many standard deviations MAL's score sits above its own mean minus how many my score sits above mine. An anime SHALL qualify for a list when its divergence reaches 1.0 in that list's direction **and** it passes that list's label gate, which keeps the heading's claim honest about both parties:

- "They liked it, I didn't" — MAL's score is at least 7.5 and my score is at most 5.
- "I liked it, they didn't" — my score is at least 8 and MAL's score is at most 7.5.

The 5 and 8 boundaries are MAL's own score labels: 5 is "Average" and below it lies everything worse, 8 is "Very Good" and above it everything better. The scores between them — 6 ("Fine") and 7 ("Good") — are a neutral band, and an anime I scored in that band SHALL fall in neither list however far MAL's average sits from it. The 7.5 boundary splits MAL's community scale between its "Good" and "Very Good" labels.

Each list SHALL be ordered by divergence, strongest first, with ties broken by title case-insensitively. Neither list SHALL be capped by rank or count: subject only to the hidden-state rule below, every anime that qualifies SHALL be present, reachable by scrolling its box.

While the global hide-scores toggle is on, each list SHALL render only those of its qualifying anime whose entry is **Completed, Dropped, or Rewatching** — the statuses the "Always show MAL scores for completed and dropped shows" setting covers — and SHALL omit the rest of its rows **entirely**, rather than rendering them with a placeholder or a reveal control. Membership of either list is itself a claim about the anime's MAL score: appearing in "They liked it, I didn't" says the score is at least 7.5, and appearing in "I liked it, they didn't" says it is at most 7.5. A row therefore leaks a bound on the score it is hiding whatever its own score cell renders, and for an entry the user has not settled — Watching, On-hold, Plan to watch, or not in the list at all — that is a viewing still ahead of them, which is exactly what the hide toggle exists to protect. A placeholder row would not do: the leak is the row's presence, not its score cell.

Omission SHALL be confined to that state. While the hide toggle is off, both lists SHALL render every qualifying anime regardless of its status, since nothing needs withholding once scores are shown. Omission SHALL NOT change which anime qualify, the order they are ranked in, or the population the standardization is computed over — all three are decided before it and are unaffected by it. A list left with no rows to render SHALL show the same empty state it shows when nothing qualified.

Divergence needs a population to normalize against. When fewer than 10 anime carry both my score and a MAL average, or when either scale's standard deviation across that population is zero, both lists SHALL be empty rather than ranking on a spread that does not exist.

#### Scenario: They liked it, I didn't
- **WHEN** I rated an anime 3 and its MAL average is 8.70, more than 1.0 standard deviation apart once each score is standardized against its own scale
- **THEN** it appears in the "They liked it, I didn't" list

#### Scenario: I liked it, they didn't
- **WHEN** I rated an anime 9 and its MAL average is 6.31, more than 1.0 standard deviation apart once each score is standardized against its own scale
- **THEN** it appears in the "I liked it, they didn't" list

#### Scenario: Both lists populate
- **WHEN** my scores run lower on average and spread wider than the MAL averages of the same anime
- **THEN** neither list is starved by that difference in scale — each holds every anime that diverges by the same standardized amount in its own direction

#### Scenario: A score I did not dislike stays out
- **WHEN** an anime's MAL average diverges upward from my score by more than 1.0 standard deviation but I scored it 6
- **THEN** it does not appear in "They liked it, I didn't", because the list claims I didn't like it and a 6 does not say that

#### Scenario: A community score that is not a dislike stays out
- **WHEN** I scored an anime 10 and it diverges downward by more than 1.0 standard deviation, but its MAL average is 7.8
- **THEN** it does not appear in "I liked it, they didn't", because the list claims they didn't like it and a 7.8 average does not say that

#### Scenario: Strongest disagreement first
- **WHEN** either list renders
- **THEN** its rows run from the largest standardized divergence to the smallest

#### Scenario: Too little to normalize against
- **WHEN** fewer than 10 of my rated anime carry a MAL average
- **THEN** both lists are empty and each box shows its empty state

#### Scenario: An unsettled qualifying anime is omitted while scores are hidden
- **WHEN** the hide toggle is on and an anime that qualifies for a divergence list is one I am Watching, have On-hold, Plan to watch, or do not have in my list at all
- **THEN** no row for it appears in that list at all — not a placeholder row, not a row carrying a reveal control

#### Scenario: A settled qualifying anime still appears while scores are hidden
- **WHEN** the hide toggle is on and an anime that qualifies for a divergence list is one I have Completed, Dropped, or am Rewatching
- **THEN** its row appears as it does today, its MAL score following the "always show completed" setting exactly as before

#### Scenario: Hiding omits nothing once scores are shown
- **WHEN** the hide toggle is off
- **THEN** both lists show every qualifying anime, whatever its status, in the same order as before

#### Scenario: A list emptied by hiding shows its ordinary empty state
- **WHEN** the hide toggle is on and every anime qualifying for one of the lists is one I have not settled
- **THEN** that list shows the same empty state it shows when nothing qualified, rather than a message about withheld rows
