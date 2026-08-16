## MODIFIED Requirements

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
