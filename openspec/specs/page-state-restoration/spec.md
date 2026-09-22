# page-state-restoration Specification

## Purpose
The page-state-restoration capability governs back/forward navigation as a restore rather than a rebuild, for every routed page: an immediate render from held data followed by a background refresh, view-control selections, page and horizontal-strip scroll offsets, and which overlay was open and its own scroll offset, all restored together. It covers only what's restored and when; how an overlay behaves as a mechanism while it's open is overlay-behaviour's, and a fresh page load's own read discipline is frontend-data-loading's.
## Requirements
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

### Requirement: Restored data is refreshed in the background
When a page is restored, the client SHALL re-issue that page's reads in the background and replace the restored data with the response. The restored content SHALL remain visible and interactive throughout, so the refresh SHALL NOT blank the page, show a loading state, or reset the scroll position.

If a background refresh fails, the page SHALL keep showing the restored data rather than falling back to an empty or error state.

#### Scenario: Stale data is corrected without a flash
- **WHEN** I edit an anime on its detail page and then go back to a list that showed that anime's old values
- **THEN** the list appears immediately with its previous contents and updates in place once the background refresh returns

#### Scenario: Refresh does not disturb the view
- **WHEN** a restored page's background refresh completes
- **THEN** no loading state was shown and the scroll position and selected view controls are unchanged

#### Scenario: Failed refresh keeps the restored view
- **WHEN** a restored page's background refresh fails because the backend is unreachable
- **THEN** the restored data stays on screen and the page does not fall back to an empty state

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

### Requirement: Scroll position is restored with the page
The client SHALL record each history entry's scroll position when navigating away from it and SHALL restore that position when that entry is returned to by back/forward navigation. Restoration SHALL happen once the restored content has been laid out, so the target position is reachable rather than clamped to a shorter page.

The position recorded for a history entry SHALL be the position that entry was displayed at when it was left. Scrolling the client performs itself as part of a navigation — sending a freshly visited page to the top, or moving a restored page to its recorded position — SHALL be recorded against the entry being displayed at that moment and no other, so arriving at a new page can never overwrite the departing entry's recorded position.

A restore in progress SHALL be abandoned only when I scroll the page myself. The scrolling the restore performs SHALL NOT be mistaken for mine, and SHALL NOT cut the restore short before the recorded position is reached.

A fresh visit SHALL start at the top of the page. The client SHALL take over scroll handling from the browser's own restoration so the two do not compete.

#### Scenario: Returning to where I was
- **WHEN** I scroll far down a long list, open an item, and then go back
- **THEN** the list is scrolled to the same position I left it at

#### Scenario: Restoring below the fold of a page that loads asynchronously
- **WHEN** a restored page's content is taller than the viewport only after its data renders
- **THEN** the recorded scroll position is applied after that content is laid out, not clamped to the top

#### Scenario: The next page's scroll-to-top does not erase where I was
- **WHEN** I scroll down a page, immediately open an item from it, and the item's page opens at the top
- **THEN** going back returns me to the position I left the first page at, not to its top

#### Scenario: A restore is not cut short by its own scrolling
- **WHEN** a restored page needs several frames to reach its recorded position because its content is still settling
- **THEN** the restore keeps going until that position is reached, rather than stopping partway or at the top

#### Scenario: Scrolling myself during a restore wins
- **WHEN** I scroll the page myself while a restore is still settling
- **THEN** the restore stops immediately and leaves me where I scrolled to

#### Scenario: A fresh navigation starts at the top
- **WHEN** I follow a link from halfway down one page to another page
- **THEN** the new page starts at the top

### Requirement: Restoration applies to every routed page
Every routed page in the application SHALL participate in restoration — Home, Season, Year, Top, Airing, My list, Series, Profile, Settings, Search, and anime detail — so that navigating back from any page to any other page behaves the same way.

A page whose content depends on a route parameter or query string SHALL key its restorable state by that parameter, so returning to one anime's detail page does not restore another anime's data.

A page that renders only part of a loaded list, revealing more as it is scrolled, SHALL restore how much of that list had been revealed as well as the list itself — otherwise its recorded scroll position is unreachable on restore and is clamped back to the top.

#### Scenario: Any pair of pages
- **WHEN** I navigate from any page to any other page and then go back
- **THEN** the page I return to is restored, with no page behaving differently from the rest

#### Scenario: Parameterised pages do not cross-contaminate
- **WHEN** I open one anime's detail page, then another's, and then go back
- **THEN** the first anime's detail page is restored with that anime's data, not the second's

#### Scenario: The year browser restores the year it was left on
- **WHEN** I browse one year, open an anime, then go back, and later repeat this on a different year
- **THEN** each return restores the year it was left on with that year's results, rather than the current year or the other year's data

