## MODIFIED Requirements

### Requirement: Navbar layout
The system SHALL provide a navbar whose left buttons are, in order, Home, My List, Top, Season, and Airing, and right controls Profile, the hide/unhide MAL-score toggle, and a Settings (gear icon) button.

#### Scenario: Navigating via the navbar
- **WHEN** I click a navbar button
- **THEN** I am taken to the corresponding page (Home, My List, Top, Season, Airing, Profile, or Settings)

#### Scenario: My List is reachable from the navbar
- **WHEN** the navbar renders
- **THEN** a "My List" button appears in the left button group and navigates to the my-list page

#### Scenario: Opening settings from the gear
- **WHEN** I click the Settings gear icon
- **THEN** I am taken to the settings page

## ADDED Requirements

### Requirement: English title preferred for display
The system SHALL, wherever an anime title is displayed (Home, Season, Top, anime detail, Profile, My List, Airing, and search results), show the anime's English title when one is available, falling back to the default title otherwise.

#### Scenario: English title available
- **WHEN** an anime has a stored English title and its title is displayed anywhere
- **THEN** the English title is shown

#### Scenario: No English title
- **WHEN** an anime has no English title
- **THEN** the default title is shown
