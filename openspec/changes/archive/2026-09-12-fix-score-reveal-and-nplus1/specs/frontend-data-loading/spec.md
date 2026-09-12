## ADDED Requirements

### Requirement: A read is not re-issued for a URL change it does not depend on
A page's read SHALL be identified by a key that carries every route or query parameter the read's result depends on. While that key is unchanged and the page already holds the read's result, the client SHALL NOT re-issue the read because the browser's history entry changed — for example because a filter, sort, scope or other view control wrote itself into the URL. An unchanged key means, by that same contract, that the changed part of the URL does not describe the data, so re-reading it can only return what the page already has.

The result the page already holds SHALL be carried into the new history entry's own restore state, so that a later back or forward navigation to that entry is still seeded with it rather than left empty.

This SHALL NOT change what happens when the key itself changes — a new anime, a new season, a new search query still loads — nor the documented behaviour of a back/forward **restore**, which seeds from its snapshot and refreshes silently in the background. A page that needs fresh server state after a mutation SHALL continue to ask for it explicitly.

#### Scenario: A filter that writes to the URL does not refetch the page
- **WHEN** I toggle a filter on a page whose whole result set was fetched once, and that filter writes itself into the query string
- **THEN** no request is issued, and the page filters the data it already holds

#### Scenario: Dismissing a scope does not re-download the list
- **WHEN** I dismiss a scope chip on my list, which removes its parameters from the URL
- **THEN** the list is not re-read, and the rows already loaded stay on screen

#### Scenario: A changed key still loads
- **WHEN** I navigate from one anime's detail page to another's, changing the parameter the read's key carries
- **THEN** the second anime's data is read

#### Scenario: A restore still refreshes silently
- **WHEN** I go back to a history entry whose snapshot holds a page's data
- **THEN** the page renders from the snapshot immediately and its read re-runs in the background without a loading state

#### Scenario: Going back to a skipped entry is still seeded
- **WHEN** a URL change skipped a re-read and I later navigate back to that history entry
- **THEN** the page is seeded with the data it was showing rather than loading from empty
