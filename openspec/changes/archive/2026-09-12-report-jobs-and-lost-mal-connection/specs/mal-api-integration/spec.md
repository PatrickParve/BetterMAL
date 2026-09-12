## ADDED Requirements

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

#### Scenario: A lost connection keeps me in the app
- **WHEN** I open the app while the connection is lost
- **THEN** the app opens normally rather than showing the connect screen

#### Scenario: A first run shows the connect screen
- **WHEN** I open the app and no login has ever been stored
- **THEN** the connect screen is shown

#### Scenario: The state reads without MyAnimeList
- **WHEN** the connection state is read while MyAnimeList is unreachable
- **THEN** it is answered from what the application stores, without a request to MyAnimeList