#### Scenario: Search results restore with their query
- **WHEN** I run a search, open a result, and go back
- **THEN** the query text and its results are both restored

#### Scenario: The Series page restores how far it had been revealed
- **WHEN** I scroll the Series page well past its first screen of cards, open a series, and go back
- **THEN** the same number of cards is rendered again and the page is returned to the scroll position I left it at, rather than being clamped back to the top

### Requirement: Horizontally scrolling strips restore their scroll offset
A horizontally scrollable strip on a restored page SHALL be returned to the horizontal offset it was left at, in the same way the page is returned to its vertical position. Restoration SHALL happen once the strip's restored contents have been laid out, so the recorded offset is reachable rather than clamped to a shorter strip.

Each strip on a page SHALL record and restore its own offset independently of every other strip and of the page's vertical position.

A strip whose contents depend on a view control SHALL restore the offset belonging to the restored value of that control, never an offset recorded under a different value.

On a fresh visit every strip SHALL start at its beginning.

#### Scenario: A strip is where I left it
- **WHEN** I scroll a poster strip well past its first tiles, open one of them, and then go back
- **THEN** the strip is still scrolled to where I left it, with the tile I opened where it was

#### Scenario: Strips and the page restore together
- **WHEN** I scroll the page down and scroll two different strips to different offsets before navigating away
- **THEN** going back restores the page's vertical position and both strips' offsets, each to its own value

#### Scenario: A restored offset belongs to the restored selection
- **WHEN** I scroll a strip under one media-type tab, switch to another tab, open an entry, and go back
- **THEN** the strip shows the tab I was on when I left, at that tab's own offset rather than the first tab's

#### Scenario: A fresh visit starts every strip at the beginning
- **WHEN** I reach the page by clicking its navbar link
- **THEN** every strip starts at its first tile

### Requirement: An overlay's open state is restorable page state
Whether one of a page's overlays is open SHALL be expressible as part of that page's restorable view state, and a page that records it SHALL show that overlay again when its history entry is returned to by back/forward navigation, so an overlay the user was looking at when they left the page is there when they come back to it.

An overlay restored this way SHALL be a new overlay opened over the page it belongs to, not an overlay that survived the navigation: leaving the page SHALL still close it, as the `overlay-behaviour` capability requires, and no other page's restore SHALL show it.

On a fresh visit the overlay SHALL be closed, as every other view control opens on its documented default. An overlay whose open state a page does not record SHALL continue to be closed on every restore.

Recording an overlay's open state SHALL NOT give it a history entry or an address of its own: opening it SHALL still not be something the back gesture stops at.

#### Scenario: An overlay is open again on return
- **WHEN** I open an overlay whose page records it, follow a link out of that overlay, and then go back
- **THEN** the overlay is open again over the page it belongs to

#### Scenario: A closed overlay stays closed
- **WHEN** I open such an overlay, close it, navigate away, and go back
- **THEN** the page is restored with no overlay over it

#### Scenario: A fresh visit opens with no overlay
- **WHEN** I reach the page by clicking its navbar link
- **THEN** no overlay is shown

#### Scenario: The overlay does not follow me elsewhere
- **WHEN** I leave a page that had a recorded overlay open and arrive somewhere else
- **THEN** nothing is shown over the page I arrive at

#### Scenario: Opening it is still not a history entry
- **WHEN** I open a recorded overlay and press the browser's back gesture
- **THEN** I leave the page rather than merely closing the overlay

### Requirement: A scrollable overlay restores its own scroll offset
An overlay restored with its page SHALL be returned to the scroll offset it was left at, in the same way the page is returned to its vertical position and a horizontally scrolling strip to its horizontal one. Restoration SHALL happen once the overlay's restored contents have been laid out, so the recorded offset is reachable rather than clamped to a shorter overlay.

Each restorable scroll offset a page records — its own, its strips', and its overlays' — SHALL be recorded and restored independently of the others, under one mechanism rather than a separate one per kind of scroller.

The page behind a restored overlay SHALL still be returned to its own recorded position, so closing the overlay reveals the page where it was left.

#### Scenario: The overlay is where I left it
- **WHEN** I scroll a restorable overlay well down its contents, follow a link from it, and then go back
- **THEN** the overlay is scrolled to the same place

#### Scenario: Restoring below the fold of an overlay that lays out late
- **WHEN** a restored overlay's contents are taller than it only once its pictures are laid out
- **THEN** the recorded offset is applied after that layout, not clamped to the top

#### Scenario: Page and overlay both restore
- **WHEN** I scroll the page down, open an overlay, scroll that too, then navigate away and come back
- **THEN** the overlay is at its offset and the page beneath is at its own

#### Scenario: A fresh visit starts at the top
- **WHEN** I reach the page by clicking its navbar link and open the overlay
- **THEN** the overlay opens at the top of its contents

