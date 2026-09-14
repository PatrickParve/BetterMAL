## ADDED Requirements

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

## MODIFIED Requirements

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
