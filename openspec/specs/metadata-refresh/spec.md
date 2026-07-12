# metadata-refresh Specification

## Purpose
TBD - created by archiving change bootstrap-anime-tracker. Update Purpose after archive.
## Requirements
### Requirement: No live API calls on page render
The system SHALL never call the MAL API live during a page render; cached metadata refresh SHALL happen only via scheduled background work or explicit on-demand actions.

#### Scenario: Rendering does not trigger a refresh
- **WHEN** a page renders cached anime data
- **THEN** no live MAL API call is made as part of that render

### Requirement: Scheduled tiered staleness refresh for my-list anime
The system SHALL run a scheduled background refresh (nightly) limited to anime with a corresponding UserAnimeEntry (my list), selecting refresh candidates by staleness tier: currently airing refreshes every 1 day; finished within the last 60 days refreshes every 3 days; finished more than 60 days but less than 1 year ago, or not yet aired, refreshes weekly; finished 1 year or more ago refreshes monthly.

#### Scenario: Airing anime refreshed daily
- **WHEN** a my-list anime is currently airing and its data is older than 1 day
- **THEN** the nightly job refreshes it

#### Scenario: Recently finished anime refreshed every few days
- **WHEN** a my-list anime finished within the last 60 days and its data is older than 3 days
- **THEN** the nightly job refreshes it

#### Scenario: Older anime refreshed weekly or monthly
- **WHEN** a my-list anime finished a year or more ago and its data is newer than its monthly threshold
- **THEN** the nightly job does not refresh it that night

#### Scenario: Non-my-list anime excluded from nightly tiers
- **WHEN** the nightly job selects refresh candidates
- **THEN** anime without a corresponding UserAnimeEntry are never included, regardless of staleness

### Requirement: Nightly batch cap ordered by staleness
The system SHALL cap the nightly refresh job at a fixed number of API calls per run (e.g. 500) and SHALL process the most-stale eligible candidates first across all tiers, carrying any overflow to the next night automatically without extra bookkeeping.

#### Scenario: Most-stale candidates processed first
- **WHEN** the nightly job selects candidates within the batch cap
- **THEN** candidates are ordered by longest time since last refresh first

#### Scenario: Overflow carries to the next night
- **WHEN** more candidates are eligible than the nightly batch cap allows
- **THEN** the remainder are simply refreshed on a subsequent night since they remain the most-stale candidates

### Requirement: Cheap, spread-out refresh requests
The system SHALL request only the specific fields needed for a refresh (for example just the `mean` score) and SHALL spread refreshes across the night in small batches rather than bursting.

#### Scenario: Minimal-field refresh
- **WHEN** only a score refresh is needed
- **THEN** the request asks for just the required field(s) rather than the full record

### Requirement: On-demand single-anime refresh
The system SHALL offer an on-demand refresh action for a single anime that performs one API call for that one anime at the moment it is requested.

#### Scenario: Refreshing one anime on demand
- **WHEN** the user triggers refresh on a single anime's detail page
- **THEN** the system makes one API call for that anime and updates its cached record and sync timestamps

### Requirement: Lean, visit-triggered refresh for browsed anime
The system SHALL refresh Season-page and Top-Anime-page listings by re-fetching only when the user visits the current season, the upcoming season, or the Top Anime ranking on a local calendar day after its last fetch, requesting only lean listing fields (title, picture, episode count, type, MAL score, rank/popularity) rather than full anime details. A season or ranking never visited SHALL never be proactively fetched.

#### Scenario: First visit of a new local day
- **WHEN** I open the current season, the upcoming season, or the Top Anime ranking and it has not been fetched yet on the current local calendar day
- **THEN** the system re-fetches that listing's lean fields live and updates the cache

#### Scenario: Same-day revisit serves cache
- **WHEN** I reopen a listing already fetched earlier the same local day
- **THEN** it is served from Postgres without a live re-fetch

#### Scenario: Lean refresh preserves existing rich fields
- **WHEN** a lean listing refresh updates an AnimeMetadata row that already has full detail fields populated
- **THEN** only the lean fields are updated and the existing rich detail fields are left unchanged

#### Scenario: Unvisited listing stays unfetched
- **WHEN** a season or ranking has never been visited
- **THEN** no background job fetches it on its behalf

### Requirement: Full detail fetch reserved for import, tiered refresh, and detail view
The system SHALL only fetch full anime-detail fields (genres, synopsis, background, studio, aired dates, broadcast schedule, prequel/sequel) via initial import, the nightly tiered my-list refresh, or the first time that specific anime's own detail page is opened (or its manual refresh action is used) — never as a side effect of a season or Top Anime listing refresh.

#### Scenario: Browsing a season does not trigger detail fetches
- **WHEN** the Season or Top Anime listing refreshes
- **THEN** no full anime-detail call is made for any anime in that listing as a result

