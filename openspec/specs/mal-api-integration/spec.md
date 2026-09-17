# mal-api-integration Specification

## Purpose
The mal-api-integration capability governs the app's contract with MyAnimeList's API: the two auth modes and PKCE sign-in, the token lifecycle and its background refresh, request pacing and burst handling, which fields each request selects, paging through a full-list read, and briefly caching a shared live search. A full detail fetch persists every related-anime edge it returns. A refused or expired sign-in is tracked through its own lost-connection lifecycle — recorded the moment it happens, blocking any further sign-in while lost, and cleared only by re-authorizing — with the resulting state reported outward.
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
The system SHALL refresh the access token with the stored refresh token. Normally this SHALL happen in the background, well ahead of the token's expiry, so a signed-in request does not have to wait for a refresh.

A signed-in request that finds the access token expired, or within minutes of expiring, MAY refresh it itself before it is sent. That happens, for example, when the application was not running while the background refresh would have renewed the token. A request answered `401` is refreshed as "A rejected access token is refreshed once before giving up" describes.

MyAnimeList issues a new refresh token on every refresh and refuses one that has already been used. Every refresh, from any path, SHALL therefore be serialized, so no refresh is ever sent with a refresh token that another refresh has already exchanged:
- at most one refresh SHALL be in progress at a time
- a caller that waited for another refresh SHALL read the stored login again before doing anything
- if that refresh succeeded, the caller SHALL use the login it stored instead of refreshing again
- if it was refused, the connection is lost, and "Nothing signs in while the connection is lost" applies
- if it got no answer or an outage status, the caller MAY attempt the refresh itself, as "A refused sign-in is recorded as a lost connection" allows

#### Scenario: Refresh before expiry
- **WHEN** the access token is at or near its expiry
- **THEN** a background process obtains a new access token and persists it

#### Scenario: A request finds the token expired
- **WHEN** a signed-in request is about to be sent, the stored access token has expired, and no refresh is in progress
- **THEN** the login is refreshed before the request is sent, and the request carries the new access token

#### Scenario: Starting after the token expired
- **WHEN** the application starts after being off past the access token's expiry, and the background refresh check and a startup job such as the initial import both need a refresh at the same time
- **THEN** exactly one refresh request reaches MyAnimeList's token endpoint, both use the login it stored, and the connection is not recorded as lost

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

### Requirement: Full-list reads follow MAL's paging
When the system reads a whole list from MAL, it SHALL fetch every page of it. The whole-list reads are my animelist (`GET /v2/users/@me/animelist`, for the initial import and reconciliation) and a season listing (`GET /v2/anime/season/{year}/{season}`, for the season browser). The read SHALL stop at the first page that has no `next` link, or that has no entries.

Each next request SHALL start at the `offset` given in the previous page's `paging.next` link, not at a fixed step past the previous request. A page can hold fewer entries than were asked for, and still cover the full range. Starting from MAL's own offset means no entry is skipped and none is read twice.

When that link carries no usable offset, the next request SHALL start one page size past the current request's offset. An offset is not usable when it is missing, is not a whole number, or is not greater than the current request's offset. The last case keeps the read moving forward.

The system SHALL build each next request itself, with its own field selection and `nsfw=true`, rather than requesting the `next` link as given.

#### Scenario: A short page is followed from MAL's offset
- **WHEN** a full read asks for a page of 100 at offset 0, and MAL returns 98 entries with a `next` link at offset 100
- **THEN** the next request starts at offset 100, and no entry appears twice in the result

#### Scenario: A next link without an offset
- **WHEN** a page's `next` link has no `offset` parameter
- **THEN** the next request starts one page size past the current request's offset

#### Scenario: A next link whose offset does not advance
- **WHEN** a page's `next` link gives an offset no greater than the current request's offset
- **THEN** the next request starts one page size past the current request's offset, and the read still ends when MAL returns a page without a `next` link

#### Scenario: The last page ends the read
- **WHEN** a page comes back with no `next` link
- **THEN** no further page is requested

#### Scenario: An empty page ends the read
- **WHEN** a page comes back with no entries, even with a `next` link
- **THEN** no further page is requested

#### Scenario: Requests keep the app's own parameters
- **WHEN** a page's `next` link names a different field selection, or leaves out `nsfw`
- **THEN** the next request still carries the system's own field selection and `nsfw=true`

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

### Requirement: A refused sign-in is recorded as a lost connection

The system SHALL record the MyAnimeList connection as **lost**, with the time it was noticed, when MyAnimeList's token endpoint refuses to refresh the app's login. A refusal is an answer of `400` or `401`: the status codes OAuth 2.0 uses for an expired or revoked grant, and for a client the server will not accept.

The system SHALL NOT record a lost connection when:
- the refresh gets no answer: the network is down, or the connection fails or times out
- the token endpoint answers with any other status, such as a `5xx`, `429` or `403`

Those are outages. Nothing SHALL be recorded, and the refresh SHALL be attempted again the next time a token is needed.

The distinction SHALL be made from what the token endpoint answers, never from how many refreshes have failed in a row.

#### Scenario: An expired refresh token
- **WHEN** a refresh is attempted and the token endpoint answers `400` because the refresh token has expired
- **THEN** the connection is recorded as lost, with the time

#### Scenario: A revoked login
- **WHEN** a refresh is attempted and the token endpoint answers `401`
- **THEN** the connection is recorded as lost, with the time

