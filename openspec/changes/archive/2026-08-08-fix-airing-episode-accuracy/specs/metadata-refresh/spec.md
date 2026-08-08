## MODIFIED Requirements

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
