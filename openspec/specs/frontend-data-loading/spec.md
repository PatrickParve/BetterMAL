# frontend-data-loading Specification

## Purpose
The frontend-data-loading capability governs the web client's read discipline: concurrent identical reads collapse into one request, a page load issues each distinct read exactly once, a mutation's own response updates state without a refetch, and a read is keyed only to the URL parts it actually depends on rather than re-issued on every change. It covers a fresh page load; restoring a page from back/forward navigation is page-state-restoration's.

## Requirements

### Requirement: Concurrent identical reads collapse into one request
The web client SHALL de-duplicate in-flight GET requests: when a GET for a given URL is issued while an identical GET is still in flight, the client SHALL return the pending request's result rather than opening a second network request. The entry SHALL be released as soon as the request settles, so a later read of the same URL is a fresh request and no response is cached beyond the lifetime of the in-flight call.

De-duplication SHALL apply to GET reads only. Mutating requests (POST, PATCH, PUT) SHALL never be collapsed, since two identical mutations are two intended actions.

A de-duplicated caller SHALL observe the same outcome as the caller it joined — the same resolved value, or the same rejection — so error handling at each call site is unaffected.

#### Scenario: Double-mounted page issues one request
- **WHEN** a page's load effect runs twice in quick succession for the same URL, as React's StrictMode does in development
- **THEN** exactly one network request is made and both callers resolve from it

#### Scenario: Sequential reads are not cached
- **WHEN** a page reads a URL, the request completes, and the same URL is read again later
- **THEN** a second network request is made, because de-duplication covers only overlapping requests and never serves a stale response

#### Scenario: A shared failure reaches every caller
- **WHEN** two callers join the same in-flight GET and the backend returns an error
- **THEN** both callers' promises reject, each running its own error handling

#### Scenario: Mutations are never collapsed
- **WHEN** the same PATCH is issued twice while the first is still in flight
- **THEN** two requests are sent, because collapsing them would silently drop an intended edit

### Requirement: A page load issues each distinct read once
Each page SHALL issue every distinct read it needs at most once per load. A page SHALL NOT re-issue a read because a derived value it computed from the response changed, and SHALL NOT issue a read that another read on the same page already covers.

Follow-on reads that depend on user action (opening an overlay, changing a filter, paginating) SHALL be issued when that action happens, not speculatively on load.

#### Scenario: Derived state does not retrigger a read
- **WHEN** a page merges fetched data into its state and that merged state feeds the effect that fetched it
- **THEN** no further request is issued, because the effect's trigger is the page's identity, not the fetched data

#### Scenario: Overlay data is fetched when the overlay opens
- **WHEN** a page has data needed only by an overlay the user has not opened
- **THEN** that data is not requested on page load, and is requested the first time the overlay opens

### Requirement: Mutation responses are applied without a refetch
When a mutation's response already carries the updated record, the client SHALL apply it to local state rather than re-reading the page's data to observe the same change. A page SHALL re-read after a mutation only when the mutation changes something the response does not describe — for example which section or grouping the item now belongs to.

This applies through shared overlays too: an overlay that saves on the user's behalf SHALL pass the saved record back to the page that opened it, so the page can patch instead of reload.

#### Scenario: Editing an entry patches in place
- **WHEN** the user changes episodes watched, score, or status and the update returns the saved entry
- **THEN** the page updates from the response and issues no additional read

#### Scenario: Completion prompt returns its saved entry
- **WHEN** the completion-score prompt saves a score after an anime is completed and then closes
- **THEN** the saved entry is handed back to the page that opened it, and a page showing only that one anime patches its state rather than re-reading the anime

#### Scenario: Regrouping still justifies a reload
- **WHEN** completing an anime moves it between sections of a list or dashboard whose grouping is computed server-side
- **THEN** that page may re-read, because the mutation response does not describe the new grouping

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
