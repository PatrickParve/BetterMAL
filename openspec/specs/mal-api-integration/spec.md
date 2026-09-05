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

### Requirement: Live search responses are briefly cached and shared
The system SHALL hold each successful live anime-search response (`GET /v2/anime?q=`) in memory for a short period — at most one minute — keyed by the query it was made for, and SHALL serve a repeat of the same query from that held response instead of making a second request. This exists because outbound MAL requests are paced (see "Burst throttling and 403 handling"), so a repeated query costs not only a network round trip but a place in that queue.

The type-ahead and the full search results page SHALL make the *same* live search for a given query — the same candidate count, so the same held response serves both. Submitting a query the type-ahead has already searched for SHALL therefore make no live request at all.

A failed live search SHALL NOT be held: a network blip or transient MAL error SHALL be retried on the next search for that query rather than being remembered for the rest of the period.

The held responses SHALL be bounded in number, so a session of many distinct searches cannot grow the app's memory without limit.

Nothing SHALL be persisted from a held response that would not be persisted from a fresh one, so a held response cannot put stale data into storage.

A live search whose caller has abandoned it — a type-ahead request the user has typed past or dismissed — SHALL be abandoned in turn rather than recorded as a live-search failure, and SHALL give up its place in the request pacing so whatever is queued behind it proceeds immediately. Every other failure SHALL keep degrading as it does today, with the caller falling back to locally stored anime.

#### Scenario: A repeated query makes no second request
- **WHEN** the same query is searched twice within the holding period
- **THEN** the second search is answered from the held response and no request reaches MAL

#### Scenario: Submitting reuses the type-ahead's search
- **WHEN** I type a query into the type-ahead and then submit it
- **THEN** the results page is served from the response the type-ahead's live search already fetched, without making a live request of its own

#### Scenario: A failure is not held
- **WHEN** a live search fails and the same query is searched again
- **THEN** a fresh request is made rather than the failure being repeated from memory

#### Scenario: Held responses are bounded
- **WHEN** many distinct queries are searched in one session
- **THEN** the number of held responses stays bounded, older ones being discarded

#### Scenario: An abandoned search is not a failure
- **WHEN** the client abandons a type-ahead request while its live search is waiting to be dispatched
- **THEN** the search is abandoned rather than logged as a live-search failure, and the next paced request proceeds without waiting for it

#### Scenario: A real failure still degrades
- **WHEN** a live search fails for a reason other than its caller abandoning it
- **THEN** the caller falls back to locally stored anime exactly as it does today

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

### Requirement: Full detail fetches persist every related-anime edge
A full-detail anime fetch SHALL persist every entry MyAnimeList returns in `related_anime`, keeping each entry's related anime id, title, picture, relation type as MAL reports it, MAL's ordering within that relation, and the related anime's media type. The system SHALL NOT discard relations it has no dedicated button for.

MAL's `related_anime` node **does** support nested field selection for `media_type` (`related_anime{node{media_type}}`), so a full-detail fetch SHALL request it and store the value MAL returns. An earlier statement in this specification that the media type is unavailable from this response was incorrect; it is available and is already requested, and no separate fetch of a related anime SHALL be made to learn it.

Relation types SHALL be stored as the raw MAL wire value (e.g. `prequel`, `sequel`, `side_story`, `parent_story`, `alternative_version`, `summary`, `spin_off`, `character`, `other`), so an unrecognized upstream relation is stored rather than dropped — mirroring how the other raw MAL vocabulary fields are handled. In particular, the stored value SHALL NOT be normalized or inverted at write time: reading an anime's relations in both directions is a read-time derivation, and rewriting an edge on the way in would destroy the record of which end MAL actually asserted it, which is exactly the signal used to judge whether the edge can be trusted.

A full-detail fetch SHALL replace the anime's stored related-anime entries wholesale, so a relation MAL no longer reports does not linger. A lean listing fetch (season, top-anime, search) SHALL NOT write or clear related-anime entries.

#### Scenario: Storing all relations
- **WHEN** the system fetches full detail for an anime whose `related_anime` contains a prequel, a sequel, two side stories, and a summary
- **THEN** all five entries are stored with their relation types, ids, titles, and media types

#### Scenario: Media type comes from the same response
- **WHEN** a full-detail fetch returns `related_anime` entries
- **THEN** each entry's `node.media_type` is stored alongside it, with no additional request for any related anime

#### Scenario: Unrecognized relation type
- **WHEN** MAL returns a relation type the app has no dedicated handling for
- **THEN** the entry is stored with that raw relation value rather than being dropped

#### Scenario: Edges are stored on the side MAL asserted them
- **WHEN** MAL reports `A --sequel--> B` and reports nothing about A on B's own record
- **THEN** one edge is stored, owned by A, and no inverted `prequel` edge is written onto B

#### Scenario: Relation removed upstream
- **WHEN** an anime's stored relations include one that MAL no longer reports and full detail is fetched again
- **THEN** the stale relation is removed from storage

#### Scenario: Lean fetch leaves relations alone
- **WHEN** an anime with stored relations is refreshed through a season, top-anime, or search listing fetch
- **THEN** its stored relations are neither written nor cleared

