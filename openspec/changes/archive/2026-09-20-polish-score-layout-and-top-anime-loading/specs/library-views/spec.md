## MODIFIED Requirements

### Requirement: My list grouped and ordered by status
The system SHALL present my list as one list grouped and ordered as Currently watching → Rewatching → On hold → Plan to watch → Completed → Dropped, where each entry shows picture, title, type (TV/movie), progress, MAL score (respecting the hide/unhide toggle), my score, and an edit button. The MAL score column SHALL sit ahead of the my-score control, per the `score-presentation` capability's ordering of a MAL/mine score pair; both columns keep the width and the centred alignment they have today, so the list's columns stay aligned with themselves. Rewatching sits directly after Currently watching because both are runs in progress. For entries in the **Plan to watch** group, each row SHALL additionally show an airing-status indicator alongside the type — **Not aired**, **Airing**, or **Aired** (mapped from the anime's `not_yet_aired`, `currently_airing`, and `finished_airing` values) — so the user can tell at a glance whether a queued show is already out, still airing, or has not yet started; when the airing status is unknown, no indicator is shown.

Rows outside Plan to watch SHALL also show the airing-status indicator while the user is working with airing status — that is, while the airing-status filter has a selection — since the indicator is the value being filtered on. Outside that case, rows in other status groups SHALL NOT show the indicator.

#### Scenario: Rendering the grouped list
- **WHEN** the my-list page loads
- **THEN** entries appear grouped in the order Currently watching, Rewatching, On hold, Plan to watch, Completed, Dropped, each showing picture, title, type, progress, MAL score, my score, and an edit button

#### Scenario: The MAL score leads the pair
- **WHEN** I look at any my-list row showing both scores
- **THEN** the MAL score appears before my own score's control, and the Edit button still follows both

#### Scenario: Rewatching sits with the in-progress groups
- **WHEN** my list holds both Rewatching and Completed entries
- **THEN** the Rewatching group appears directly after Currently watching, not beside Completed

#### Scenario: Plan-to-watch row shows airing status
- **WHEN** the Plan to watch group renders an entry whose anime has a known airing status
- **THEN** that row shows an airing-status indicator (Not aired, Airing, or Aired) next to the type

#### Scenario: Airing status shown while filtering by it
- **WHEN** the airing-status filter has a selection
- **THEN** every row with a known airing status shows the indicator, whatever its watch status

#### Scenario: Airing status otherwise only on Plan to watch
- **WHEN** no airing-status filter is selected
- **THEN** rows outside Plan to watch show the type without an airing-status indicator

#### Scenario: Unknown airing status shows no indicator
- **WHEN** an entry's anime has no known airing status
- **THEN** its row shows the type with no airing-status indicator

### Requirement: Top anime ranked list
The system SHALL present a Top anime page as a ranked list covering up to rank 500, where every entry — whatever form it takes — shows its rank, picture, title, MAL score, and my score, in that order, per the `score-presentation` capability's ordering of a MAL/mine score pair. The page shows one selected ranking list at a time (see "Top anime ranking list selector"); everything below applies to whichever list is selected. Because this page ranks anime overall, an entry's anime may not be in my list; the flat-row tier (rank 11 and beyond) SHALL additionally carry a list-action button, conditional — "Add" when the anime is not yet in my list, and "Edit" when it is — the showcase and top-ten tiers (ranks 1–10) SHALL NOT carry one, keeping those tiers focused on the ranking and scores alone. The list SHALL be paginated at 50 entries per page, with page-number controls plus left/right arrows at the bottom, and left/right arrow controls at the top-right.

The page-number controls SHALL show the first page, the last page, and the current page with at most one page on each side of it, collapsing any gap between those groups into an ellipsis. On page 6 of 10 this yields `1 … 5 6 7 … 10`.

Changing the page from the bottom controls (page-number buttons or arrows) SHALL scroll the page back to the top, so the newly-shown entries are visible without the reader having to scroll up manually. The top-right arrows need no such scroll, since they already sit at the top of the page.

Changing the page, by any control, SHALL add a step to the browser's own navigation history rather than only updating local view state. Going back (the browser's back button, a keyboard shortcut, or a trackpad swipe gesture) SHALL therefore step to the previously-viewed page of the ranking, and going forward again SHALL return to the page just left — the same way back/forward already move between any other two pages in the app. Only once that page-by-page history is exhausted SHALL going back leave the Top anime page entirely, for whichever page preceded it in the browsing session.

