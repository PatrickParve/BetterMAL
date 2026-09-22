## MODIFIED Requirements

### Requirement: Back/forward navigation restores a page rather than rebuilding it
The web client SHALL treat a page reached by back/forward navigation (the browser's back and forward buttons, keyboard shortcuts, or a trackpad swipe gesture) as a *restore* rather than a fresh visit. On a restore, the page SHALL render immediately from the data it held when the user navigated away, without an intermediate loading or empty state.

A page reached any other way SHALL be a fresh visit: it opens on its default view state and loads its data from scratch, exactly as it does today. The other ways are a navbar or in-page link from another page, a typed URL, a reload, and the current page's own navbar link clicked while the page is already at its top. The one exception to loading from scratch is a fresh visit that lands on the page already on screen, for the same view whose data that page already holds: it SHALL show that data at once rather than a loading state.

Clicking the current page's own navbar link while that page is scrolled down SHALL NOT be a visit of any kind, as `navigation-and-search` sets out in "The current page's navbar link returns me to the top". The page, its history entry and its restorable state SHALL be left exactly as they are, and only its scroll position changes.

Restoration state SHALL be held in memory for the lifetime of the browser tab's application session and SHALL NOT be persisted; a reload SHALL start with nothing to restore.

How many history entries' state is retained SHALL be bounded so a long session cannot grow without limit. When that bound forces an entry's state to be dropped, the client SHALL drop the least recently visited entry rather than the oldest-created one, so an entry I am actively navigating back and forth through keeps its state.

#### Scenario: Returning to a page shows it instantly
- **WHEN** I open a page, navigate elsewhere, and then go back
- **THEN** the page appears already populated with the data it had, showing no loading text and no empty flash

#### Scenario: A fresh visit still loads from scratch
- **WHEN** I reach a page from another page by clicking its navbar link, rather than navigating back
- **THEN** the page loads its data as a fresh visit and shows its normal loading state

#### Scenario: The current page's link while scrolled is not a visit
- **WHEN** I scroll down a page and click that page's own navbar link
- **THEN** the page is neither reloaded nor reset, and going back afterwards leads to wherever it led before the click

#### Scenario: A reload discards restorable state
- **WHEN** I reload the browser and then navigate back to a page I had visited before the reload
- **THEN** the page loads from scratch, because nothing from before the reload is restorable

#### Scenario: Never-visited page is not a restore
- **WHEN** back/forward navigation lands on a page that was never rendered in this session
- **THEN** the page loads from scratch as a fresh visit

#### Scenario: A page I keep returning to is not evicted
- **WHEN** I visit a page, then repeatedly open items from it and go back over a long session
- **THEN** that page keeps restoring with its data, position, and view controls, rather than eventually reloading from scratch because older entries pushed it out

### Requirement: View-control selections are restored with the page
A page's view controls — filter tabs, media-type filters, sort selections, section filters, per-section expand/collapse states, and the like — SHALL be captured as part of that page's restorable state and SHALL be restored to their previously selected values when the page is restored.

A control that holds several selections at once, or a selection per section of the page, SHALL be restored whole rather than reduced to a single value.

On a fresh visit those same controls SHALL open on their documented defaults. A page SHALL NOT persist view-control selections across visits by any other mechanism. Clicking the current page's own navbar link while the page is scrolled down is not a visit, and SHALL leave every selection as it was.

A restored selection that names something the restored page no longer renders — a group that has since disappeared, an option no longer offered — SHALL be ignored rather than treated as an error.

#### Scenario: Filter survives a back navigation
- **WHEN** I select a non-default filter on a page, open an anime from it, and then go back
- **THEN** that filter is still selected and the page shows the filtered contents

#### Scenario: Two independent controls both restore
- **WHEN** a page has two independent view controls and I change both before navigating away
- **THEN** going back restores both to the values I selected

#### Scenario: A multi-selection restores whole
- **WHEN** I select several values in one multi-select control, navigate away, and go back
- **THEN** every one of those values is still selected

#### Scenario: A per-section state restores
- **WHEN** I collapse one section of a page and expand another, navigate away, and go back
- **THEN** each section is in the state I left it in

#### Scenario: A fresh visit opens on defaults
- **WHEN** I select a non-default filter, go to another page, and then reach that page again by clicking its navbar link
- **THEN** the filter is back on its default

#### Scenario: Returning to the top keeps my selections
- **WHEN** I select a non-default filter, scroll down, and click the navbar link for the page I am on
- **THEN** the page is back at its top with that filter still selected
