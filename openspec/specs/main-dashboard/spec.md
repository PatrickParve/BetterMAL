# main-dashboard Specification

## Purpose
TBD - created by archiving change bootstrap-anime-tracker. Update Purpose after archive.
## Requirements
### Requirement: Currently watching horizontal carousel
The system SHALL show a "Currently watching" section on the main page as a horizontal row of cards navigable with left/right arrows. At most 5 cards SHALL be visible at once; the visible row SHALL be bounded to the width of 5 cards rather than growing with the number of entries. When there are more than 5 entries, the row SHALL scroll as a bounded list: it stops at the first card when scrolling left and at the last card when scrolling right, and SHALL NOT wrap around from the last card to the first or from the first card to the last. Each entry SHALL be rendered exactly once. The left/right arrows SHALL be shown only when the row has more entries than fit on screen (i.e. scrolling is actually possible); when all cards already fit, no arrows are shown. Clicking a card's picture or title SHALL open that anime's detail page. Each card SHALL have a plus control next to its episode count that increments episodes-watched, applying the started-date and debounced-sync logic; clicking the card's picture or title navigates and does not increment.

The 5-card bound SHALL be exact: at every resting scroll position, no part of a 6th card SHALL be visible. Whenever a card sits flush against the row's left edge — the row scrolled fully left, or scrolled by whole cards with the arrows — exactly that card and the 4 after it SHALL be visible, and the next card SHALL be entirely outside the visible strip. When the row is scrolled fully right, the last card SHALL likewise sit flush against the right edge with exactly the 4 cards before it visible and no sliver of a further card at either edge. The space the row reserves for edge-card hover treatments SHALL NOT count toward the visible strip's card budget.

The row's scroll position SHALL be preserved when a card's episodes-watched changes: incrementing from the plus control SHALL update the card in place and leave the row scrolled exactly where it was, for any number of entries.

Each card SHALL show the standard watched/total progress bar directly in front of its episode count, on the same line, so the card reads as `[bar] watched/total [+]`. The bar SHALL be the same watched/total bar used on My List and the anime detail page — the accent fill measured as episodes watched out of total episodes — and the count SHALL read `watched/?` when the total episode count is unknown. When the total is unknown and the anime is not currently airing, the track SHALL be empty. While the anime is currently airing, the bar SHALL additionally carry the blue aired fill specified by the "Broadcast progress on currently-watching cards" requirement, which also governs how the fills are measured when the total is unknown. Incrementing from the plus control SHALL update the bar's accent fill together with the count.

A card whose anime has aired no episode is the one exception to the two paragraphs above: it SHALL show no progress row at all — no bar, no count, and no plus control — per the "The editable progress row appears only once an episode has aired" requirement in the `list-editing` capability, since there is nothing on such a card to increment. The card SHALL still render its picture and title and SHALL still link to the detail page, and its next-episode countdown SHALL still be shown when one is known. The progress row SHALL return once the anime has aired an episode.

The progress row — the bar, the `watched/total` count, and the plus control — SHALL NOT be part of the card's navigation link: clicking anywhere within that row SHALL NOT navigate to the detail page. The count itself SHALL be directly editable in place, per the "Inline editable episode count" requirement.

The row SHALL reserve enough space inside its scrollable area for hover treatments on the edge cards, so that hovering the first or last card's plus control (or the card itself) renders the full hover state without any part being clipped by the row's scroll boundary, both when the row is scrolled fully left and fully right.

#### Scenario: At most five cards visible
- **WHEN** the Currently watching section renders with more than 5 entries
- **THEN** at most 5 cards are visible at once and the remaining entries are reached by scrolling

#### Scenario: No sliver of a sixth card at rest
- **WHEN** the row has more than 5 entries and is scrolled fully left
- **THEN** exactly 5 cards are visible and no part of the 6th card's picture is visible past the fifth

#### Scenario: No sliver after an arrow click
- **WHEN** the row has more than 5 entries and I click the right arrow so the row advances by one card
- **THEN** the newly leftmost card sits flush at the left edge, 5 cards are visible, and no part of the card before it or the card after them is visible

#### Scenario: Flush at the right end
- **WHEN** the row has more than 5 entries and I scroll or click right until it stops
- **THEN** the last card sits flush at the right edge with the 4 cards before it visible, and no sliver of an earlier card shows at the left edge

