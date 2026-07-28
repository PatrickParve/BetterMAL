## MODIFIED Requirements

### Requirement: Requests select the fields the app persists
The system SHALL request from MAL the fields it stores. For the user animelist (`GET /v2/users/@me/animelist`), the request SHALL include each entry's `list_status` sub-fields (status, score, num_episodes_watched, start_date, finish_date, num_times_rewatched) and SHALL include `nsfw=true` so NSFW-rated titles are not silently omitted. Season listing requests (`GET /v2/anime/season/{year}/{season}`) SHALL likewise include `nsfw=true`, so a season's cached listing matches what MAL itself lists for that season instead of silently dropping NSFW-rated titles. Anime search requests (`GET /v2/anime?q=`) SHALL also include `nsfw=true`, so an anime that MAL lists in a season is findable by searching for it. For anime nodes, the requested fields SHALL include `alternative_titles` (English title) and `rating` (MAL's content rating, needed by the season browser's hentai filter), and — for full detail fetches — `average_episode_duration` and `source`.

#### Scenario: User list includes personal list status
- **WHEN** the system fetches the `@me` animelist
- **THEN** the request asks for `list_status` and each returned entry's status, score, and episodes-watched are populated from it

#### Scenario: NSFW-rated titles are not dropped
- **WHEN** the system fetches the `@me` animelist
- **THEN** the request includes `nsfw=true` and NSFW-rated titles are included in the result

#### Scenario: Season listings include NSFW-rated titles
- **WHEN** the system fetches a season listing page
- **THEN** the request includes `nsfw=true` and NSFW-rated titles MAL classifies under that season are returned and cached

#### Scenario: Search includes NSFW-rated titles
- **WHEN** the system runs a live MAL search for a query
- **THEN** the request includes `nsfw=true` and NSFW-rated matches are returned rather than silently omitted

#### Scenario: Nodes carry English title, duration, and source
- **WHEN** the system fetches an anime's full detail
- **THEN** the request asks for `alternative_titles`, `average_episode_duration`, and `source`, and those values are stored when MAL provides them

#### Scenario: Listing nodes carry the content rating
- **WHEN** the system fetches a listing page (season, search, ranking) or an anime's full detail
- **THEN** the requested fields include `rating` and the returned value is stored on the cached anime record
