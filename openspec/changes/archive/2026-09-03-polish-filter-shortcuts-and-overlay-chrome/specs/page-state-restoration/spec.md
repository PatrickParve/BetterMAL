## ADDED Requirements

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