#### Scenario: Scrolling stops at the end
- **WHEN** the row has more than 5 entries and I scroll right to the last card
- **THEN** the row stops there and does not continue back to the first card

#### Scenario: Scrolling stops at the start
- **WHEN** the row has more than 5 entries and I scroll left to the first card
- **THEN** the row stops there and does not continue back to the last card

#### Scenario: Each entry appears once
- **WHEN** the row has more than 5 entries and I scroll from the first card to the last
- **THEN** each currently-watching anime is shown exactly one time, with no duplicated copies of the list

#### Scenario: Scroll position survives an increment
- **WHEN** the row has more than 5 entries, I have scrolled to a later card, and I click that card's plus control
- **THEN** the count and bar update in place and the row stays scrolled where it was rather than jumping back to the first card

#### Scenario: Navigating the carousel
- **WHEN** the row has more entries than fit and I click the left or right arrow on the Currently watching row
- **THEN** the row scrolls to reveal more currently-watching cards

#### Scenario: Arrows hidden when everything fits
- **WHEN** there are 5 or fewer currently-watching cards and they all fit within the visible width
- **THEN** no left/right arrows are shown

#### Scenario: Opening a card
- **WHEN** I click a currently-watching card's picture or title
- **THEN** I am taken to that anime's detail page without changing its episode count

#### Scenario: Incrementing from the plus control
- **WHEN** I click the plus control next to a card's episode count
- **THEN** its episodes-watched increases by one and the started-date and debounced-sync rules are applied

#### Scenario: Progress bar in front of the count
- **WHEN** a currently-watching card renders for an anime where I have watched 3 of 12 episodes
- **THEN** a progress bar filled to 3/12 is shown immediately before the `3/12` count, with the plus control after it

#### Scenario: Progress bar with unknown total
- **WHEN** a currently-watching card renders for an anime with an unknown total episode count that is not currently airing
- **THEN** its track is empty and the count reads `watched/?`

#### Scenario: No progress row before the first episode
- **WHEN** a currently-watching card renders for an anime that has aired no episode
- **THEN** the card shows no bar, no count, and no plus control, while still showing its picture, title, and any next-episode countdown

#### Scenario: The progress row returns with the first episode
- **WHEN** that anime airs its first episode and the dashboard is read again
- **THEN** its card shows the progress bar, count, and plus control as any other card does

#### Scenario: Bar follows an increment
- **WHEN** I click the plus control on a currently-watching card
- **THEN** the bar's accent fill grows along with the incremented count without a page reload

#### Scenario: Clicking the progress bar does not navigate
- **WHEN** I click the progress bar or the empty space around it on a currently-watching card
- **THEN** I stay on the main page and am not taken to the anime's detail page

#### Scenario: Clicking the count does not navigate
- **WHEN** I click the `watched/total` count on a currently-watching card
- **THEN** I stay on the main page and the count becomes editable instead of navigating

#### Scenario: Plus control fully visible on the last card
- **WHEN** the row is scrolled fully to the right and I hover the last card's plus control
- **THEN** the whole control is visible, with no part of it cut off by the row's right edge

#### Scenario: Plus control fully visible on the first card
- **WHEN** the row is scrolled fully to the left and I hover the first card's plus control
- **THEN** the whole control is visible, with no part of it cut off by the row's left edge

### Requirement: A card's hover treatment clears completely when the pointer leaves
The hover and keyboard-focus treatment an anime card wears — its tinted plate, its coloured border, and its elevation — SHALL be drawn while the card is hovered or focused and SHALL leave nothing behind once it is not. This SHALL hold for the whole treatment, including the part that extends beyond the card's own box, and in every browser the app is used in.

No further interaction SHALL be required to clear it: the treatment SHALL be gone as soon as the pointer has left, rather than persisting until the row is scrolled, another card is hovered, or the page is otherwise repainted. This SHALL hold for a card at the edge of a horizontally scrolling row as much as for one in the middle of it, and for the first card hovered in a session as much as for any later one.

#### Scenario: Leaving a card leaves no trace
- **WHEN** I hover a currently-watching card and then move the pointer away from it
- **THEN** the card and the space around it look exactly as they did before I hovered, with no line, tint, or edge left behind

#### Scenario: The first card hovered is no different
- **WHEN** the first card I hover after opening the home page is the row's first card, and I then move the pointer away
- **THEN** nothing is left under or around it

