## MODIFIED Requirements

### Requirement: Currently watching horizontal carousel
The system SHALL show a "Currently watching" section on the main page as a horizontal row of cards navigable with left/right arrows. At most 5 cards SHALL be visible at once; the visible row SHALL be bounded to the width of 5 cards rather than growing with the number of entries. When there are more than 5 entries, the carousel SHALL scroll as an infinite loop in both directions: scrolling past the last card continues to the first card, and scrolling before the first card continues to the last card, with no dead-end and no visible seam. The left/right arrows SHALL be shown only when the row has more entries than fit on screen (i.e. scrolling is actually possible); when all cards already fit, no arrows are shown and no looping occurs. Clicking a card SHALL open that anime's detail page. Each card SHALL have a plus control next to its episode count that increments episodes-watched, applying the started-date and debounced-sync logic; clicking the card itself navigates and does not increment.

#### Scenario: At most five cards visible
- **WHEN** the Currently watching section renders with more than 5 entries
- **THEN** at most 5 cards are visible at once and the remaining entries are reached by scrolling

#### Scenario: Looping forward past the end
- **WHEN** the row has more than 5 entries and I scroll right past the last card
- **THEN** the row continues to the first card rather than stopping, so I can keep scrolling around the list

#### Scenario: Looping backward past the start
- **WHEN** the row has more than 5 entries and I scroll left before the first card
- **THEN** the row continues to the last card rather than stopping, so I can keep scrolling around the list

#### Scenario: Navigating the carousel
- **WHEN** the row has more entries than fit and I click the left or right arrow on the Currently watching row
- **THEN** the row scrolls to reveal more currently-watching cards

#### Scenario: Arrows hidden when everything fits
- **WHEN** there are 5 or fewer currently-watching cards and they all fit within the visible width
- **THEN** no left/right arrows are shown and the row does not loop

#### Scenario: Opening a card
- **WHEN** I click a currently-watching card
- **THEN** I am taken to that anime's detail page without changing its episode count

#### Scenario: Incrementing from the plus control
- **WHEN** I click the plus control next to a card's episode count
- **THEN** its episodes-watched increases by one and the started-date and debounced-sync rules are applied

## ADDED Requirements

### Requirement: Dashboard section title dividers
The system SHALL render a thin horizontal divider rule directly beneath the title of each dashboard section on the main page — "Currently watching", "Airing today", and "Followed shows airing" — visually separating the section heading from its content. The divider SHALL span the width of the section's content area.

#### Scenario: Divider under each section title
- **WHEN** the main page renders the "Currently watching", "Airing today", and "Followed shows airing" sections
- **THEN** each section's title is underlined by a thin horizontal divider rule separating the heading from the section content

#### Scenario: Divider spans the section width
- **WHEN** a dashboard section title divider is rendered
- **THEN** the divider spans the width of that section's content area
