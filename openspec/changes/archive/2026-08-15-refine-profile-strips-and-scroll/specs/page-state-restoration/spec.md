## MODIFIED Requirements

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

### Requirement: Back/forward navigation restores a page rather than rebuilding it
The web client SHALL treat a page reached by back/forward navigation (the browser's back and forward buttons, keyboard shortcuts, or a trackpad swipe gesture) as a *restore* rather than a fresh visit. On a restore, the page SHALL render immediately from the data it held when the user navigated away, without an intermediate loading or empty state.

A page reached any other way — a navbar or in-page link, a typed URL, a reload — SHALL be a fresh visit: it loads from scratch with its default view state, exactly as it does today.

Restoration state SHALL be held in memory for the lifetime of the browser tab's application session and SHALL NOT be persisted; a reload SHALL start with nothing to restore.

How many history entries' state is retained SHALL be bounded so a long session cannot grow without limit. When that bound forces an entry's state to be dropped, the client SHALL drop the least recently visited entry rather than the oldest-created one, so an entry I am actively navigating back and forth through keeps its state.

#### Scenario: Returning to a page shows it instantly
- **WHEN** I open a page, navigate elsewhere, and then go back
- **THEN** the page appears already populated with the data it had, showing no loading text and no empty flash

#### Scenario: A fresh visit still loads from scratch
- **WHEN** I reach a page by clicking its navbar link rather than navigating back
- **THEN** the page loads its data as a fresh visit and shows its normal loading state

#### Scenario: A reload discards restorable state
- **WHEN** I reload the browser and then navigate back to a page I had visited before the reload
- **THEN** the page loads from scratch, because nothing from before the reload is restorable

#### Scenario: Never-visited page is not a restore
- **WHEN** back/forward navigation lands on a page that was never rendered in this session
- **THEN** the page loads from scratch as a fresh visit

#### Scenario: A page I keep returning to is not evicted
- **WHEN** I visit a page, then repeatedly open items from it and go back over a long session
- **THEN** that page keeps restoring with its data, position, and view controls, rather than eventually reloading from scratch because older entries pushed it out

## ADDED Requirements

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