#### Scenario: Nothing else is needed to clear it
- **WHEN** I move the pointer off a card and neither scroll the row nor hover anything else
- **THEN** the treatment is already gone rather than clearing later

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

### Requirement: Rewatches appear in the currently-watching carousel

The main dashboard's "Currently watching" section SHALL include entries whose status is **Rewatching** alongside those whose status is Watching, since both are runs in progress and both are incremented from the same control.

A Rewatching card SHALL be rendered identically to a Watching one — same picture, title, progress row, and "+" control — and SHALL take its place in the section's existing ordering (episodes watched descending, then title) rather than being grouped separately or pinned. A Rewatching entry's anime has always finished airing, so no next-episode countdown applies to its card; its absence SHALL be the ordinary "no countdown known" case rather than a special one.

Incrementing a Rewatching card SHALL behave exactly as incrementing a Watching one, including the rule that finishing the run returns the entry to Completed and increases its rewatch count, per the `list-editing` capability. Once that happens the entry is no longer in progress, so it SHALL leave this section on the next read, the same way a completed first viewing does.

#### Scenario: A rewatch is on the dashboard

- **WHEN** an entry's status is Rewatching
- **THEN** it appears in the Currently watching section, rendered like any other card there

#### Scenario: Ordered together, not grouped apart

- **WHEN** the section holds both Watching and Rewatching entries
- **THEN** they are ordered together by episodes watched descending and then title, with no separation between the two statuses

#### Scenario: Incrementing a rewatch from the dashboard

- **WHEN** I press "+" on a Rewatching card below the last available episode
- **THEN** its episodes watched increases by one and its bar and count update in place, exactly as for a Watching card

#### Scenario: A finished rewatch leaves the section

- **WHEN** I press "+" on a Rewatching card and thereby reach everything available
- **THEN** the entry returns to Completed with its rewatch count increased, and is no longer in Currently watching on the next read

### Requirement: Broadcast progress on currently-watching cards
While an anime is currently airing, its currently-watching card's progress bar SHALL render broadcast progress — episodes aired out of the anime's total episode count — as a fill behind my watched fill, in the same blue used by the home page's airing-progress bar and by the anime detail page's bar. My watched progress SHALL keep the site's purple accent, layered on top within the same track.

This SHALL be an addition to the card's existing bar, not a second bar and not a replacement: the card SHALL keep exactly one progress row, its `watched/total` label, its in-place editable count, and its increment control, and the card's height SHALL be unchanged.

The aired fill SHALL be drawn only while MAL reports the anime as currently airing. For any other airing status the bar SHALL render with no aired fill — a finished run's aired count and total are the same figure, and an unaired show has broadcast nothing. The dashboard payload backing this section SHALL therefore carry, per currently-watching anime, whether MAL reports it as currently airing, alongside the aired-episode count it already carries.

When the total episode count is known, the aired fill SHALL span episodes aired out of that total. When the total is unknown and the aired count is known, the aired fill SHALL span exactly half the track as a fixed "progress so far, end unknown" marker, and my watched fill SHALL be measured within that extent against the aired count, so being caught up on everything aired covers the aired extent exactly and my fill can never exceed it. When the aired count is not known, no aired fill SHALL be drawn. Both fills SHALL be clamped so neither can exceed the track.

The bar's label SHALL remain `watched/total`, or `watched/?` when the total is unknown, and SHALL NOT restate the aired count.

When my episodes watched changes — by the increment control or by editing the count in place — my fill SHALL update in place without a reload, the aired fill SHALL be unaffected, and the row's scroll position SHALL be preserved as it is today.

#### Scenario: Airing anime shows broadcast progress
- **WHEN** a currently-watching card renders for a currently airing anime with 12 total episodes, 5 aired, and 3 watched
- **THEN** a blue fill spans 5/12 of the track, a purple fill spans 3/12 on top of it, and the label reads `3/12`

#### Scenario: Caught up with the broadcast
- **WHEN** a currently-watching card renders for a currently airing anime where I have watched every episode that has aired
- **THEN** the purple fill covers the full extent of the blue fill and neither extends past the aired portion of the track

#### Scenario: Finished airing keeps the plain bar
- **WHEN** a currently-watching card renders for an anime that has finished airing
- **THEN** no aired fill is drawn and the bar shows only my watched progress against the total, as it did before

