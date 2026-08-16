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
The system SHALL offer an on-demand refresh action for a single anime that, at the moment it is requested, performs one MyAnimeList API call for that one anime and additionally re-fetches that one anime's airing data from AniList. Neither call SHALL touch any other anime.

When the MyAnimeList call succeeds but the AniList fetch fails, the action SHALL still report success, update the cached record and sync timestamps, and log the AniList failure.

#### Scenario: Refreshing one anime on demand
- **WHEN** the user triggers refresh on a single anime's detail page
- **THEN** the system makes one MyAnimeList API call for that anime, re-fetches that anime's airing data from AniList, and updates its cached record and sync timestamps

#### Scenario: Scope stays at one anime
- **WHEN** the user triggers refresh on a single anime's detail page
- **THEN** no other anime's cached record or airing data is fetched or modified

#### Scenario: AniList unavailable during an on-demand refresh
- **WHEN** the user triggers refresh, the MyAnimeList call succeeds, and the AniList fetch fails
- **THEN** the action reports success with updated metadata, the anime's stored airing rows are left unchanged, and the AniList failure is logged

### Requirement: Lean, visit-triggered refresh for browsed anime
The system SHALL refresh Season-page and Top-Anime-page listings by re-fetching only when the user visits the current season, the upcoming season, or a Top Anime ranking list on a local calendar day after that listing's last fetch, requesting only lean listing fields (title, picture, episode count, type, MAL score, rank/popularity) rather than full anime details. A season, or a ranking list, never visited SHALL never be proactively fetched.

Each selectable Top Anime ranking list — All, TV, Movie, OVA, Special, Popularity, and Favourite — SHALL be treated as its own listing for this purpose, with its own cached rows and its own last-fetched time, in the same way each (year, season) pair is its own listing. Fetching one list SHALL NOT mark any other list as fetched, and SHALL NOT invalidate another list's cache.

#### Scenario: First visit of a new local day
- **WHEN** I open the current season, the upcoming season, or a Top Anime ranking list and it has not been fetched yet on the current local calendar day
- **THEN** the system re-fetches that listing's lean fields live and updates the cache

#### Scenario: Same-day revisit serves cache
- **WHEN** I reopen a listing already fetched earlier the same local day
- **THEN** it is served from Postgres without a live re-fetch

#### Scenario: Lean refresh preserves existing rich fields
- **WHEN** a lean listing refresh updates an AnimeMetadata row that already has full detail fields populated
- **THEN** only the lean fields are updated and the existing rich detail fields are left unchanged

#### Scenario: One ranking list's fetch does not cover another
- **WHEN** the All ranking list was fetched earlier today and I select the Movie list for the first time today
- **THEN** the Movie list is fetched live and cached under its own last-fetched time, leaving the All list's cached rows untouched

#### Scenario: Unvisited listing stays unfetched
- **WHEN** a season, or a Top Anime ranking list, has never been visited
- **THEN** no background job fetches it on its behalf, and visiting a different ranking list does not fetch it either

### Requirement: Visit-triggered live fetches collapse concurrent duplicates
Every read endpoint that can live-fetch from MyAnimeList as a side effect of being visited — the single-anime detail read and the Top Anime ranking read, alongside the season listing that already does this — SHALL admit at most one such fetch per subject at a time. Concurrent requests for the same subject SHALL wait on the in-flight fetch rather than starting a second one.

After acquiring its turn, a waiting request SHALL re-evaluate the condition that triggers the fetch (the row is missing or detail-incomplete; the ranking list has not been fetched on the current local day) and SHALL serve the now-updated cache when the condition no longer holds. A subject SHALL be identified by what is being fetched — the anime id, the specific ranking list, the (year, season) pair — so fetches for different subjects still run independently. Two different Top Anime ranking lists are two different subjects.

A failed fetch SHALL release the turn without marking the subject as fetched, so the next request retries.

#### Scenario: Two overlapping detail reads make one MAL call
- **WHEN** two `GET /api/anime/{id}` requests for the same uncached anime arrive at the same time
- **THEN** one full-detail MAL fetch is made, the second request waits for it and then serves the freshly cached row, and only one set of writes reaches the database

#### Scenario: Two overlapping top-anime reads make one MAL call
- **WHEN** two `GET /api/top-anime` requests for the same ranking list arrive at the same time on a day that list has not been fetched yet
- **THEN** one ranking fetch is made and both requests are served from its result

#### Scenario: Different ranking lists still fetch in parallel
- **WHEN** `GET /api/top-anime` requests for two different ranking lists arrive at the same time, neither fetched yet today
- **THEN** neither waits on the other, since they are different subjects

#### Scenario: Different anime still fetch in parallel
- **WHEN** detail reads for two different uncached anime arrive at the same time
- **THEN** neither waits on the other, since they are different subjects

#### Scenario: A failed fetch is retried by the next request
- **WHEN** a visit-triggered fetch fails and another request for the same subject arrives afterwards
- **THEN** that request attempts the fetch again rather than being treated as already refreshed

### Requirement: Full detail fetch reserved for import, tiered refresh, and detail view
The system SHALL only fetch full anime-detail fields (genres, synopsis, background, studio, aired dates, broadcast schedule, related anime) via initial import, the nightly tiered my-list refresh, or the first time that specific anime's own detail page is opened (or its manual refresh action is used) — never as a side effect of a season or Top Anime listing refresh, and never on behalf of an anime the user has not opened. A related anime's media type is reported directly by MAL's `related_anime` data (via nested field selection) as part of that full-detail fetch, so no separate fetch of a related anime is ever made purely to learn its media type.

#### Scenario: Browsing a season does not trigger detail fetches
- **WHEN** the Season or Top Anime listing refreshes
- **THEN** no full anime-detail call is made for any anime in that listing as a result

#### Scenario: Related-anime media types come from the owning anime's own fetch
- **WHEN** an anime's detail page is opened and its full-detail fetch runs
- **THEN** every related anime's media type is resolved from that same fetch, and no additional MAL call is made for any related anime

