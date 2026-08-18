## ADDED Requirements

### Requirement: Currently watching carousel ordering
The "Currently watching" carousel SHALL be ordered by episodes watched, highest first, so the shows I have invested the most viewing in lead the row. Entries with the same episodes-watched count SHALL be ordered alphabetically by title, case-insensitively, over the stored title rather than the display title — the same convention every other server-side ordering in the app uses — so the order does not shift with title-display preference.

The ordering SHALL be computed from the episodes-watched count alone and SHALL NOT be scaled by the anime's total episode count, so an entry whose total is unknown is ordered on the same footing as every other entry rather than being pushed to either end of the row.

The order SHALL be established server-side in the dashboard payload, so a freshly loaded carousel renders in its final order on first paint rather than reordering after mount.

Editing an episode count SHALL NOT reorder the row while I am looking at it. Incrementing from a card's plus control, or editing the count in place, SHALL update that card's count and bar where the card already sits, leaving every card's position and the row's scroll offset untouched — in keeping with the existing requirement that the row's scroll position survives an increment. This SHALL hold however far the new count would move that card had the row been re-ordered, and SHALL hold for repeated increments to the same card.

Completing an anime is not an in-place count edit and is exempt: it moves that anime out of the "Currently watching" section entirely, which the page already handles by reloading its dashboard data. The row MAY therefore reorder at that moment, since it is being rebuilt around a departing entry rather than nudged by a count change.

The new order SHALL instead take effect the next time the page's dashboard data is loaded from the server. That covers every route by which I come back to the page: a browser reload, a fresh visit through the navbar or a link, and the background refresh that runs when the page is restored by back/forward navigation. In the restore case the carousel SHALL first paint in the order it held when I left — the `page-state-restoration` capability's no-loading-state, no-flash guarantee is unaffected — and SHALL then settle into the new order in place when the refresh returns, rather than being held in the stale order until a full reload.

#### Scenario: Most-watched entries lead the row
- **WHEN** the main page loads and I am watching one anime with 1,050 episodes watched, one with 23, and one with 8
- **THEN** the carousel lists them in that order, from 1,050 down to 8

#### Scenario: Equal progress falls back to alphabetical
- **WHEN** two currently-watching anime both have 12 episodes watched
- **THEN** they appear next to each other in alphabetical order by title

#### Scenario: Ordering ignores the total episode count
- **WHEN** one currently-watching anime has 40 of 50 episodes watched and another has 100 of an unknown total
- **THEN** the one with 100 episodes watched is listed first, because ordering compares watched counts rather than how far through each show I am

#### Scenario: The row is already ordered on arrival
- **WHEN** I load the main page fresh
- **THEN** the carousel paints in its final order without a visible reorder after it appears

#### Scenario: An increment does not reshuffle the row
- **WHEN** I click the plus control on a currently-watching card whose new count would place it first in the order
- **THEN** the card's count and bar update where that card already sits, every card keeps its position, and the row stays scrolled where it was

#### Scenario: Repeated increments still do not reshuffle the row
- **WHEN** I increment the same card several times in a row without leaving the page
- **THEN** the card's count rises each time and the row's order and scroll offset are unchanged throughout

#### Scenario: Editing the count in place does not reshuffle the row either
- **WHEN** I edit a card's episode count directly rather than using the plus control
- **THEN** the card updates where it sits and the row does not reorder

#### Scenario: Completing an anime may reorder the row
- **WHEN** I increment a card to its final episode and confirm the completion prompt
- **THEN** that anime leaves the "Currently watching" row and the remaining cards may settle into the new order, because the section is reloaded rather than patched

#### Scenario: A reload picks up the new order
- **WHEN** I increment a card until it should lead the row, then reload the browser
- **THEN** the carousel comes back with that card first

#### Scenario: Coming back to the page picks up the new order
- **WHEN** I increment a card until it should lead the row, navigate to another page, and then return to the main page
- **THEN** the carousel shows that card first

#### Scenario: A restore reorders in place rather than flashing
- **WHEN** I increment a card, open that anime's detail page, and then press back to the main page
- **THEN** the carousel appears immediately in the order it had when I left, with no loading state, and settles into the new order in place once the background refresh returns

### Requirement: Followed shows airing sort is restored with the page
The "Followed shows airing" section's sort selection SHALL be captured as part of the home page's restorable view state, so returning to the home page by back/forward navigation restores the sort I had selected rather than resetting it to its default. This SHALL follow the `page-state-restoration` capability's rules for view controls exactly: it SHALL be restored on a back/forward navigation, and SHALL open on its documented default (Popularity) on a fresh visit.

#### Scenario: The sort survives opening an anime and coming back
- **WHEN** I set the "Followed shows airing" sort to MAL score, open one of its cards, and then go back
- **THEN** the sort is still MAL score and the cards are still ordered by it

#### Scenario: A fresh visit opens on the default sort
- **WHEN** I set the sort to Alphabetical, navigate away, and then reach the home page by clicking its navbar link
- **THEN** the sort is back on Popularity
