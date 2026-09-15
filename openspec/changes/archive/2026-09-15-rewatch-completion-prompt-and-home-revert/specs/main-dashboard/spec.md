## MODIFIED Requirements

### Requirement: Currently watching carousel ordering
The "Currently watching" carousel SHALL be ordered by episodes watched, highest first, so the shows I have invested the most viewing in lead the row. Entries with the same episodes-watched count SHALL be ordered alphabetically by title, case-insensitively, over the stored title rather than the display title — the same convention every other server-side ordering in the app uses — so the order does not shift with title-display preference.

The ordering SHALL be computed from the episodes-watched count alone and SHALL NOT be scaled by the anime's total episode count, so an entry whose total is unknown is ordered on the same footing as every other entry rather than being pushed to either end of the row.

The order SHALL be established server-side in the dashboard payload, so a freshly loaded carousel renders in its final order on first paint rather than reordering after mount.

Editing an episode count SHALL NOT reorder the row while I am looking at it. Incrementing from a card's plus control, or editing the count in place, SHALL update that card's count and bar where the card already sits, leaving every card's position and the row's scroll offset untouched — in keeping with the existing requirement that the row's scroll position survives an increment. This SHALL hold however far the new count would move that card had the row been re-ordered, and SHALL hold for repeated increments to the same card.

Committing a completion is not an in-place count edit and is exempt. Committing means completing an anime and then saving a score in the completion prompt that follows, by save or by save and rank. It moves that anime out of the "Currently watching" section entirely, which the page handles by reloading its dashboard data. The row MAY therefore reorder at that moment, since it is being rebuilt around a departing entry rather than nudged by a count change.

A completion that is **not** committed SHALL be treated as the ordinary in-place edit it is. That is a Rewatching entry that already has a score finishing with no prompt, or a completion whose prompt is closed without saving a score. The card SHALL update its count and bar where it sits and SHALL stay in the row, with no reload and no reorder. The entry is omitted from the section only when the dashboard is next loaded from the server. Until then its completion can be undone from the card, per "A completion left in Currently watching can be undone from its card".

Becoming caught up on an anime with episodes still to come is **not** such a departure and SHALL be treated as the ordinary in-place edit it is: the card SHALL update its count and bar where it sits and SHALL stay in the row, with no reload and no reorder. The entry is omitted from the section only when the dashboard is next loaded from the server, per "A caught-up entry leaves the currently-watching carousel" — a card the user has just incremented does not vanish from under the control they are pressing.

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

#### Scenario: Catching up does not remove the card under my hand
- **WHEN** I click the plus control on a card and thereby reach its anime's aired-so-far count while its total is higher
- **THEN** the card's count and bar update where it sits, the card stays in the row, and the row neither reloads nor reorders

#### Scenario: The caught-up card is gone on the next load
- **WHEN** I then reload the main page or come back to it
- **THEN** that show is no longer in the Currently watching row

#### Scenario: Completing an anime may reorder the row
- **WHEN** I increment a card to its final episode and save a score in the completion prompt
- **THEN** that anime leaves the "Currently watching" row and the remaining cards may settle into the new order, because the section is reloaded rather than patched

#### Scenario: A completion that is not committed stays under my hand
- **WHEN** I increment a card to its final episode and close the completion prompt with Cancel, Esc, or a click outside
- **THEN** the card shows its full count where it sits, stays in the row, and the row neither reloads nor reorders

#### Scenario: A reload picks up the new order
- **WHEN** I increment a card until it should lead the row, then reload the browser
- **THEN** the carousel comes back with that card first

#### Scenario: Coming back to the page picks up the new order
- **WHEN** I increment a card until it should lead the row, navigate to another page, and then return to the main page
- **THEN** the carousel shows that card first

#### Scenario: A restore reorders in place rather than flashing
- **WHEN** I increment a card, open that anime's detail page, and then press back to the main page
- **THEN** the carousel appears immediately in the order it had when I left, with no loading state, and settles into the new order in place once the background refresh returns

### Requirement: Rewatches appear in the currently-watching carousel

The main dashboard's "Currently watching" section SHALL include entries whose status is **Rewatching** alongside those whose status is Watching, since both are runs in progress and both are incremented from the same control.

A Rewatching card SHALL be rendered identically to a Watching one — same picture, title, progress row, and "+" control — and SHALL take its place in the section's existing ordering (episodes watched descending, then title) rather than being grouped separately or pinned. A Rewatching entry's anime has always finished airing, so no next-episode countdown applies to its card; its absence SHALL be the ordinary "no countdown known" case rather than a special one.

Incrementing a Rewatching card SHALL behave exactly as incrementing a Watching one, including the rule that finishing the run returns the entry to Completed and increases its rewatch count, per the `list-editing` capability. Finishing the run SHALL open the completion prompt only when the entry has no score. Once the run is finished the entry is no longer in progress:

- If a score is saved through that prompt, it SHALL leave this section at once, the same way a committed first completion does.
- Otherwise it SHALL leave on the next read of the dashboard. Until then its card SHALL stay where it sits at its full count, per "Currently watching carousel ordering".

#### Scenario: A rewatch is on the dashboard

- **WHEN** an entry's status is Rewatching
- **THEN** it appears in the Currently watching section, rendered like any other card there

