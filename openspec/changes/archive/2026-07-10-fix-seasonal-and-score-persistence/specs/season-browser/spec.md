## MODIFIED Requirements

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

### Requirement: Season filtering
The system SHALL allow filtering/sorting season anime by popularity, score, alphabetical, and my score, where my score is meaningful only for anime also in my list. When sorting by popularity, anime that are unranked — MAL popularity rank absent or zero — SHALL be ordered after all ranked anime (a rank of `0` is not treated as "most popular"), then ranked anime by ascending rank (1 = most popular), then by title.

#### Scenario: Sorting by my score
- **WHEN** I sort by my score
- **THEN** anime in my list are ordered by my score and the ordering handles anime without my score

#### Scenario: Unranked anime sort last by popularity
- **WHEN** I sort by popularity and some anime have no popularity rank (rank absent or zero)
- **THEN** the ranked anime appear first in ascending rank order and the unranked anime appear last, rather than an unranked anime sorting to the top
