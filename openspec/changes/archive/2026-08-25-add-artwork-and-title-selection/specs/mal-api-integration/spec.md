## MODIFIED Requirements

### Requirement: Requests select the fields the app persists
The system SHALL request from MAL the fields it stores. For the user animelist (`GET /v2/users/@me/animelist`), the request SHALL include each entry's `list_status` sub-fields (status, score, num_episodes_watched, start_date, finish_date, num_times_rewatched) and SHALL include `nsfw=true` so NSFW-rated titles are not silently omitted. Season listing requests (`GET /v2/anime/season/{year}/{season}`) SHALL likewise include `nsfw=true`, so a season's cached listing matches what MAL itself lists for that season instead of silently dropping NSFW-rated titles. Anime search requests (`GET /v2/anime?q=`) SHALL also include `nsfw=true`, so an anime that MAL lists in a season is findable by searching for it. For anime nodes, the requested fields SHALL include `alternative_titles` (English title) and `rating` (MAL's content rating, needed by the season browser's hentai filter), and — for full detail fetches — `average_episode_duration` and `source`.

A full-detail fetch SHALL additionally request `pictures` **when, and only when, the anime being fetched is in my list**. MAL rate-limits per request rather than per field, so carrying the picture set on a fetch that is happening anyway costs nothing; requesting it for an anime that is not in my list would store artwork the app has decided not to keep. The field SHALL therefore be selected per anime rather than being fixed into one field list for all full-detail fetches.

A response that did not ask for `pictures` SHALL NOT be treated as reporting an empty picture set: a fetch that omitted the field SHALL leave any stored set, and its fetch timestamp, untouched.

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

#### Scenario: A my-list full fetch asks for pictures
- **WHEN** the system fetches full detail for an anime that is in my list
- **THEN** the request includes `pictures` and the returned URLs are stored as that anime's picture set

#### Scenario: A non-list full fetch does not
- **WHEN** the system fetches full detail for an anime that is not in my list
- **THEN** the request does not include `pictures`

#### Scenario: An omitted field does not clear a stored set
- **WHEN** an anime with a stored picture set is fetched by a request that did not ask for `pictures`
- **THEN** its stored picture set and its picture fetch timestamp are unchanged
