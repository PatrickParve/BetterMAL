## MODIFIED Requirements

### Requirement: Requests select the fields the app persists
The system SHALL request from MAL the fields it stores. For the user animelist (`GET /v2/users/@me/animelist`), the request SHALL include each entry's `list_status` sub-fields (status, score, num_episodes_watched, start_date, finish_date, num_times_rewatched) and SHALL include `nsfw=true` so NSFW-rated titles are not silently omitted. Season listing requests (`GET /v2/anime/season/{year}/{season}`) SHALL likewise include `nsfw=true`, so a season's cached listing matches what MAL itself lists for that season instead of silently dropping NSFW-rated titles. For anime nodes, the requested fields SHALL include `alternative_titles` (English title), and — for full detail fetches — `average_episode_duration` and `source`.

#### Scenario: User list includes personal list status
- **WHEN** the system fetches the `@me` animelist
- **THEN** the request asks for `list_status` and each returned entry's status, score, and episodes-watched are populated from it

#### Scenario: NSFW-rated titles are not dropped
- **WHEN** the system fetches the `@me` animelist
- **THEN** the request includes `nsfw=true` and NSFW-rated titles are included in the result

#### Scenario: Season listings include NSFW-rated titles
- **WHEN** the system fetches a season listing page
- **THEN** the request includes `nsfw=true` and NSFW-rated titles MAL classifies under that season are returned and cached

#### Scenario: Nodes carry English title, duration, and source
- **WHEN** the system fetches an anime's full detail
- **THEN** the request asks for `alternative_titles`, `average_episode_duration`, and `source`, and those values are stored when MAL provides them
