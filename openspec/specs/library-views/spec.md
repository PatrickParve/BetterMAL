# library-views Specification

## Purpose
TBD - created by archiving change bootstrap-anime-tracker. Update Purpose after archive.
## Requirements
### Requirement: My list grouped and ordered by status
The system SHALL present my list as one list grouped and ordered as Currently watching → On hold → Plan to watch → Completed → Dropped, where each entry shows picture, title, type (TV/movie), progress, my score, MAL score (respecting the hide/unhide toggle), and an edit button. For entries in the **Plan to watch** group, each row SHALL additionally show an airing-status indicator alongside the type — **Not aired**, **Airing**, or **Aired** (mapped from the anime's `not_yet_aired`, `currently_airing`, and `finished_airing` values) — so the user can tell at a glance whether a queued show is already out, still airing, or has not yet started; when the airing status is unknown, no indicator is shown.

Rows outside Plan to watch SHALL also show the airing-status indicator while the user is working with airing status — that is, while the airing-status filter has a selection or Airing status is the primary sort key — since the indicator is the value being filtered or ordered on. Outside those cases, rows in other status groups SHALL NOT show the indicator.

#### Scenario: Rendering the grouped list
- **WHEN** the my-list page loads
- **THEN** entries appear grouped in the order Currently watching, On hold, Plan to watch, Completed, Dropped, each showing picture, title, type, progress, my score, MAL score, and an edit button

#### Scenario: Plan-to-watch row shows airing status
- **WHEN** the Plan to watch group renders an entry whose anime has a known airing status
- **THEN** that row shows an airing-status indicator (Not aired, Airing, or Aired) next to the type

#### Scenario: Airing status shown while filtering or sorting by it
- **WHEN** the airing-status filter has a selection, or Airing status is the primary sort key
- **THEN** every row with a known airing status shows the indicator, whatever its watch status

#### Scenario: Airing status otherwise only on Plan to watch
- **WHEN** no airing-status filter is selected and the sort is not by airing status
- **THEN** rows outside Plan to watch show the type without an airing-status indicator

#### Scenario: Unknown airing status shows no indicator
- **WHEN** an entry's anime has no known airing status
- **THEN** its row shows the type with no airing-status indicator

### Requirement: List-row posters fill the row
The system SHALL render the poster in a my-list row and a top-anime row flush with the row's top and bottom edges, filling the row's full height with no padding above or below it, so the poster reads as part of the card rather than an image floating inside it. On a top-anime row, or a my-list row preceded by a rank column, the poster SHALL also sit flush with its leading neighbour (the rank column, or the row's leading edge when there is no rank), still filling the row's height. On a my-list row with no rank shown, the poster SHALL instead sit a small fixed gap after the row's status-colour stripe, rather than flush against it, so the stripe and poster read as two distinct elements.

Rows SHALL keep the height they have without the change: the row does not grow to the poster's natural aspect ratio, so adopting this layout does not lengthen the page. The poster's width MAY grow along with its height to avoid cropping the image more tightly than before. A row whose anime has no picture SHALL render its placeholder at the same full-height size, so rows with and without a picture stay aligned.

#### Scenario: Poster fills a my-list row
- **WHEN** the my-list page renders a row in grouped (unranked) view
- **THEN** that row's poster touches the row's top and bottom edges, with a small fixed gap between the status-colour stripe and the poster's leading edge

#### Scenario: Poster fills a ranked row
- **WHEN** a my-list or top-anime row shows a rank number before its poster
- **THEN** the poster still touches the row's top and bottom edges, sitting after the rank column rather than at the leading edge

#### Scenario: Row heights are unchanged
- **WHEN** rows adopt the full-height poster
- **THEN** each row occupies the same height as before, and the list is no longer than it was

#### Scenario: Missing picture keeps the row aligned
- **WHEN** a row's anime has no poster picture
- **THEN** its placeholder occupies the same full-height area, keeping the row's content aligned with its neighbours

### Requirement: Rank numbers when sorted by score
The system SHALL show rank numbers (e.g. `#1`) on the left of my-list entries when the list is flat — that is, when grouping by status is off — and the primary sort key is anything other than Alphabetical, since a rank against an alphabetical ordering carries no meaning. A grouped list SHALL NOT show rank numbers. In flat mode the system SHALL show a single header line naming the active status filter (or "All") above the list.

The rank SHALL occupy a fixed-width column sized for the longest rank the list can produce (at least three digits), independent of the rank actually shown on a given row. The `#` SHALL start at the same horizontal position on every row, and the poster that follows SHALL start at the same horizontal position on every row, so a rank of any length neither shifts the posters out of alignment nor runs underneath one.

#### Scenario: Ranks on a flat sorted list
- **WHEN** grouping is off and I sort by MAL score, my score, episodes watched, or any key other than Alphabetical
- **THEN** each entry shows a rank number on the left

#### Scenario: Sorting by airing status
- **WHEN** grouping is off and I sort by airing status
- **THEN** each entry shows a rank number on the left, ordered by the chosen show-first status ahead of the other two

#### Scenario: No ranks when grouped
- **WHEN** grouping by status is on
- **THEN** no rank numbers are shown, whatever the sort key

#### Scenario: No ranks on an alphabetical flat list
- **WHEN** grouping is off and the primary sort key is Alphabetical
- **THEN** no rank numbers are shown

#### Scenario: Flat view shows an active-filter header
- **WHEN** the my-list page is in flat (ungrouped) mode
- **THEN** a header line above the list names the active status filter, or "All" when unfiltered

#### Scenario: Three-digit rank stays clear of the poster
- **WHEN** a ranked row's number reaches three digits (for example `#100`)
- **THEN** the whole number is visible beside the poster rather than overlapping or sliding under it

#### Scenario: Ranks and posters align down the list
- **WHEN** a ranked list contains rows with one-, two-, and three-digit ranks
- **THEN** every row's `#` starts at the same horizontal position and every row's poster starts at the same horizontal position

### Requirement: My list status filter tabs
The system SHALL provide status filter controls on the my-list page — All, Watching, Completed, Plan to watch, On hold, Dropped — so that selecting one shows only entries in that status.

#### Scenario: Filtering by status
- **WHEN** I select a status filter other than All
- **THEN** only entries in that status are shown

#### Scenario: Showing all statuses
- **WHEN** I select All
- **THEN** entries in every status are shown, grouped in the standard order

### Requirement: My list filter bar
The system SHALL present every my-list filter and sort control in a single bar directly below the status filter tabs, above the entries it applies to. The bar SHALL appear exactly once on the page — never repeated per status group — and SHALL apply to every group on screen alike. When the bar is wider than the viewport its controls SHALL wrap onto further lines rather than overflow or scroll horizontally, matching how the status tabs already wrap.

The bar SHALL be laid out as two clusters: the filters that narrow which entries are shown, and the controls that order them.

The bar SHALL offer a "Clear filters" action that restores the page's default view — no text query, no type restriction, no airing restriction, no score restriction, alphabetical primary sort in its natural direction, no tiebreaker, grouped by status. The action SHALL be shown only while at least one of those is not at its default, and SHALL NOT change the selected status tab, which is a separate control.

#### Scenario: One bar for the whole page
- **WHEN** the my-list page renders with several status groups on screen
- **THEN** the filter bar appears once below the status tabs, and no status group header carries its own copy of any filter or sort control

#### Scenario: Controls wrap on a narrow viewport
- **WHEN** the viewport is too narrow to fit the bar's controls on one line
- **THEN** the controls wrap onto additional lines and the page does not scroll horizontally

#### Scenario: Clearing filters
- **WHEN** I have narrowed or reordered the list and use "Clear filters"
- **THEN** the text query, type, airing-status and score filters are cleared, the sort returns to alphabetical with no tiebreaker, grouping by status is on, and the selected status tab is left as it was

#### Scenario: Clear action hidden at defaults
- **WHEN** every filter and sort control is at its default value
- **THEN** no "Clear filters" action is shown

### Requirement: Find in list
The system SHALL provide a text field in the my-list filter bar that narrows the list to entries whose title contains the typed text, matched case-insensitively against both the English title and the original title, so an entry is found under either name. The match SHALL be a substring match, not a prefix-only match. An empty field SHALL impose no restriction. The typed text SHALL combine with every other filter rather than replacing them.

#### Scenario: Narrowing by title text
- **WHEN** I type text into the find-in-list field
- **THEN** only entries whose English or original title contains that text, ignoring case, remain shown

#### Scenario: Matching the other title
- **WHEN** an entry is displayed under its English title and I type part of its original title
- **THEN** that entry is still shown

#### Scenario: Clearing the text
- **WHEN** I clear the find-in-list field
- **THEN** the text restriction is lifted and the remaining filters continue to apply

### Requirement: My list type filter
The system SHALL provide a multi-select type filter in the my-list filter bar. The filter SHALL offer only the media types actually present in my list — for example TV, Movie, OVA, ONA, Special, TV special, Music — plus an "Unknown" option when the list contains an entry whose type is not known, so the control never offers a choice that would return nothing.

Any combination of types SHALL be selectable. With none selected, no type restriction applies; with one or more selected, only entries of those types are shown. The control SHALL report its state in its label — the full set, the single selected type, or a count when several are selected — so the active restriction is readable without opening it.

Media types SHALL be shown by display label (for example "TV special"), not by the raw value the API returns (`tv_special`), and the same labelling SHALL be used on list rows.

#### Scenario: Showing a single type
- **WHEN** I select only Movie in the type filter
- **THEN** only movie entries are shown

#### Scenario: Showing several types
- **WHEN** I select TV and ONA
- **THEN** entries of either type are shown and all others are hidden

#### Scenario: No types selected
- **WHEN** no type is selected in the filter
- **THEN** entries of every type are shown

#### Scenario: Only present types are offered
- **WHEN** my list contains no music videos
- **THEN** the type filter does not offer Music as an option

#### Scenario: Types read as labels
- **WHEN** the type filter lists its options and a row shows its type
- **THEN** each reads as a display label such as "TV special" rather than `tv_special`

### Requirement: My list airing-status filter
The system SHALL provide a multi-select airing-status filter in the my-list filter bar offering Finished airing, Currently airing, Not yet aired, and — when the list contains one — entries whose airing status is unknown. The filter SHALL be available under every status tab, including All, not only under Plan to watch. With none selected, no airing restriction applies.

#### Scenario: Filtering to still-airing shows
- **WHEN** I select Currently airing while the Watching status tab is active
- **THEN** only entries I am watching whose anime is still airing are shown

#### Scenario: Available under every status tab
- **WHEN** any status tab is active, including All
- **THEN** the airing-status filter is offered

#### Scenario: No airing status selected
- **WHEN** no airing status is selected
- **THEN** entries of every airing status are shown

### Requirement: My list score filter
The system SHALL provide a score filter in the my-list filter bar with the options Any, Rated, and Unrated. Rated SHALL show only entries carrying a score of 1–10; Unrated SHALL show only entries with no score; Any SHALL impose no restriction.

#### Scenario: Finding unrated entries
- **WHEN** I select Unrated
- **THEN** only entries with no score of my own are shown

#### Scenario: Finding rated entries
- **WHEN** I select Rated
- **THEN** only entries carrying a score of mine are shown

### Requirement: My list two-level sorting
The system SHALL order my list by a primary sort key with an optional tiebreaker key, both chosen in the filter bar, so orderings such as "my score, then MAL score" or "episodes watched, then MAL score" are expressible directly.

Both selectors SHALL offer: Alphabetical, My score, MAL score, Episodes watched, Progress, Total episodes, Airing status, Type, Start date, and Finish date. The tiebreaker selector SHALL additionally offer "none", which is its default, and SHALL NOT offer the key already chosen as primary.

Each key SHALL have a natural direction — descending for My score, MAL score, Episodes watched, Progress, Total episodes, Start date and Finish date; ascending for Alphabetical and Type; and Finished airing → Currently airing → Not yet aired for Airing status. A direction control SHALL flip the primary key between its natural direction and the reverse; the tiebreaker SHALL always apply in its own natural direction.

Entries missing the value being sorted on — no score, no start or finish date, an unknown total or unknown airing status — SHALL sort last regardless of the direction chosen, rather than leading the list when the direction is flipped. When the primary and tiebreaker keys both tie, entries SHALL fall back to alphabetical order, so the same list always renders in the same order.

When Airing status is the primary key, the system SHALL continue to offer the existing control choosing which airing status is shown first, and SHALL order the remaining two statuses after it in their established cycle.

#### Scenario: Sorting by my score then MAL score
- **WHEN** I choose My score as the primary sort and MAL score as the tiebreaker
- **THEN** entries are ordered by my score highest first, and entries sharing the same score of mine are ordered among themselves by MAL score highest first

#### Scenario: Sorting by progress then MAL score
- **WHEN** I choose Episodes watched as the primary sort and MAL score as the tiebreaker
- **THEN** entries are ordered by episodes watched highest first, with ties broken by MAL score

#### Scenario: Reversing the primary direction
- **WHEN** I flip the direction control while sorted by My score
- **THEN** entries are ordered by my score lowest first, and the tiebreaker still applies in its own natural direction

#### Scenario: Missing values sort last in either direction
- **WHEN** the list is sorted by a key some entries have no value for, in either direction
- **THEN** the entries with no value appear at the end of the list

#### Scenario: Tiebreaker cannot repeat the primary key
- **WHEN** My score is the primary sort key
- **THEN** the tiebreaker selector does not offer My score

#### Scenario: Fully tied entries keep a stable order
- **WHEN** two entries tie on both the primary and tiebreaker keys
- **THEN** they appear in alphabetical order, and that order is the same every time the list renders

#### Scenario: Airing-status sort keeps its "show first" control
- **WHEN** Airing status is the primary sort key
- **THEN** a control offering which airing status to show first is shown, and choosing one orders that status's entries ahead of the other two

### Requirement: My list grouping is an explicit choice
The system SHALL provide a "group by status" toggle in the my-list filter bar, on by default, that decides whether entries are grouped rather than inferring it from the sort key.

With grouping on, entries SHALL appear under the standard status groups (Watching → On hold → Plan to watch → Completed → Dropped), with the active sort applied inside each group and no rank numbers. With grouping off, entries SHALL appear as one flat list ordered by the active sort. Changing the sort key SHALL NOT change whether the list is grouped, and changing the status tab SHALL NOT reset the sort.

#### Scenario: Grouping on with a score sort
- **WHEN** grouping is on and I sort by my score
- **THEN** entries stay grouped by status, and each group's entries are ordered by my score

#### Scenario: Turning grouping off
- **WHEN** I turn grouping off
- **THEN** entries appear as one flat list ordered by the active sort, with no status group headers

#### Scenario: Changing sort leaves grouping alone
- **WHEN** I change the sort key or direction
- **THEN** the list stays grouped or flat exactly as it was

#### Scenario: Changing status tab leaves the sort alone
- **WHEN** I switch from one status tab to another
- **THEN** the primary sort key, tiebreaker and direction are left as they were

### Requirement: My list reports what is being shown
The system SHALL show, whenever any filter is narrowing my list, a line reporting how many entries are shown out of the total in the current status tab. When no filter is narrowing the list, no such line SHALL be shown.

When filters exclude every entry, the system SHALL say that nothing matches the current filters and offer to clear them, rather than showing the "nothing here yet" message used for a genuinely empty status.

#### Scenario: Count while filtered
- **WHEN** a filter narrows the list
- **THEN** a line reports how many entries are shown out of the total for the active status tab

#### Scenario: No count when unfiltered
- **WHEN** no filter is narrowing the list
- **THEN** no count line is shown

#### Scenario: Filters match nothing
- **WHEN** the active filters exclude every entry
- **THEN** the page says nothing matches the current filters and offers to clear them

#### Scenario: Genuinely empty status
- **WHEN** the active status tab holds no entries at all and no filter is active
- **THEN** the page shows its "nothing here yet" message

### Requirement: My list edit opens an overlay
The system SHALL open the entry editor as an overlay on top of the my-list page when an entry's edit button is used, consistent with the editor overlay used everywhere edit/add-to-list actions appear.

#### Scenario: Opening the editor overlay
- **WHEN** I click an entry's edit button
- **THEN** an editor overlay opens on top of the page for that entry

### Requirement: My list rows edit the watched count in place
The system SHALL make the `watched` count in each my-list row's progress cell directly editable in place, per the "Inline editable episode count" requirement, so an entry's episode number can be set without opening the edit overlay. The row's edit button SHALL remain available for status, score, and rewatch-count changes. Saving an in-place count edit SHALL update that row's count and bar without reloading the page or re-sorting the list.

#### Scenario: Setting a row's count in place
- **WHEN** I click the count in a my-list row, type a number, and confirm
- **THEN** that row's episodes-watched is saved and its count and bar update in place, with no overlay opening

#### Scenario: Edit button still opens the overlay
- **WHEN** I click a row's edit button
- **THEN** the editor overlay opens as before, unaffected by the in-place count field

#### Scenario: Row stays in position after an in-place edit
- **WHEN** I save an in-place count edit on a row partway down the list
- **THEN** the list is not reloaded or reordered underneath me and the row keeps its position

### Requirement: Top anime ranked list
The system SHALL present a Top anime page as a ranked list covering up to rank 500, where every entry — whatever form it takes — shows its rank, picture, title, my score, and MAL score. Because this page ranks anime overall, an entry's anime may not be in my list; the flat-row tier (rank 11 and beyond) SHALL additionally carry a list-action button, conditional — "Add" when the anime is not yet in my list, and "Edit" when it is — the showcase and top-ten tiers (ranks 1–10) SHALL NOT carry one, keeping those tiers focused on the ranking and scores alone. The list SHALL be paginated at 50 entries per page, with page-number controls plus left/right arrows at the bottom, and left/right arrow controls at the top-right.

The page-number controls SHALL show the first page, the last page, and the current page with at most one page on each side of it, collapsing any gap between those groups into an ellipsis. On page 6 of 10 this yields `1 … 5 6 7 … 10`.

Changing the page from the bottom controls (page-number buttons or arrows) SHALL scroll the page back to the top, so the newly-shown entries are visible without the reader having to scroll up manually. The top-right arrows need no such scroll, since they already sit at the top of the page.

Changing the page, by any control, SHALL add a step to the browser's own navigation history rather than only updating local view state. Going back (the browser's back button, a keyboard shortcut, or a trackpad swipe gesture) SHALL therefore step to the previously-viewed page of the ranking, and going forward again SHALL return to the page just left — the same way back/forward already move between any other two pages in the app. Only once that page-by-page history is exhausted SHALL going back leave the Top anime page entirely, for whichever page preceded it in the browsing session.

The ranking SHALL be presented in three tiers, each visually distinct from the next, so the shape of the page itself communicates where an anime sits in the ranking:

- **Ranks 1–3 — showcase cards.** Three cards, each a self-contained bordered surface carrying its rank, poster, title, and both scores. They SHALL be laid out in ascending rank order following the reading direction, so the first card is rank 1; the presentation SHALL NOT reorder them visually away from their document order. Each card SHALL carry a prominent rank badge in a gold, silver, and bronze family for ranks 1, 2, and 3 respectively, and the card SHALL pick up its own medal colour beyond the badge (for example in its border and surface tint) so the three are distinguishable from one another at a glance and from every other tier. Rank 1 SHALL read as the most prominent of the three. The medal colours SHALL render the same in light and dark mode, since a medal's colour is its meaning. The card's two score chips SHALL be the same size as each other regardless of their label or value text differing in length, so the pair reads as one deliberate row rather than two mismatched boxes.
- **Ranks 4–10 — top-ten card row.** A single row of poster cards, smaller than the showcase cards, each carrying its own rank badge, title, and both scores, inside a bordered, coloured box of its own so the tier reads as a defined group rather than loose posters. The badge SHALL remain fully legible over any poster artwork, bright or dark, rather than relying on a translucent overlay whose contrast depends on the art beneath it.
- **Ranks 11 and beyond — flat rows.** The existing full-width rows, each with plain `#N` rank text, poster, title, my score, right-aligned MAL score, and its action button.

The rank of any entry SHALL be readable from the entry itself, in every tier, without relying on its position in the layout.

Because a page holds 50 entries, the showcase and top-ten tiers SHALL appear only on page 1; every entry on page 2 and beyond SHALL use the flat row form.

#### Scenario: Rendering the top-anime ranking
- **WHEN** the top-anime page loads
- **THEN** it shows the first page of 50 entries, each with its rank, picture, title, my score, and MAL score, plus a list-action button for entries in the flat-row tier

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
- **WHEN** I am on page 1 of the ranking, having reached it without changing pages since arriving, and go back
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

#### Scenario: A showcase card's two score boxes match
- **WHEN** I look at a showcase card's "My score" and "MAL" chips
- **THEN** the two boxes are the same width and height as each other, even though "My score" is longer text than "MAL"

#### Scenario: Ranks 4 to 10 form their own tier
- **WHEN** the top-anime page's first page renders
- **THEN** ranks 4 through 10 appear as one row of cards, each stating its rank, sized and styled distinctly from both the showcase cards above and the flat rows below

#### Scenario: A rank badge stays legible over bright poster art
- **WHEN** a top-ten card's poster art is bright, pale, or visually busy
- **THEN** its rank badge is still fully legible, because the badge does not depend on contrast against the artwork behind it

#### Scenario: Medal colours survive a theme switch
- **WHEN** I switch between light and dark mode
- **THEN** the gold, silver, and bronze accents on ranks 1, 2, and 3 still read as gold, silver, and bronze

#### Scenario: Card tiers do not appear on later pages
- **WHEN** I view page 2 or later of the top-anime ranking
- **THEN** every entry on that page uses the flat row form, since ranks 1–10 only appear on page 1

### Requirement: Daily refresh of the Top Anime ranking
The system SHALL re-fetch the Top Anime ranking's lean listing fields the first time it is visited on a local calendar day after its last fetch, serving it from cache on same-day revisits. If the ranking has never been visited, it SHALL never be proactively fetched.

At most one ranking refresh SHALL be in flight at a time: a second request arriving while a refresh is running SHALL wait for it and then serve the refreshed cache, rather than starting a second ranking fetch. A fetch that fails SHALL NOT count as the day's fetch — the next visit retries.

#### Scenario: New-day visit
- **WHEN** I open the Top Anime page and it has not been fetched yet on the current local calendar day
- **THEN** the system re-fetches the ranking live and updates the cache

#### Scenario: Same-day revisit
- **WHEN** I reopen the Top Anime page again on the same local day
- **THEN** it is served from the cache without a live re-fetch

#### Scenario: Concurrent visits share one refresh
- **WHEN** a second request for the ranking arrives while its refresh is already running
- **THEN** no additional MAL fetch is started, and the second request is served from the refreshed cache once the first completes

#### Scenario: A failed fetch does not consume the day
- **WHEN** the ranking refresh fails and I open the Top Anime page again the same day
- **THEN** the refresh is retried, because only a successful fetch marks the ranking as fetched for that day

#### Scenario: Never visited stays unfetched
- **WHEN** the Top Anime ranking has never been visited
- **THEN** no background job fetches it

### Requirement: Consistent list placement across my-list view modes
The system SHALL place the my-list entry list at the same vertical offset below its status header in flat (ungrouped) view as in grouped view, for every status filter. Switching between grouped and flat view, or changing the sort, SHALL NOT shift the list up or down relative to the header it sits under. The header-to-list spacing SHALL be defined by a single rule shared by both view modes, rather than by per-mode values that can diverge.

#### Scenario: Switching view mode does not shift the list
- **WHEN** I turn grouping off, or change the sort key or direction
- **THEN** the first entry row stays at the same vertical offset below its header, and only the ordering or grouping of the rows changes

#### Scenario: Spacing is consistent under every status filter
- **WHEN** the my-list page is in flat view under any status filter — All, Watching, Completed, Plan to watch, On hold, or Dropped
- **THEN** the gap between the header line and the first entry row matches the gap shown in grouped view

#### Scenario: Grouped view spacing is unchanged
- **WHEN** the my-list page is in grouped view
- **THEN** each status group's list sits directly below its group header at the established spacing, and consecutive status groups remain separated by the page's section spacing
