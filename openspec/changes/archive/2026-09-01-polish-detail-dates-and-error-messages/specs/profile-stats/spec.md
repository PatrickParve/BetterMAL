## ADDED Requirements

### Requirement: The rankings "See all" overlay is sized by its rows and carries no close control
The overlay a favourites ranking's "See all" control opens SHALL show **eight whole rows** when it opens, with no part of a ninth row visible beneath them and no row clipped part-way, so the list ends where a row ends rather than mid-poster. Its height SHALL be derived from the height of a row rather than from the viewport, so the same eight whole rows are shown at any window height; the rest of the ranking SHALL be reached by scrolling the list, which SHALL keep every row it holds. A ranking of fewer than eight rows SHALL show what it has without reserving the height of eight.

The overlay SHALL offer **no close control of its own**: neither a labelled Close button beneath the list nor an icon control in its corner. Its dismissals SHALL be the ones every overlay in the app already provides — Esc and a click outside it — and those SHALL be unchanged. The list SHALL therefore be the last thing in the overlay, with nothing below it, so the whole box is the ranking.

Nothing else about the overlay SHALL change: its rows, their order, their posters, their family colour and banding, and where following a row leads SHALL be exactly as they are.

#### Scenario: Eight whole rows on open
- **WHEN** I open the "See all" overlay on a ranking with more than eight qualifying rows
- **THEN** eight rows are visible in full, no part of a ninth is shown, and no row is cut through the middle

#### Scenario: Whole rows at any window height
- **WHEN** I open the same overlay in a taller or a shorter window
- **THEN** it still shows eight whole rows rather than a fraction of a row

#### Scenario: The rest is still reachable
- **WHEN** the ranking holds more rows than the eight shown
- **THEN** the list scrolls to the rest of them

#### Scenario: Fewer than eight rows
- **WHEN** the ranking holds six qualifying rows
- **THEN** the overlay shows those six without reserving the height of eight

#### Scenario: No close control is offered
- **WHEN** the overlay is open
- **THEN** no Close button is shown beneath the list and no ✕ is shown in its corner, and the list is the last thing in the overlay

#### Scenario: Esc and click-outside close it
- **WHEN** the overlay is open and I press Esc or click outside it
- **THEN** the overlay closes

#### Scenario: The rows are otherwise unchanged
- **WHEN** I open the "See all" overlay from **Favourite years**
- **THEN** its rows, their order, their posters, and its banded title in the year family are exactly as they were