#### Scenario: Ordered together, not grouped apart

- **WHEN** the section holds both Watching and Rewatching entries
- **THEN** they are ordered together by episodes watched descending and then title, with no separation between the two statuses

#### Scenario: Incrementing a rewatch from the dashboard

- **WHEN** I press "+" on a Rewatching card below the last available episode
- **THEN** its episodes watched increases by one and its bar and count update in place, exactly as for a Watching card

#### Scenario: A finished rewatch with a score stays until the next read

- **WHEN** I press "+" on a Rewatching card whose entry has a score, and thereby reach everything available
- **THEN** the entry returns to Completed with its rewatch count increased, no prompt opens, the card stays where it sits showing its full count, and it is no longer in Currently watching on the next read

#### Scenario: A finished rewatch without a score prompts

- **WHEN** I press "+" on a Rewatching card whose entry has no score, reach everything available, and save a score in the prompt that opens
- **THEN** the entry returns to Completed with its rewatch count increased and the score I saved, and the dashboard reloads without that card

## ADDED Requirements

### Requirement: A completion left in Currently watching can be undone from its card

The system SHALL let a completion that was left in the "Currently watching" section without being committed be undone from its card. A completion is left there in two ways:

- a Rewatching entry that already has a score finishes, with no prompt;
- the completion prompt is closed without saving a score, by Cancel, Esc, or a click outside.

Either way the card stays where it sits at its full count, per "Currently watching carousel ordering". The system SHALL treat that completion as possibly a mistake.

While I stay on the main page, lowering such a card's count SHALL undo the completion rather than follow the `list-editing` capability's rule for lowering a Completed entry's count. The one edit that lowers the count SHALL, together:

- save the lowered count as entered;
- put the status back to what it was before the completion: **Watching** for a completion reached from Watching, even where Rewatching would be permitted; **Rewatching** for a finished rewatch;
- put the rewatch count back to what it was before the completion. That takes back the increase "Watching a rewatch to the end counts it automatically" made. A completion reached from Watching never raised it, so it SHALL be left alone;
- put the finish date back to what it was before the completion: cleared where that completion filled it in, and kept where the entry already had one.

The start date and the score SHALL be left as they are. The undo is an ordinary edit. It SHALL be logged as activity and queued for MyAnimeList sync like any other edit, and SHALL NOT remove the completion's own activity row.

Once the undo is saved, the card SHALL be an ordinary card again. Raising it to the total SHALL complete it afresh, with whichever prompt behaviour applies. If the undo fails, the failure SHALL be reported as any failed count edit is. The card SHALL keep its full count, and lowering it again SHALL still undo the completion.

The undo SHALL apply only on the main page, and only while that page stays open. Once the dashboard is read from the server again, the section reflects the entry's stored state: a completed entry is no longer in it. Lowering the count of the same entry on my list or on its detail page SHALL follow the `list-editing` capability's rule as usual.

For each entry in the section, the dashboard payload SHALL carry its score and its finish date, empty where the entry has none. The score is what decides whether a finished rewatch opens the completion prompt. The finish date is what the undo puts back.

#### Scenario: Undoing a first completion left behind
- **WHEN** I press "+" on a Watching card for a 12-episode finished anime with no finish date to reach 12, cancel the completion prompt, and then set that card's count to 11
- **THEN** the entry is Watching with 11 episodes watched and no finish date, and its rewatch count is unchanged

#### Scenario: A finish date that was already there is kept
- **WHEN** a Watching card whose entry already has a finish date is completed from the carousel, the prompt is cancelled, and I then lower that card's count
- **THEN** the entry is Watching with the lowered count and the same finish date it had before

#### Scenario: Undoing a finished rewatch that had a score
- **WHEN** I press "+" on a Rewatching card with a score of 8 and a rewatch count of 1 to reach the total, no prompt opens, and I then set that card's count below the total
- **THEN** the entry is Rewatching with the lowered count, a rewatch count of 1, and the finish date it had before

#### Scenario: Undoing a finished rewatch whose prompt was cancelled
- **WHEN** a Rewatching card with no score reaches its total, I cancel the prompt, and I then lower that card's count
- **THEN** the entry is Rewatching with the lowered count and the rewatch count it had before the completion

#### Scenario: After the undo the card is ordinary again
- **WHEN** I undo a completion from a card and then raise that card to its total again
- **THEN** the entry completes again exactly as any completion from the carousel would

#### Scenario: A failed undo can be retried
- **WHEN** the edit lowering such a card's count fails
- **THEN** a failure notice is shown, the card keeps its full count, and lowering it again still undoes the completion

#### Scenario: Other pages follow the general rule
- **WHEN** an entry was completed from the carousel without being committed, and I lower its count from my list or from its detail page
- **THEN** it follows the `list-editing` rule for lowering a Completed entry's count, becoming Rewatching where that is permitted, with its rewatch count and finish date unchanged

#### Scenario: The next read ends it
- **WHEN** the dashboard is read from the server again after such a completion
- **THEN** the completed entry is no longer in Currently watching

#### Scenario: The payload carries score and finish date
- **WHEN** the dashboard is served with one currently-watching entry scored 7 with a finish date and one with no score and no finish date
- **THEN** the first item carries a score of 7 and its finish date, and the second carries neither
