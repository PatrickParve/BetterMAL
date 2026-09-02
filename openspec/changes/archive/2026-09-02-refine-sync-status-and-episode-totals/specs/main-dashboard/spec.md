## ADDED Requirements

### Requirement: A caught-up entry leaves the currently-watching carousel

The main dashboard's "Currently watching" carousel SHALL omit an entry that has nothing left to watch right now: one whose aired-so-far episode count is known and at least one, and whose episodes watched has reached or passed that count.

The rule SHALL read the aired-so-far count alone and SHALL NOT consult MyAnimeList's airing status, which is routinely still `currently_airing` weeks after a show has ended. What decides whether there is something to watch is how much has aired against how much has been seen, and that figure — resolved from stored AniList airing data per the `episode-airing-data` capability — does not lag.

This SHALL be a selection rule for this one section and nothing more. The entry's status SHALL remain **Watching**, unchanged, everywhere it is shown — My List, the anime detail page, the profile and activity surfaces, and MyAnimeList itself. Nothing about the entry is written when it is omitted; the section simply does not select it.

The entry SHALL return to the carousel as soon as there is something to watch again — the moment a further episode's stored air instant passes, on the next read of the dashboard, with no refresh, poll, or user action, exactly as an aired-episode count changes anywhere else. The omission SHALL follow the same read-time evaluation, so it needs no scheduled pass either.

An entry whose aired-so-far count is unknown SHALL NOT be omitted, since there is no figure to be caught up with. An entry that has watched its anime's full published run is not omitted by this rule either: it is completed by the `list-editing` capability, which removes it from this section by status rather than by omission. A **Rewatching** entry SHALL be treated exactly as a Watching one here — a rewatch that has reached everything aired likewise has nothing to watch right now — which in practice means one part-way through a finished run is shown, as it is today.

The section's ordering, card rendering, and every other requirement of this capability SHALL be unaffected — an omitted entry is simply not among the entries they operate on. The "Current season" section, which shows the same shows with their aired progress, SHALL be unaffected too, so a caught-up airing show remains visible on the main page there.

#### Scenario: A caught-up show is not in the carousel

- **WHEN** an entry is Watching at 7 episodes watched, its anime's total is 12, and its aired-so-far count is 7
- **THEN** the Currently watching carousel does not include it, and My List still shows it as Watching with 7 episodes watched

#### Scenario: It comes back when the next episode airs

- **WHEN** episode 8's stored air instant passes and I then load the main page
- **THEN** that show is in the Currently watching carousel again, with 7 of 8 aired shown on its card

#### Scenario: A stale airing status changes nothing

- **WHEN** a caught-up entry's anime is still reported by MyAnimeList as currently airing but its aired-so-far count has not grown
- **THEN** the entry stays omitted, because the rule reads the aired count rather than the airing status

#### Scenario: Being behind keeps a show in the carousel

- **WHEN** an entry is Watching at 5 episodes watched and its anime's aired-so-far count is 7
- **THEN** the carousel includes it as it does today

#### Scenario: An unknown aired count keeps a show in the carousel

- **WHEN** an entry is Watching on an anime for which no aired-so-far count is known
- **THEN** the carousel includes it

#### Scenario: A part-way rewatch stays in the carousel

- **WHEN** a Rewatching entry is at 3 episodes watched of a finished anime whose 12 episodes have all aired
- **THEN** the carousel includes it, because it has not reached the aired-so-far count

#### Scenario: Nothing is written by the omission

- **WHEN** the dashboard omits a caught-up entry from the carousel
- **THEN** no status change is recorded, no activity row is written, and nothing is queued for MyAnimeList sync

#### Scenario: The show is still on the main page

- **WHEN** a caught-up airing show is omitted from Currently watching
- **THEN** it still appears in the Current season section with its watched and aired progress

## MODIFIED Requirements

### Requirement: Currently watching carousel ordering
The "Currently watching" carousel SHALL be ordered by episodes watched, highest first, so the shows I have invested the most viewing in lead the row. Entries with the same episodes-watched count SHALL be ordered alphabetically by title, case-insensitively, over the stored title rather than the display title — the same convention every other server-side ordering in the app uses — so the order does not shift with title-display preference.

The ordering SHALL be computed from the episodes-watched count alone and SHALL NOT be scaled by the anime's total episode count, so an entry whose total is unknown is ordered on the same footing as every other entry rather than being pushed to either end of the row.

The order SHALL be established server-side in the dashboard payload, so a freshly loaded carousel renders in its final order on first paint rather than reordering after mount.

Editing an episode count SHALL NOT reorder the row while I am looking at it. Incrementing from a card's plus control, or editing the count in place, SHALL update that card's count and bar where the card already sits, leaving every card's position and the row's scroll offset untouched — in keeping with the existing requirement that the row's scroll position survives an increment. This SHALL hold however far the new count would move that card had the row been re-ordered, and SHALL hold for repeated increments to the same card.

Completing an anime is not an in-place count edit and is exempt: it moves that anime out of the "Currently watching" section entirely, which the page already handles by reloading its dashboard data. The row MAY therefore reorder at that moment, since it is being rebuilt around a departing entry rather than nudged by a count change.

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