#### Scenario: Not yet aired keeps the plain bar
- **WHEN** a currently-watching card renders for an anime that has not yet aired
- **THEN** no aired fill is drawn

#### Scenario: Airing with an unknown total
- **WHEN** a currently-watching card renders for a currently airing anime with an unpublished total episode count and 10 episodes aired, of which I have watched 5
- **THEN** the blue fill spans half the track, the purple fill covers half of that blue extent, and the label reads `5/?`

#### Scenario: Airing with no aired count
- **WHEN** a currently-watching card renders for a currently airing anime with no determinable aired-episode count
- **THEN** no blue fill is drawn, and my watched fill and label render as they did before

#### Scenario: Incrementing updates my fill only
- **WHEN** I click the plus control on a currently-watching card for a currently airing anime
- **THEN** the purple fill grows in place without a reload and the blue aired fill is unchanged

#### Scenario: The card does not get taller
- **WHEN** a currently-watching card renders with the aired fill drawn
- **THEN** it has the same single progress row and the same height as a card without one, with no additional bar or aired-count label added

### Requirement: Next-episode countdown on currently-watching cards
The system SHALL show, on each currently-watching card, a countdown to the next episode in the form "Next ep: in X days, Y h", computed from the earliest stored per-episode air instant still in the future, converted to local time.

The countdown SHALL NOT be projected from the anime's broadcast day and time when no future stored episode row exists.

#### Scenario: Showing the countdown
- **WHEN** a currently-watching card is rendered for an anime with a stored episode row whose air instant is in the future
- **THEN** it shows the time remaining until that instant as "Next ep: in X days, Y h"

#### Scenario: No known next episode
- **WHEN** a currently-watching anime has no stored episode row with a future air instant
- **THEN** the card omits the next-episode countdown rather than showing a stale or projected value

#### Scenario: Countdown skips a break
- **WHEN** a currently-watching anime is on a one-week break and its next stored episode is two weeks out
- **THEN** the countdown counts to that instant rather than to the next weekly broadcast slot

### Requirement: Airing today filtered to my list in local time
The system SHALL show an "Airing today" section as a list column (not a grid) containing only anime in my list that have a stored per-episode airing row whose air instant converts to today's local date, where each row links to the anime's detail page.

Each row SHALL be laid out as a thumbnail image beside two stacked lines of text. The first line SHALL read `time : Ep N`, where the time and the episode number are those of the stored airing row for that local date. The second line SHALL be the anime's display title.

The row thumbnail SHALL be a poster at the same 2:3 aspect ratio used by anime cards elsewhere in the app, and SHALL be large enough to read as the row's own picture rather than an inline icon — clearly taller than the two lines of text beside it, so the poster, not the text, sets the row's height.

The two text lines SHALL be aligned to the top of the row rather than centred against the thumbnail: the `time : Ep N` line SHALL begin at the top of the thumbnail, with the title starting directly beneath it and the remaining space falling below the text.

The title SHALL be clamped to at most two lines, with an ellipsis (`…`) marking a title cut short, so no single row can grow unbounded in height. The ellipsis SHALL appear only when the title genuinely overflows at the section's rendered width — a title that fits SHALL be shown in full, with no ellipsis and no truncation at a fixed character count.

The section SHALL NOT list an anime for which no stored airing row falls on today's local date, even when its cached broadcast day matches today. No row's episode number SHALL be projected from a broadcast cadence.

The dashboard payload backing this section SHALL carry the episode number of the stored row per anime.

#### Scenario: Local-day airing filter
- **WHEN** the main page loads
- **THEN** "Airing today" lists only my-list anime with a stored episode air instant converting to today's local date

#### Scenario: Row layout
- **WHEN** an "Airing today" row renders for an anime whose stored episode 4 airs at 19:30 local time
- **THEN** the row shows its poster thumbnail beside `19:30 : Ep 4` with the anime's title on the line below

#### Scenario: Poster anchors the row
- **WHEN** an "Airing today" row renders
- **THEN** its thumbnail is a 2:3 poster taller than the two text lines beside it, and the poster's height sets the row's height

#### Scenario: Text starts at the top of the row
- **WHEN** an "Airing today" row renders with a one-line title
- **THEN** the `time : Ep N` line sits at the top of the row level with the top of the poster, the title sits directly below it, and the leftover space is below the title rather than split above and below the text