The ranking SHALL be presented in three tiers, each visually distinct from the next, so the shape of the page itself communicates where an anime sits in the ranking:

- **Ranks 1–3 — showcase cards.** Three cards, each a self-contained bordered surface carrying its rank, poster, title, and both scores, the MAL chip leading and my score's chip following it. They SHALL be laid out in ascending rank order following the reading direction, so the first card is rank 1; the presentation SHALL NOT reorder them visually away from their document order. Each card SHALL carry a prominent rank badge in a gold, silver, and bronze family for ranks 1, 2, and 3 respectively, and the card SHALL pick up its own medal colour beyond the badge (for example in its border and surface tint) so the three are distinguishable from one another at a glance and from every other tier. Rank 1 SHALL read as the most prominent of the three. The medal colours SHALL render the same in light and dark mode, since a medal's colour is its meaning. The card's two score chips SHALL be the same size as each other regardless of their label or value text differing in length, so the pair reads as one deliberate row rather than two mismatched boxes.
- **Ranks 4–10 — top-ten card row.** A single row of poster cards, smaller than the showcase cards, each carrying its own rank badge, title, and both scores, inside a bordered, coloured box of its own so the tier reads as a defined group rather than loose posters. The two scores SHALL sit at opposite edges of the card as they do today, with the MAL score now at the leading edge and my score at the trailing one. The badge SHALL remain fully legible over any poster artwork, bright or dark, rather than relying on a translucent overlay whose contrast depends on the art beneath it.
- **Ranks 11 and beyond — flat rows.** The existing full-width rows, each with plain `#N` rank text, poster, title, right-aligned MAL score, my score, and its action button. The MAL column SHALL keep the end-of-cell alignment it has today, so the column of scores stays aligned with itself in its new position.

The showcase tier's medal identity SHALL be drawn from the application-wide medal colours — the same gold, silver, and bronze the recap podium uses — rather than from a palette local to this page, so the two surfaces that rank a top three in the app cannot drift apart in colour. Its rank badge SHALL take the same form as the recap podium's: a medal-coloured outline around a neutral fill, carrying the rank as a bare number. The medal treatment carried over SHALL be colour and badge form only; the podium's own sizing, entrance animation, hover response, and rank-1 sheen SHALL NOT follow it onto this page.

A showcase card's score chips SHALL be compact — sized for the narrow column beside the poster rather than to the app's default chip width — and within them the role label ("MAL score", "My score") SHALL be large enough to read as a label rather than as fine print, at a size closer to its own value's than today's, while staying subordinate to that value.

Both card tiers (ranks 1–3 and 4–10) SHALL render an entry's poster in the same proportions the anime pages give that poster, so the artwork shown here is the artwork the anime's own detail page shows, not a differently-cropped portion of it. Neither tier SHALL crop a poster to proportions narrower than that box in order to fit its layout. The flat-row tier's small thumbnail is unaffected by this rule.

The rank of any entry SHALL be readable from the entry itself, in every tier, without relying on its position in the layout.

Because a page holds 50 entries, the showcase and top-ten tiers SHALL appear only on page 1; every entry on page 2 and beyond SHALL use the flat row form.

#### Scenario: Rendering the top-anime ranking
- **WHEN** the top-anime page loads
- **THEN** it shows the first page of 50 entries of the selected list, each with its rank, picture, title, MAL score, and my score, plus a list-action button for entries in the flat-row tier

#### Scenario: Every tier puts MAL first
- **WHEN** I look at a showcase card, a ranks-4-to-10 card, and a flat row on the same page
- **THEN** all three show MAL's score ahead of my own

