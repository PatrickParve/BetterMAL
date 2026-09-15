## MODIFIED Requirements

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