#### Scenario: Long title clamped
- **WHEN** an "Airing today" row's title is too long to fit on two lines at the section's width
- **THEN** the title is cut off at the end of the second line with an ellipsis rather than wrapping further

#### Scenario: Short title shown in full
- **WHEN** an "Airing today" row's title fits within two lines at the section's width
- **THEN** it is shown in full with no ellipsis

#### Scenario: Anime on break today
- **WHEN** a my-list anime's cached broadcast day is today but it has no stored episode airing today because it is on a break
- **THEN** it does not appear in "Airing today"

#### Scenario: Nothing airing today
- **WHEN** no my-list anime have a stored episode airing today in local time
- **THEN** the section shows a small message indicating nothing is airing today

### Requirement: Current season section with filters and progress
The system SHALL show a "Current season" section on the home page containing anime in my list that belong to the current viewing season, filterable by popularity, MAL score, alphabetical, and my score. When sorting by popularity, anime that are unranked — MAL popularity rank absent or zero — SHALL be ordered after all ranked anime rather than treated as most popular.

An anime SHALL be included when it is in my list and either its MAL airing status is `currently_airing`, or it has already premiered and its start date falls in the current season quarter. "Has already premiered" SHALL mean its start date is on or before today in local terms. An anime with no recorded start date SHALL be included only while MAL reports it as currently airing. A current-season anime that has finished airing — a movie, a short, or a completed TV run — SHALL therefore remain in the section for the rest of that season. A current-season anime that has not premiered yet SHALL be excluded until its start date passes.

Season membership SHALL be derived from the anime's own start date rather than from a cached season listing, so that the section's contents do not depend on which season pages have been browsed.

Each card in this section SHALL show an **airing progress bar** rather than a watched/total progress bar. The bar SHALL render broadcast progress — episodes aired out of the anime's total episode count — as the primary fill, in a blue colour distinct from the site's purple accent, and SHALL label it `aired/total`. When the total episode count is unknown the label SHALL show `aired/?`; when the aired count cannot be determined the label SHALL show `?/total` and the primary fill SHALL be empty rather than showing a fabricated value.

When the total episode count is unknown, the blue fill SHALL be determined by whether the run is over. When MAL reports the anime as finished airing, the blue fill SHALL span the full track — the show has broadcast everything it is going to, even though no episode count is published. Otherwise, when the aired count is known, the blue fill SHALL span exactly half the track regardless of how many episodes have aired — a fixed "progress so far, end unknown" marker rather than a proportion of a total that does not exist. When the run is not finished and neither the total nor the aired count is known, the blue fill SHALL be empty.

The dashboard payload backing this section SHALL carry, per anime, whether MAL reports it as finished airing.

When I have watching progress on that anime — episodes watched greater than zero — the bar SHALL additionally render my watched progress as a fill in the site's purple accent colour, layered on top of the aired fill within the same track, measured against the same total episode count, so that the accent extent reads as how far I have watched and the blue extent reads as how far the show has broadcast. Both fills SHALL be clamped so neither can exceed the width of the track. When episodes watched is zero, no accent fill SHALL be rendered. When the total episode count is unknown, the purple fill SHALL be measured within the blue extent against the aired count instead, so that being caught up on every aired episode covers the whole blue extent and the purple fill never exceeds it; when the aired count is also unknown, my watched count SHALL serve as its own measure so a watched title still reads as covered.

When an episode is incremented elsewhere on the home page for an anime that also appears in this section, that anime's purple fill here SHALL update to match without a page reload.

This bar — the `aired/total`-labelled bar specified here, with no editable count — SHALL apply only to the home page's followed-shows-airing section. Broadcast progress itself is not exclusive to this section: the anime detail page draws its own aired fill behind my watched fill while an anime is currently airing, per the anime-detail capability; the currently-watching carousel does the same on its own editable watched/total bar, per the "Broadcast progress on currently-watching cards" requirement; and the series page does the same for its own progress figures. The watched/total progress bar SHALL remain unchanged everywhere else it is used, including My List.

#### Scenario: Filtering current season
- **WHEN** I choose a sort/filter (popularity, MAL score, alphabetical, or my score)
- **THEN** the current-season cards reorder accordingly

#### Scenario: Unranked anime sort last by popularity
- **WHEN** I sort the current-season section by popularity and some anime have no popularity rank (rank absent or zero)
- **THEN** those unranked anime appear last rather than at the top

