# mal-api-integration Specification

## Purpose
TBD - created by archiving change bootstrap-anime-tracker. Update Purpose after archive.
## Requirements
### Requirement: Public read endpoints use client-id auth only
The system SHALL call MAL API v2 public/read endpoints (anime search, season lists, ranking/top anime, anime details) using only the `X-MAL-Client-ID` header, without an OAuth token.

#### Scenario: Fetching public data without a token
- **WHEN** the system requests a season list, top-anime ranking, anime search, or anime details
- **THEN** the request includes the `X-MAL-Client-ID` header and no `Authorization` bearer token

#### Scenario: Public read succeeds while unauthorized
- **WHEN** no valid OAuth token is present
- **THEN** public/read endpoints still return data using the client-id header

### Requirement: User endpoints require OAuth2 bearer token
The system SHALL call MAL user-specific endpoints (get/update/delete list entries) using a valid OAuth2 (PKCE) bearer token tied to the account.

#### Scenario: Authorized user request
- **WHEN** the system fetches or modifies `@me` list data and a valid access token exists
- **THEN** the request includes the `Authorization: Bearer <token>` header

#### Scenario: Missing token blocks user endpoints
- **WHEN** a user-endpoint call is attempted without a valid token
- **THEN** the system does not send the request and surfaces an authorization-required state

### Requirement: One-time interactive PKCE authorization with plain challenge
The system SHALL provide a one-time interactive OAuth2 authorization flow that opens MAL's authorize URL, receives the redirect at the local callback, and uses `code_challenge_method=plain`.

#### Scenario: Completing authorization
- **WHEN** the user starts authorization and logs in on MAL
- **THEN** MAL redirects to `http://localhost:{port}/callback`, the system exchanges the code for tokens, and stores them

#### Scenario: Plain code challenge is enforced
- **WHEN** the authorization request is built
- **THEN** `code_challenge_method` is set to `plain` and never `S256`

### Requirement: Tokens persist in Postgres
The system SHALL store the access token, refresh token, and expiry in Postgres so they survive container restarts and rebuilds.

#### Scenario: Tokens survive restart
- **WHEN** the backend container restarts after a successful authorization
- **THEN** the previously stored tokens are loaded from Postgres and reused without re-authorizing

### Requirement: Background token refresh
The system SHALL refresh the access token using the stored refresh token as a background concern before or upon expiry, not on the per-request path.

#### Scenario: Refresh before expiry
- **WHEN** the access token is at or near its expiry
- **THEN** a background process obtains a new access token and persists it

### Requirement: Burst throttling and 403 handling
The system SHALL pace outbound API requests conservatively and treat a MAL `403` throttling response as a signal to back off and retry later rather than as a hard failure.

#### Scenario: Backoff on throttle
- **WHEN** MAL returns a `403` indicating a throttled burst
- **THEN** the system backs off and retries the request later instead of dropping the operation

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

