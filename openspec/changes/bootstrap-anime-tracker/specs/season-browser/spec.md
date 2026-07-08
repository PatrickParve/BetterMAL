## ADDED Requirements

### Requirement: Full season listing with live-then-cached fetch
The system SHALL show all anime airing in the selected season (not just my list), fetching the season live from the API on first visit to that season and caching the results for subsequent visits.

#### Scenario: First visit to a season
- **WHEN** I open a season that has not been fetched before
- **THEN** the system fetches that season from the API, caches it, and displays all its anime

#### Scenario: Revisiting a cached, fully-past season
- **WHEN** I return to a season that has already fully aired and was previously cached
- **THEN** it is served from the cache indefinitely without any further live re-fetch

### Requirement: Daily refresh limited to current and upcoming seasons
The system SHALL, only for the current season and the immediately upcoming season, re-fetch that season's lean listing fields the first time it is visited on a local calendar day after its last fetch; same-day revisits are served from cache.

#### Scenario: New-day visit to the current season
- **WHEN** I open the current season's page and it has not been fetched yet on the current local calendar day
- **THEN** the system re-fetches the season's listing live and updates the cache

#### Scenario: Same-day revisit to the current season
- **WHEN** I reopen the current season's page again on the same local day
- **THEN** it is served from the cache without a live re-fetch

### Requirement: Season selection
The system SHALL allow changing the selected season.

#### Scenario: Changing season
- **WHEN** I select a different season
- **THEN** the page shows that season's anime

### Requirement: Season card content
The system SHALL show, on each season card, the anime's title, picture, episode count, and type (TV/movie/etc).

#### Scenario: Rendering a season card
- **WHEN** season anime are displayed
- **THEN** each card shows the anime's title, picture, episode count, and type

#### Scenario: Unknown episode count on a season card
- **WHEN** a season anime's total episode count is unknown
- **THEN** the card shows the episode count as `?`

### Requirement: Season filtering
The system SHALL allow filtering/sorting season anime by popularity, score, alphabetical, and my score, where my score is meaningful only for anime also in my list.

#### Scenario: Sorting by my score
- **WHEN** I sort by my score
- **THEN** anime in my list are ordered by my score and the ordering handles anime without my score

### Requirement: Infinite scroll
The system SHALL load season results with infinite scroll.

#### Scenario: Scrolling loads more
- **WHEN** I scroll to the end of the loaded season results
- **THEN** more results load automatically