#### Scenario: Finished current-season title stays listed
- **WHEN** a my-list movie or short whose start date falls in the current season quarter has already premiered and finished airing
- **THEN** it still appears in the section, with its aired bar full

#### Scenario: Completed TV run stays listed
- **WHEN** a my-list TV anime whose start date falls in the current season quarter finishes its run mid-season and its MAL status flips to finished airing
- **THEN** it remains in the section for the rest of the season rather than disappearing

#### Scenario: Unpremiered current-season title excluded
- **WHEN** a my-list anime's start date falls in the current season quarter but is still in the future
- **THEN** it does not appear in the section

#### Scenario: Anime from an earlier season excluded
- **WHEN** a my-list anime finished airing in a previous season
- **THEN** it does not appear in the section

#### Scenario: Membership does not depend on browsing history
- **WHEN** the section renders and the current season's browse page has never been opened
- **THEN** premiered current-season titles are listed just the same, because membership comes from each anime's own start date

#### Scenario: Bar shows broadcast progress
- **WHEN** a followed-shows-airing card renders for an anime where 5 of 12 episodes have aired
- **THEN** the blue aired fill spans 5/12 of the track and the label reads `5/12`

#### Scenario: Watched progress layered in the accent colour
- **WHEN** a followed-shows-airing card renders for an anime where 5 of 12 episodes have aired and I have watched 3
- **THEN** a purple fill spanning 3/12 of the track is drawn on top of the blue fill, leaving the blue visible from 3/12 to 5/12

#### Scenario: No watching progress
- **WHEN** a followed-shows-airing card renders for an anime I have not started (episodes watched is zero)
- **THEN** only the blue aired fill is drawn and no purple fill appears

#### Scenario: Caught up with the broadcast
- **WHEN** a followed-shows-airing card renders for an anime where I have watched every episode that has aired
- **THEN** the purple fill covers the full extent of the blue fill and neither fill extends past the aired portion of the track

#### Scenario: Progress bar with unknown total
- **WHEN** a followed-shows-airing card's anime is still airing, has an unknown total episode count, and 3 episodes have aired
- **THEN** the blue fill spans half the track and the label reads `3/?`

#### Scenario: Unknown total, many episodes aired
- **WHEN** a followed-shows-airing card's anime is still airing, has an unknown total episode count, and 40 episodes have aired
- **THEN** the blue fill still spans exactly half the track rather than more

#### Scenario: Unknown total with watching progress
- **WHEN** a followed-shows-airing card's anime is still airing, has an unknown total, 10 episodes have aired, and I have watched 5
- **THEN** the purple fill covers half of the blue half-track extent, and watching all 10 would cover the whole blue extent

#### Scenario: Unknown total, watched a quarter of what aired
- **WHEN** a followed-shows-airing card's anime is still airing, has an unknown total, 8 episodes have aired, and I have watched 2
- **THEN** the purple fill covers a quarter of the blue half-track extent — the accent is proportional to episodes watched out of episodes aired, not a fixed fraction of the blue fill

#### Scenario: Unknown total, watched ahead of the aired estimate
- **WHEN** a followed-shows-airing card's anime is still airing, has an unknown total, the aired count is estimated at 4, and I have watched 5
- **THEN** the purple fill covers the whole blue half-track extent and does not spill past it

#### Scenario: Finished run with no published episode count
- **WHEN** a followed-shows-airing card's anime is reported by MAL as finished airing and has no published total episode count
- **THEN** the blue fill spans the full track rather than half or none

#### Scenario: Finished movie I have watched
- **WHEN** a followed-shows-airing card's anime is a finished movie with neither a published total nor a determinable aired count, and I have watched it
- **THEN** the blue fill spans the full track, the purple fill covers it entirely, and the label reads `?/?`

#### Scenario: Unknown aired count
- **WHEN** a followed-shows-airing card's anime has a known total but no determinable aired-episode count
- **THEN** its label shows `?/total`, the blue fill is empty, and any purple watched fill is still drawn

#### Scenario: Neither count known and still airing
- **WHEN** a followed-shows-airing card's anime is still airing and has neither a total episode count nor a determinable aired count
- **THEN** its label shows `?/?` and the blue fill is empty rather than half-filled

