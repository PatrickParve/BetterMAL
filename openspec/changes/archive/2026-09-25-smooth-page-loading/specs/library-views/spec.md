## MODIFIED Requirements

### Requirement: Fast switching between ranking lists
Switching between ranking lists SHALL NOT blank the page. A list already loaded during the current application session SHALL be shown immediately when re-selected, without a network request and without any loading state.

When a list not yet loaded in this session is selected, the page SHALL keep the previously shown list rendered — visually muted and non-interactive, so it is unmistakably not the list the highlighted button names — until the new list arrives, rather than replacing it with a loading message or an empty page. Only the page's very first load, when there is no list on screen at all, SHALL show a plain loading state, and it SHALL follow the `page-load-states` capability's loading presentation: nothing for that capability's delay, then a loading indicator.

When a list's first load in this session fails, the page SHALL NOT keep loading indefinitely. It SHALL show the `page-load-states` capability's failure state for the selected list, with Try again and the automatic retry when the server is reachable again. A previous list held muted on screen SHALL give way to that failure state, since it is not the list the highlighted button names. The failed load SHALL NOT be remembered: Try again, re-selecting the list, returning to the page, or the server becoming reachable again SHALL each request the list afresh, rather than reusing the failure for the rest of the session.

#### Scenario: Returning to an already-loaded list is instant
- **WHEN** I select Movie, then All, then Movie again
- **THEN** the second Movie selection renders immediately with no loading state and no further request

#### Scenario: Loading an unseen list keeps the current one visible
- **WHEN** I select a list for the first time this session and its data has not arrived yet
- **THEN** the list I was reading stays on screen, muted and not interactive, until the new list replaces it

#### Scenario: The selector stays usable while a list loads
- **WHEN** a newly selected list is still loading
- **THEN** I can select a different list without waiting for the first one to finish

#### Scenario: First arrival shows a loading state
- **WHEN** I open the Top anime page for the first time in a session, no list has loaded yet, and the list takes longer than the loading indicator's delay
- **THEN** a plain loading indicator is shown, because there is no previous list to keep on screen

#### Scenario: A failed first load ends in a failure state
- **WHEN** I open the Top anime page while the backend cannot be reached
- **THEN** the page shows the failure state with Try again rather than staying on its loading indicator

#### Scenario: A failed list loads once the server is back
- **WHEN** the Top anime page's first load failed and the backend then becomes reachable again
- **THEN** the page requests the list again by itself and shows it, without a browser reload

#### Scenario: A failure is not remembered for the session
- **WHEN** a list's load failed earlier in the session and I later select that list again
- **THEN** the list is requested afresh rather than the earlier failure being reused
