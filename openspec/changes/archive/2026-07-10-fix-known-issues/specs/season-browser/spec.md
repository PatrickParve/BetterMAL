## MODIFIED Requirements

### Requirement: Full season listing with live-then-cached fetch
The system SHALL show all anime whose premiere season is the selected season (not just my list), fetching the season live from the API on first visit to that season and caching the results for subsequent visits. Each anime SHALL appear in exactly one season — the season its start date falls in — even if it continued airing across later seasons; a long-running anime SHALL NOT appear in seasons after its premiere.

#### Scenario: First visit to a season
- **WHEN** I open a season that has not been fetched before
- **THEN** the system fetches that season from the API, caches it, and displays the anime whose premiere season is that season

#### Scenario: Long-running anime shown only in its premiere season
- **WHEN** an anime started airing well before the selected season but is still airing during it
- **THEN** it appears only in its premiere season and is not listed in the selected later season

#### Scenario: Revisiting a cached, fully-past season
- **WHEN** I return to a season that has already fully aired and was previously cached
- **THEN** it is served from the cache indefinitely without any further live re-fetch

### Requirement: Season selection
The system SHALL allow changing the selected season, and SHALL keep the selected season (and sort) in the page's URL so it persists through back-navigation from an anime detail page. Opening the Season page from the navbar with no season specified SHALL default to the current season. The system SHALL provide a quick-jump control to select a specific year and season directly, in addition to stepping one season at a time.

#### Scenario: Changing season
- **WHEN** I select a different season
- **THEN** the page shows that season's anime and the selection is reflected in the URL

#### Scenario: Selection persists through back-navigation
- **WHEN** I pick a season, open an anime, then press the browser Back button
- **THEN** I return to the season I had selected, not the current season

#### Scenario: Navbar defaults to current season
- **WHEN** I open the Season page from the navbar with no season in the URL
- **THEN** it defaults to the current season

#### Scenario: Quick-jump to a specific season
- **WHEN** I use the quick-jump control to choose a year and season
- **THEN** the page jumps directly to that season without stepping through intermediate seasons
