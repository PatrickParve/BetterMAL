# season-browser Specification

## Purpose
TBD - created by archiving change bootstrap-anime-tracker. Update Purpose after archive.
## Requirements
### Requirement: Full season listing with live-then-cached fetch
The system SHALL show all anime whose MAL season classification (`start_season`) is the selected season (not just my list), fetching the season live from the API on first visit to that season and caching the results for subsequent visits. Season membership SHALL follow MAL's own `start_season` — the field MAL uses to build its per-season listings — which can differ from the calendar quarter the anime's start date falls in; the system SHALL NOT re-derive an anime's season from its start date. Each anime SHALL appear in exactly one season (the season MAL files it under); a long-running anime SHALL NOT appear in seasons after its premiere. The cached listing SHALL be the single source of truth read back to the page (no start-date re-filtering at read time).

#### Scenario: First visit to a season
- **WHEN** I open a season that has not been fetched before
- **THEN** the system fetches that season from the API, caches every anime MAL classifies under that season, and displays them

#### Scenario: Premiere season differs from the start-date quarter
- **WHEN** an anime's MAL `start_season` is the selected season but its start date falls in a different calendar quarter (e.g. an early-June start date that MAL files under Summer)
- **THEN** it still appears on the selected season, because membership follows MAL's classification rather than the start-date quarter

#### Scenario: Long-running anime shown only in its premiere season
- **WHEN** an anime started airing well before the selected season but is still airing during it
- **THEN** it appears only in its premiere season (its `start_season`) and is not listed in the selected later season

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

### Requirement: Season card content
The system SHALL show, on each season card, the anime's title, picture, episode count, and type (TV/movie/etc).

#### Scenario: Rendering a season card
- **WHEN** season anime are displayed
- **THEN** each card shows the anime's title, picture, episode count, and type

#### Scenario: Unknown episode count on a season card
- **WHEN** a season anime's total episode count is unknown
- **THEN** the card shows the episode count as `?`

### Requirement: Season filtering
The system SHALL allow filtering/sorting season anime by popularity, score, alphabetical, and my score, where my score is meaningful only for anime also in my list. When sorting by popularity, anime that are unranked — MAL popularity rank absent or zero — SHALL be ordered after all ranked anime (a rank of `0` is not treated as "most popular"), then ranked anime by ascending rank (1 = most popular), then by title.

#### Scenario: Sorting by my score
- **WHEN** I sort by my score
- **THEN** anime in my list are ordered by my score and the ordering handles anime without my score

#### Scenario: Unranked anime sort last by popularity
- **WHEN** I sort by popularity and some anime have no popularity rank (rank absent or zero)
- **THEN** the ranked anime appear first in ascending rank order and the unranked anime appear last, rather than an unranked anime sorting to the top

### Requirement: Infinite scroll
The system SHALL load season results with infinite scroll.

#### Scenario: Scrolling loads more
- **WHEN** I scroll to the end of the loaded season results
- **THEN** more results load automatically

