## ADDED Requirements

### Requirement: Backend reachability is tracked centrally from every API call
The system SHALL maintain a single app-wide reachability state for its own backend, updated by every API request the frontend makes, so that no page has to opt in to reporting failures.

A request SHALL mark the backend **unreachable** when:

- the request fails at the network level (connection refused, DNS failure, the browser being offline), or
- the response status is 500 or above.

A request SHALL mark the backend **reachable** when it receives any response with a status below 500, including 4xx responses. A 404 for an anime the app does not have, a 400 for an invalid edit, and a 401 from an expired token are ordinary application outcomes and SHALL NOT be treated as an outage — a response of any kind below 500 proves the backend answered.

Reporting SHALL happen for both successful and failed requests, and SHALL NOT change what any request returns or throws. Each page's existing error handling SHALL be unaffected.

#### Scenario: Backend process is down
- **WHEN** an API request fails because nothing is listening on the backend
- **THEN** the app-wide state becomes unreachable

#### Scenario: Server error
- **WHEN** an API request returns a 500 response
- **THEN** the app-wide state becomes unreachable

#### Scenario: Not-found response is not an outage
- **WHEN** an API request returns 404 because the requested anime is not in the app's data
- **THEN** the app-wide state is reachable, and the calling page handles the 404 as it does today

#### Scenario: Successful request restores the state
- **WHEN** an API request succeeds after an earlier failure
- **THEN** the app-wide state becomes reachable

#### Scenario: Error handling is unchanged
- **WHEN** a request fails and the calling page has its own catch handler
- **THEN** that handler runs exactly as before, and the page's behavior on failure is unchanged

### Requirement: Connection-lost notice
The system SHALL display a notice whenever the backend is unreachable, so the user is not left looking at stale data with no indication that anything is wrong.

The notice SHALL be a small fixed-position bar over the page rather than a full-page state, SHALL state that the server cannot be reached, and SHALL leave the current page's content readable and interactive beneath it.

The notice SHALL appear anywhere in the app, on any page, without the page having to be reloaded or navigated.

The notice SHALL be dismissible. Dismissing it SHALL hide it for the current outage only; a later transition from reachable back to unreachable SHALL show it again.

Exactly one notice SHALL be shown at a time regardless of how many requests fail — a backend that is down fails many requests at once, and each failure SHALL NOT produce its own message.

#### Scenario: Backend goes down mid-session
- **WHEN** I am using the app and the backend becomes unreachable
- **THEN** a notice appears telling me the server cannot be reached, and the page I am on stays readable beneath it

#### Scenario: Many requests fail at once
- **WHEN** a page issues several requests and all of them fail because the backend is down
- **THEN** one notice is shown, not one per failed request

#### Scenario: Dismissing the notice
- **WHEN** I dismiss the notice while the backend is still down
- **THEN** it disappears and does not come back for this outage

#### Scenario: Notice returns on a new outage
- **WHEN** I dismissed the notice, the backend recovers, and it later goes down again
- **THEN** the notice is shown again

### Requirement: The notice clears itself when the backend recovers
While the backend is unreachable, the system SHALL poll its health endpoint at a fixed short interval, so the notice disappears on its own once the backend is back without the user having to reload or navigate.

The poll SHALL run only while the backend is unreachable; while it is reachable, no polling requests SHALL be made.

A successful poll SHALL clear the notice through the same reachability tracking as any other request. A successful request made for any other reason SHALL also clear the notice, whether or not a poll has fired yet.

#### Scenario: Backend comes back while the app sits idle
- **WHEN** the notice is showing and the backend becomes available again
- **THEN** the next health poll succeeds and the notice disappears without any interaction from me

#### Scenario: No polling in the healthy case
- **WHEN** the backend is reachable
- **THEN** the app makes no health-poll requests

#### Scenario: Recovery via ordinary use
- **WHEN** the notice is showing and I navigate to a page whose data request succeeds
- **THEN** the notice disappears immediately rather than waiting for the next poll

#### Scenario: Polling stops after recovery
- **WHEN** the backend has recovered and the notice has cleared
- **THEN** the health polling stops

### Requirement: First-load failure keeps its full-page message
When the app's very first request fails — before the main shell has mounted and there is no page to show a notice over — the system SHALL keep showing its existing full-page "can't reach the backend" message rather than the notice bar.

The two SHALL NOT be shown at the same time: the full-page message covers the pre-shell case, and the notice covers every failure after the shell has mounted.

#### Scenario: Backend down at first load
- **WHEN** I load the app while the backend is down
- **THEN** the full-page "can't reach the backend" message is shown, and no notice bar appears over it

#### Scenario: Backend down after the app has loaded
- **WHEN** the app has loaded successfully and the backend later becomes unreachable
- **THEN** the notice bar is shown and the app stays on the page I was using
