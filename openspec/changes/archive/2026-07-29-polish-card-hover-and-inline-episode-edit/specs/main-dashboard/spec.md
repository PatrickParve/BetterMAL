## MODIFIED Requirements

### Requirement: Currently watching horizontal carousel
The system SHALL show a "Currently watching" section on the main page as a horizontal row of cards navigable with left/right arrows. At most 5 cards SHALL be visible at once; the visible row SHALL be bounded to the width of 5 cards rather than growing with the number of entries. When there are more than 5 entries, the row SHALL scroll as a bounded list: it stops at the first card when scrolling left and at the last card when scrolling right, and SHALL NOT wrap around from the last card to the first or from the first card to the last. Each entry SHALL be rendered exactly once. The left/right arrows SHALL be shown only when the row has more entries than fit on screen (i.e. scrolling is actually possible); when all cards already fit, no arrows are shown. Clicking a card's picture or title SHALL open that anime's detail page. Each card SHALL have a plus control next to its episode count that increments episodes-watched, applying the started-date and debounced-sync logic; clicking the card's picture or title navigates and does not increment.

The row's scroll position SHALL be preserved when a card's episodes-watched changes: incrementing from the plus control SHALL update the card in place and leave the row scrolled exactly where it was, for any number of entries.

Each card SHALL show the standard watched/total progress bar directly in front of its episode count, on the same line, so the card reads as `[bar] watched/total [+]`. The bar SHALL be the same watched/total bar used on My List and the anime detail page — the accent fill measured as episodes watched out of total episodes — and SHALL show an empty track when the total episode count is unknown, with the count still reading `watched/?`. Incrementing from the plus control SHALL update the bar's fill together with the count.

The progress row — the bar, the `watched/total` count, and the plus control — SHALL NOT be part of the card's navigation link: clicking anywhere within that row SHALL NOT navigate to the detail page. The count itself SHALL be directly editable in place, per the "Inline editable episode count" requirement.

The row SHALL reserve enough space inside its scrollable area for hover treatments on the edge cards, so that hovering the first or last card's plus control (or the card itself) renders the full hover state without any part being clipped by the row's scroll boundary, both when the row is scrolled fully left and fully right.

#### Scenario: At most five cards visible
- **WHEN** the Currently watching section renders with more than 5 entries
- **THEN** at most 5 cards are visible at once and the remaining entries are reached by scrolling

#### Scenario: Scrolling stops at the end
- **WHEN** the row has more than 5 entries and I scroll right to the last card
- **THEN** the row stops there and does not continue back to the first card

#### Scenario: Scrolling stops at the start
- **WHEN** the row has more than 5 entries and I scroll left to the first card
- **THEN** the row stops there and does not continue back to the last card

#### Scenario: Each entry appears once
- **WHEN** the row has more than 5 entries and I scroll from the first card to the last
- **THEN** each currently-watching anime is shown exactly one time, with no duplicated copies of the list

#### Scenario: Scroll position survives an increment
- **WHEN** the row has more than 5 entries, I have scrolled to a later card, and I click that card's plus control
- **THEN** the count and bar update in place and the row stays scrolled where it was rather than jumping back to the first card

#### Scenario: Navigating the carousel
- **WHEN** the row has more entries than fit and I click the left or right arrow on the Currently watching row
- **THEN** the row scrolls to reveal more currently-watching cards

#### Scenario: Arrows hidden when everything fits
- **WHEN** there are 5 or fewer currently-watching cards and they all fit within the visible width
- **THEN** no left/right arrows are shown

#### Scenario: Opening a card
- **WHEN** I click a currently-watching card's picture or title
- **THEN** I am taken to that anime's detail page without changing its episode count

#### Scenario: Incrementing from the plus control
- **WHEN** I click the plus control next to a card's episode count
- **THEN** its episodes-watched increases by one and the started-date and debounced-sync rules are applied

#### Scenario: Progress bar in front of the count
- **WHEN** a currently-watching card renders for an anime where I have watched 3 of 12 episodes
- **THEN** a progress bar filled to 3/12 is shown immediately before the `3/12` count, with the plus control after it

#### Scenario: Progress bar with unknown total
- **WHEN** a currently-watching card renders for an anime with an unknown total episode count
- **THEN** its track is empty and the count reads `watched/?`

#### Scenario: Bar follows an increment
- **WHEN** I click the plus control on a currently-watching card
- **THEN** the bar's fill grows along with the incremented count without a page reload

#### Scenario: Clicking the progress bar does not navigate
- **WHEN** I click the progress bar or the empty space around it on a currently-watching card
- **THEN** I stay on the main page and am not taken to the anime's detail page

#### Scenario: Clicking the count does not navigate
- **WHEN** I click the `watched/total` count on a currently-watching card
- **THEN** I stay on the main page and the count becomes editable instead of navigating

#### Scenario: Plus control fully visible on the last card
- **WHEN** the row is scrolled fully to the right and I hover the last card's plus control
- **THEN** the whole control is visible, with no part of it cut off by the row's right edge

#### Scenario: Plus control fully visible on the first card
- **WHEN** the row is scrolled fully to the left and I hover the first card's plus control
- **THEN** the whole control is visible, with no part of it cut off by the row's left edge
