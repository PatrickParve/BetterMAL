## MODIFIED Requirements

### Requirement: Currently watching horizontal carousel
The system SHALL show a "Currently watching" section on the main page as a horizontal row of cards navigable with left/right arrows. The left/right arrows SHALL be shown only when the row's content overflows its visible width (i.e. scrolling is actually possible); when all cards already fit on screen, no arrows are shown. Clicking a card SHALL open that anime's detail page. Each card SHALL have a plus control next to its episode count that increments episodes-watched, applying the started-date and debounced-sync logic; clicking the card itself navigates and does not increment.

#### Scenario: Navigating the carousel
- **WHEN** the row overflows and I click the left or right arrow on the Currently watching row
- **THEN** the row scrolls to reveal more currently-watching cards

#### Scenario: Arrows hidden when everything fits
- **WHEN** all currently-watching cards fit within the visible width with no need to scroll
- **THEN** no left/right arrows are shown

#### Scenario: Opening a card
- **WHEN** I click a currently-watching card
- **THEN** I am taken to that anime's detail page without changing its episode count

#### Scenario: Incrementing from the plus control
- **WHEN** I click the plus control next to a card's episode count
- **THEN** its episodes-watched increases by one and the started-date and debounced-sync rules are applied