#### Scenario: The network is down
- **WHEN** a refresh is attempted and no answer arrives
- **THEN** nothing is recorded, and the connection still reads as connected

#### Scenario: MyAnimeList is having trouble
- **WHEN** a refresh is attempted and the token endpoint answers `503`
- **THEN** nothing is recorded, and the refresh is attempted again the next time a token is needed

### Requirement: A rejected access token is refreshed once before giving up

When a signed-in call is answered with `401`, the system SHALL refresh the login once, even though the stored access token has not reached its expiry. It SHALL then retry that call once with the new token. Calls rejected with the same token at the same time SHALL share one refresh.

- If the refresh is **refused**, the connection SHALL be recorded as lost (see "A refused sign-in is recorded as a lost connection"), and the call SHALL fail as authorization-required.
- If the refresh gets **no answer**, or an outage status, nothing SHALL be recorded and the call SHALL fail as it does today.
- A retried call that is rejected again SHALL NOT be retried a second time.

#### Scenario: A login revoked on MyAnimeList's site
- **WHEN** I revoked the app's access on MyAnimeList, and the app next makes a signed-in call
- **THEN** the call is answered `401`, the refresh is refused, and the connection is recorded as lost on that first call

#### Scenario: A stale access token is renewed
- **WHEN** a signed-in call is answered `401` and the refresh succeeds
- **THEN** the call is sent again with the new token and succeeds, and nothing is recorded as lost

#### Scenario: Concurrent rejections share one refresh
- **WHEN** two signed-in calls are rejected with the same token at the same time
- **THEN** one refresh is made, and both calls are retried with its token

### Requirement: Nothing signs in while the connection is lost

While the connection is recorded as lost, the system SHALL NOT send any signed-in request to MyAnimeList. It SHALL NOT attempt a refresh either, whether for a call or from the background refresh, since the stored login has already been refused.

- Signed-in calls SHALL fail as authorization-required without a request being sent.
- Public reads, which use only the client id, SHALL continue unaffected.
- My local edits SHALL keep being recorded, and SHALL stay pending as `mal-write-sync` requires of any push that cannot be made.

The lost state SHALL be stored with the login, so it survives a restart.

#### Scenario: A push while lost is not sent
- **WHEN** I edit an entry while the connection is lost
- **THEN** no request is sent to MyAnimeList, and the edit stays pending

#### Scenario: No refresh while lost
- **WHEN** the background refresh runs while the connection is lost
- **THEN** it makes no request to the token endpoint

#### Scenario: Lost survives a restart
- **WHEN** the application restarts while the connection is lost
- **THEN** the connection still reads as lost, with the time it was first noticed

#### Scenario: Public reads carry on
- **WHEN** I browse a season while the connection is lost
- **THEN** the season is fetched from MyAnimeList as usual

### Requirement: Re-authorizing restores the connection

Completing the authorization flow SHALL clear a lost connection, and signed-in calls SHALL resume with the new login. Nothing else SHALL clear it: not a restart, and not the passing of time.

#### Scenario: Re-authorizing clears the lost state
- **WHEN** the connection is lost and I complete re-authorization
- **THEN** the connection reads as connected, and signed-in calls are sent again

#### Scenario: Pending edits go out afterwards
- **WHEN** edits were left pending while the connection was lost and I re-authorize
- **THEN** the retry job pushes them on its next pass

#### Scenario: A restart does not clear it
- **WHEN** the connection is lost and the application restarts without re-authorizing
- **THEN** the connection still reads as lost

### Requirement: The connection state is reported

The system SHALL report the MyAnimeList connection as one of three states:

- **Connected**: a login is stored and has not been refused
- **Lost**: a login is stored but MyAnimeList has refused it, reported with the time that was noticed
- **Not connected**: no login is stored

Reading the state SHALL NOT contact MyAnimeList.

The first-run connect screen SHALL be shown only when the state is not connected. A lost connection SHALL leave me in the app, with my local data, where the Settings page explains it.

While the connect screen is shown, the app SHALL read the state again whenever I return to it — its window regaining focus, its tab becoming visible again, or the page being shown again by going Back to it — so that authorizing, which leaves the app's tab or happens in another, opens the app on my return without a reload. A read made on return that fails SHALL leave the connect screen in place rather than replacing it with the screen for an unreachable backend. Once the app is open, returning to it SHALL NOT read the state this way.

#### Scenario: A lost connection keeps me in the app
- **WHEN** I open the app while the connection is lost
- **THEN** the app opens normally rather than showing the connect screen

#### Scenario: A first run shows the connect screen
- **WHEN** I open the app and no login has ever been stored
- **THEN** the connect screen is shown

#### Scenario: The state reads without MyAnimeList
- **WHEN** the connection state is read while MyAnimeList is unreachable
- **THEN** it is answered from what the application stores, without a request to MyAnimeList

#### Scenario: Returning after authorizing opens the app
- **WHEN** the connect screen is showing, I complete authorization, and I return to the app's tab by switching to it or going Back to it
- **THEN** the app opens without my reloading the page

#### Scenario: Returning without authorizing
- **WHEN** the connect screen is showing and I leave and return to the tab without completing authorization
- **THEN** the connect screen is still shown

#### Scenario: A failed read on return keeps the connect screen
- **WHEN** the connect screen is showing and the read made on my return fails
- **THEN** the connect screen stays in place rather than the unreachable-backend screen being shown