#### Scenario: Paginating the ranking
- **WHEN** I use the page-number controls or the left/right arrows (at the bottom or top-right)
- **THEN** the list shows the corresponding 50-entry page, up to rank 500

#### Scenario: Changing pages from the bottom scrolls back to the top
- **WHEN** I use the bottom page-number controls or arrows to change the page, from anywhere on the page
- **THEN** the page scrolls back to the top so the newly-shown entries — the showcase tier on page 1, or the first flat rows on later pages — are visible immediately

#### Scenario: Going back steps to the previous page of the ranking
- **WHEN** I change from page 1 to page 2, then to page 3, and then go back — via the browser's back button, a keyboard shortcut, or a trackpad swipe gesture
- **THEN** I return to page 2 of the ranking, not to whatever page I was on before I opened Top anime

#### Scenario: Going forward returns to the page just left
- **WHEN** I go back from page 3 to page 2 as above, then go forward
- **THEN** I return to page 3

#### Scenario: Going back past the first page leaves Top anime
- **WHEN** I am on page 1 of the ranking, having reached it without changing pages or lists since arriving, and go back
- **THEN** I leave the Top anime page for whatever page I was on immediately before it

#### Scenario: Page numbers in the middle of the range
- **WHEN** I am on page 6 of 10
- **THEN** the page-number controls show 1, an ellipsis, 5, 6, 7, an ellipsis, and 10 — and no other page numbers

#### Scenario: Page numbers near an edge of the range
- **WHEN** I am on page 2 of 10
- **THEN** the page-number controls show 1, 2, 3, an ellipsis, and 10, with no ellipsis between adjacent pages

#### Scenario: Flat-row entry not in my list
- **WHEN** a flat-row (rank 11+) top-anime entry's anime is not in my list
- **THEN** its button reads "Add"; using it adds the anime with status Plan to watch and the button changes in place to "Edit"

#### Scenario: Flat-row entry already in my list
- **WHEN** a flat-row (rank 11+) top-anime entry's anime is already in my list
- **THEN** its button reads "Edit" and opens the editor overlay in place when used

#### Scenario: Top 3 stand out from the rest of the ranking
- **WHEN** the top-anime page's first page renders
- **THEN** ranks 1, 2, and 3 render as three showcase cards, each carrying a gold, silver, or bronze rank badge and matching card accent, visibly different from both the top-ten card row and the flat rows below

#### Scenario: The top 3 read in rank order
- **WHEN** I look at the three showcase cards
- **THEN** they run 1, 2, 3 in reading order, and each card states its own rank

#### Scenario: The showcase and the recap podium agree on gold
- **WHEN** I compare a Top anime showcase card with the recap page's podium card of the same rank
- **THEN** both carry the same medal colour and the same badge form, in light mode and in dark mode alike

#### Scenario: The podium's motion does not follow its colours
- **WHEN** the top-anime page's first page renders
- **THEN** the showcase cards appear without an entrance animation, rank 1 carries no sweeping sheen, and hovering a card does not lift it

#### Scenario: A showcase card's two score boxes match
- **WHEN** I look at a showcase card's "MAL score" and "My score" chips
- **THEN** the two boxes are the same width and height as each other, even though "My score" and "MAL score" differ in text length

#### Scenario: A showcase chip's label is readable
- **WHEN** I look at a showcase card's score chips
- **THEN** each chip's label is legible beside its value rather than reading as fine print, and the chip itself takes no more room beside the poster than its two short lines need

#### Scenario: A top-ten poster matches the anime's own page
- **WHEN** I open the detail page of an anime shown in the showcase or top-ten tier
- **THEN** its poster there is the same picture, showing the same extent of the artwork, as the Top anime card showed — neither is a narrower crop of the other

### Requirement: Daily refresh of the Top Anime ranking
The system SHALL serve a Top Anime ranking list from its cache without waiting on MyAnimeList, and SHALL refresh that list from MAL separately from the read that serves it. Reading a list SHALL therefore never block on a live fetch: whatever is cached for that list is answered immediately, however stale, and the refresh runs alongside.