#### Scenario: Increment elsewhere on the page updates this bar
- **WHEN** I increment an episode from the currently-watching carousel for an anime that also appears in the followed-shows-airing section
- **THEN** that anime's purple fill in the followed-shows-airing section grows to match, without a page reload

#### Scenario: My List keeps the watched progress bar
- **WHEN** I view My List
- **THEN** the episode bar there still shows watched/total with no aired fill

#### Scenario: The detail page draws its own aired fill
- **WHEN** I open the detail page of a currently airing anime
- **THEN** its progress bar shows an aired fill behind my watched fill, per the anime-detail capability, rather than this section's `aired/total`-labelled bar

### Requirement: Followed shows airing sort is restored with the page
The "Followed shows airing" section's sort selection SHALL be captured as part of the home page's restorable view state, so returning to the home page by back/forward navigation restores the sort I had selected rather than resetting it to its default. This SHALL follow the `page-state-restoration` capability's rules for view controls exactly: it SHALL be restored on a back/forward navigation, and SHALL open on its documented default (Popularity) on a fresh visit.

#### Scenario: The sort survives opening an anime and coming back
- **WHEN** I set the "Followed shows airing" sort to MAL score, open one of its cards, and then go back
- **THEN** the sort is still MAL score and the cards are still ordered by it

#### Scenario: A fresh visit opens on the default sort
- **WHEN** I set the sort to Alphabetical, navigate away, and then reach the home page by clicking its navbar link
- **THEN** the sort is back on Popularity

### Requirement: Aired-episode count for followed airing shows
The dashboard data for the followed-shows-airing section SHALL include, per anime, the number of episodes that have aired as of the current instant. The count SHALL be the highest episode number among that anime's stored per-episode airing rows whose air instant has passed. An episode SHALL be counted as aired only once its stored air instant has passed.

The count SHALL NOT be estimated from a weekly broadcast cadence, from elapsed time since the anime's start date, or from any other projection. When the anime has no stored airing rows, the count SHALL be reported as unknown rather than guessed or reported as zero.

The count SHALL NOT be clamped to the anime's total episode count from MyAnimeList, and SHALL NOT be substituted with that total for an anime that has finished airing. A finished anime's stored rows already cover its whole run.

#### Scenario: Aired count from stored per-episode rows
- **WHEN** the aired count is computed for an anime whose stored rows place episodes 1 through 4 in the past and episode 5 in the future
- **THEN** the reported aired count is 4

#### Scenario: Today's episode has not aired yet
- **WHEN** the aired count is computed on a day this anime broadcasts, before the stored air instant of that day's episode has passed
- **THEN** today's episode is not counted as aired

#### Scenario: Finished show counts every stored episode
- **WHEN** the aired count is computed for an anime that has finished airing and has stored rows for all of its episodes
- **THEN** the reported aired count equals the highest stored episode number

#### Scenario: Show on hiatus does not accrue episodes
- **WHEN** the aired count is computed for an anime that stopped broadcasting ten weeks ago and has no stored episode rows since
- **THEN** the count is the highest episode number that actually aired, and does not grow by a further ten

#### Scenario: Count exceeds the MyAnimeList total
- **WHEN** the aired count is computed for an anime with a stored, already-aired row for episode 13 whose cached MyAnimeList total is 12
- **THEN** the reported aired count is 13 rather than 12

#### Scenario: Aired count is unknown
- **WHEN** the aired count is computed for an anime with no stored airing rows
- **THEN** the aired count is reported as unknown rather than as zero or an estimate

#### Scenario: Unknown count is rendered as unknown
- **WHEN** a followed-shows-airing card renders for an anime whose aired count is unknown
- **THEN** the card shows an explicit unknown marker in place of the count rather than the number zero

### Requirement: Dashboard section title dividers
The system SHALL render a thin horizontal divider rule directly beneath the title of each dashboard section on the main page — "Currently watching", "Airing today", and "Followed shows airing" — visually separating the section heading from its content. The divider SHALL span the width of the section's content area.

#### Scenario: Divider under each section title
- **WHEN** the main page renders the "Currently watching", "Airing today", and "Followed shows airing" sections
- **THEN** each section's title is underlined by a thin horizontal divider rule separating the heading from the section content

#### Scenario: Divider spans the section width
- **WHEN** a dashboard section title divider is rendered
- **THEN** the divider spans the width of that section's content area

