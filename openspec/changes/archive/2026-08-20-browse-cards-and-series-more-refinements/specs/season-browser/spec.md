## MODIFIED Requirements

### Requirement: Season card content
The system SHALL show, on each season card, the anime's title, picture, episode count, type (TV/movie/etc), and MAL score.

The type and episode count SHALL sit at the leading edge of the card's meta line and the MAL score at its trailing edge, on that same line and sharing its baseline, so a column of cards reads as one row of figures per card rather than as a stack of separate lines. The score SHALL be rendered in the app's MAL colour, in the same two-decimal format every other MAL score uses.

An anime with no MAL score SHALL show nothing in that slot rather than a placeholder — an unrated or not-yet-aired title simply has no figure to report there, and the type and episode count keep their own position regardless.

While the global hide-scores toggle is on, the card SHALL show no score at all: no value, no placeholder, and no per-score reveal control, so a season of cards under hiding carries no score furniture. The type and episode count SHALL stay exactly where they are.

#### Scenario: Rendering a season card
- **WHEN** season anime are displayed
- **THEN** each card shows the anime's title, picture, episode count and type at the start of its meta line, and its MAL score at the end of that same line

#### Scenario: Unknown episode count on a season card
- **WHEN** a season anime's total episode count is unknown
- **THEN** the card shows the episode count as `?`

#### Scenario: An anime with no MAL score
- **WHEN** a season anime has no MAL score
- **THEN** its card's meta line shows only the type and episode count, with nothing in the score slot

#### Scenario: Hiding scores removes the card score entirely
- **WHEN** the global hide-scores toggle is on
- **THEN** no season card shows a MAL score, a placeholder, or a reveal control, and the type and episode count are unmoved