The system SHALL re-fetch a list's lean listing fields the first time that list is viewed on a local calendar day after its own last fetch, serving it from cache without a re-fetch on same-day revisits. Each selectable list SHALL keep its own last-fetched time, so viewing one list never marks another as fetched for the day. If a list has never been viewed, it SHALL never be proactively fetched — opening the page SHALL NOT warm the lists the user has not selected.

When a visit's refresh fetches new data, the page SHALL take it up in place: the rows the reader is looking at SHALL be replaced by the refreshed ones without a loading state, without leaving the page, and without losing any list membership the reader has just changed from that page. A refresh that fetched nothing new SHALL leave the page exactly as it is.

A list the client has already loaded during the current application session SHALL continue to be served from what the client already holds, with no request of any kind, per "Fast switching between ranking lists" — so a list is refreshed at most once per session however often it is re-selected, and the daily cadence above bounds it further.

A list with nothing cached at all — one selected for the first time ever — has nothing to serve immediately and SHALL behave as it does today: the previously shown list stays on screen, muted, until the new one arrives, or a plain loading state is shown if no list is on screen yet.

At most one refresh per list SHALL be in flight at a time: a second request for the same list arriving while its refresh is running SHALL wait for it rather than starting a second fetch of that list. Requests for two different lists SHALL NOT wait on each other. A fetch that fails SHALL NOT count as that list's fetch for the day — the next visit to that list retries — and SHALL NOT disturb what the page is already showing for it.

#### Scenario: A new-day visit shows the cache first
- **WHEN** I open a Top Anime ranking list that has not been fetched yet on the current local calendar day and has rows cached from an earlier day
- **THEN** yesterday's cached rows appear immediately, without waiting on MyAnimeList

#### Scenario: The refresh lands while I am reading
- **WHEN** that visit's background refresh finishes with new data
- **THEN** the rows on screen are replaced by the refreshed ones in place, with no loading state and no navigation

#### Scenario: A refresh that changes nothing leaves the page alone
- **WHEN** a visit's refresh is skipped because the list was already fetched today, or it fails
- **THEN** the page keeps showing exactly the rows it was showing

#### Scenario: Same-day revisit
- **WHEN** I reopen the same ranking list again on the same local day
- **THEN** it is served from the cache without a live re-fetch

#### Scenario: Each list has its own daily clock
- **WHEN** I have already viewed the All list today and then select the Movie list for the first time today
- **THEN** the Movie list is fetched live, because the All list's fetch does not count as Movie's

#### Scenario: A list already loaded this session is not refreshed again
- **WHEN** I select Movie, then All, then Movie again within one session
- **THEN** the second Movie selection makes no request at all — neither a read nor a refresh

#### Scenario: A never-cached list still waits
- **WHEN** I select a ranking list that has never been fetched, so nothing is cached for it
- **THEN** the previously shown list stays on screen, muted, until the new list arrives — the behaviour that list already has today

#### Scenario: Concurrent visits share one refresh
- **WHEN** a second request for the same ranking list arrives while its refresh is already running
- **THEN** no additional MAL fetch is started, and the second request waits for the first rather than racing it

#### Scenario: Different lists refresh in parallel
- **WHEN** requests for two different ranking lists arrive at the same time, neither fetched yet today
- **THEN** neither waits on the other, because they are different subjects

#### Scenario: A failed fetch does not consume the day
- **WHEN** a ranking list's refresh fails and I open that list again the same day
- **THEN** the refresh is retried, because only a successful fetch marks that list as fetched for that day

#### Scenario: A failed fetch still serves the cache
- **WHEN** a ranking list's refresh fails and that list has cached rows from an earlier fetch
- **THEN** the page renders those cached rows rather than an error or an empty list

#### Scenario: Never visited stays unfetched
- **WHEN** a Top Anime ranking list has never been viewed
- **THEN** no background job fetches it, and viewing any other list does not fetch it either

#### Scenario: An edit survives a refresh landing
- **WHEN** I add an anime to my list from a ranking row and that list's background refresh lands immediately afterwards
- **THEN** that row still reads "Edit", not "Add", because the refresh does not undo the membership change I just made
