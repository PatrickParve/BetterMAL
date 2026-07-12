## ADDED Requirements

### Requirement: Navbar layout
The system SHALL provide a navbar with left buttons Home, Seasonal anime, Top anime, and Airing, and right controls Profile, the hide/unhide MAL-score toggle, and a Settings (gear icon) button.

#### Scenario: Navigating via the navbar
- **WHEN** I click a navbar button
- **THEN** I am taken to the corresponding page (Home, Seasonal, Top, Airing, Profile, or Settings)

#### Scenario: Opening settings from the gear
- **WHEN** I click the Settings gear icon
- **THEN** I am taken to the settings page

### Requirement: Type-ahead search, local-first with live fallback
The system SHALL provide a centered type-ahead search that is debounced, queries the local cache first using word-boundary prefix matching (the query matches the start of the title or the start of any word within the title), and falls back to a live API search only when no cached titles match, showing a dropdown of up to 5 matches with picture and title.

#### Scenario: Prefix match at the start of a title
- **WHEN** I type a query that is a prefix of a cached title
- **THEN** the dropdown shows up to 5 matching cached titles with picture and title, without a live call

#### Scenario: Prefix match on a word inside a title
- **WHEN** I type a query matching the start of a word inside a cached title, not the title's own start
- **THEN** that title is included among the matches

#### Scenario: Live fallback for uncached titles
- **WHEN** my query matches no cached titles by prefix
- **THEN** the system performs a debounced live API search and shows up to 5 matches with picture and title

### Requirement: Clickable anime cards everywhere
The system SHALL make anime cards clickable everywhere they appear, linking to that anime's detail page.

#### Scenario: Clicking any card
- **WHEN** I click an anime card on any page
- **THEN** I am taken to that anime's detail page
