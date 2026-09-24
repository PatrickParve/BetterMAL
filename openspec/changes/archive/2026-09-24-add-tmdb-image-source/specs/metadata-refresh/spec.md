## MODIFIED Requirements

### Requirement: On-demand single-anime refresh
The system SHALL offer an on-demand refresh action for a single anime. At the moment it is requested, the action SHALL:
- make one MyAnimeList API call for that one anime
- re-fetch that one anime's airing data from AniList
- refetch the TMDB sets the anime draws from (`tmdb-artwork`), whatever their age, when the anime is in my list, has a TMDB mapping, and TMDB access is configured

None of these calls SHALL modify any other anime's cached record or airing data. A TMDB set is shared by every anime that maps to it, so its refreshed images are offered to all of those anime.

The TMDB refetch SHALL belong to this on-demand action only. The scheduled tiered refresh, the first visit to a lean-only row, and the resolving fetch for a newly discovered relation SHALL NOT call TMDB.

When the MyAnimeList call succeeds but the AniList fetch or a TMDB fetch fails, the action SHALL still report success and update the cached record and sync timestamps. It SHALL leave as it was whatever the failed fetch would have replaced, and SHALL log the failure.

#### Scenario: Refreshing one anime on demand
- **WHEN** the user triggers refresh on a single anime's detail page
- **THEN** the system makes one MyAnimeList API call for that anime, re-fetches that anime's airing data from AniList, and updates its cached record and sync timestamps

#### Scenario: Scope stays at one anime
- **WHEN** the user triggers refresh on a single anime's detail page
- **THEN** no other anime's cached record or airing data is fetched or modified

#### Scenario: AniList unavailable during an on-demand refresh
- **WHEN** the user triggers refresh, the MyAnimeList call succeeds, and the AniList fetch fails
- **THEN** the action reports success with updated metadata, the anime's stored airing rows are left unchanged, and the AniList failure is logged

#### Scenario: TMDB sets are refetched on demand
- **WHEN** the user triggers refresh on a mapped anime in my list while TMDB access is configured
- **THEN** the TMDB sets that anime draws from are fetched again, even when they are less than 30 days old

#### Scenario: TMDB unavailable during an on-demand refresh
- **WHEN** the user triggers refresh, the MyAnimeList call succeeds, and a TMDB fetch fails
- **THEN** the action reports success, the anime's cached TMDB images are left unchanged, and the TMDB failure is logged

#### Scenario: Scheduled refreshes never call TMDB
- **WHEN** the scheduled tiered refresh refreshes a mapped anime in my list
- **THEN** no TMDB request is made
