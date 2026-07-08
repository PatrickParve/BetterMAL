## ADDED Requirements

### Requirement: Currently watching horizontal carousel
The system SHALL show a "Currently watching" section on the main page as a horizontal row of cards navigable with left/right arrows. Clicking a card SHALL open that anime's detail page. Each card SHALL have a plus control next to its episode count that increments episodes-watched, applying the started-date and debounced-sync logic; clicking the card itself navigates and does not increment.

#### Scenario: Navigating the carousel
- **WHEN** I click the left or right arrow on the Currently watching row
- **THEN** the row scrolls to reveal more currently-watching cards

#### Scenario: Opening a card
- **WHEN** I click a currently-watching card
- **THEN** I am taken to that anime's detail page without changing its episode count

#### Scenario: Incrementing from the plus control
- **WHEN** I click the plus control next to a card's episode count
- **THEN** its episodes-watched increases by one and the started-date and debounced-sync rules are applied

### Requirement: Next-episode countdown on currently-watching cards
The system SHALL show, on each currently-watching card, a countdown to the next episode in the form "Next ep: in X days, Y h", computed from the anime's cached broadcast schedule converted to local time.

#### Scenario: Showing the countdown
- **WHEN** a currently-watching card is rendered for an airing anime with a known broadcast schedule
- **THEN** it shows the time remaining until the next episode as "Next ep: in X days, Y h"

#### Scenario: No known next episode
- **WHEN** a currently-watching anime has no known upcoming broadcast (e.g. it has finished airing)
- **THEN** the card omits the next-episode countdown rather than showing a stale value

### Requirement: Airing today filtered to my list in local time
The system SHALL show an "Airing today" section as a list column (not a grid) containing only anime in my list, filtered by broadcast day converted from JST to local (Finland) time, where each row shows a small image and "time: Title" and links to the anime's detail page.

#### Scenario: Local-day airing filter
- **WHEN** the main page loads
- **THEN** "Airing today" lists only my-list anime whose broadcast day, converted to local time, is today, each as a row with a small image and "time: Title"

#### Scenario: Nothing airing today
- **WHEN** no my-list anime air today in local time
- **THEN** the section shows a small message indicating nothing is airing today

### Requirement: Current season section with filters and progress
The system SHALL show a "Current season" section containing only anime in my list that are airing this season, filterable by popularity, MAL score, alphabetical, and my score, with each card showing an episode-progress bar (watched/total, or watched/? when total is unknown).

#### Scenario: Filtering current season
- **WHEN** I choose a sort/filter (popularity, MAL score, alphabetical, or my score)
- **THEN** the current-season cards reorder accordingly

#### Scenario: Progress bar with unknown total
- **WHEN** a current-season card's anime has an unknown total episode count
- **THEN** its progress bar shows `watched/?`
