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

The bar SHALL offer a "Clear filters" action that restores the page's default view — no text query, no type restriction, no airing restriction, no score restriction, the Started filter off, alphabetical primary sort in its natural direction, no tiebreaker, grouped by status. The action SHALL be shown only while at least one of those is not at its default, and SHALL NOT change the selected status tab, which is a separate control.

#### Scenario: One bar for the whole page
- **WHEN** the my-list page renders with several status groups on screen
- **THEN** the filter bar appears once below the status tabs, and no status group header carries its own copy of any filter or sort control

#### Scenario: Controls wrap on a narrow viewport
- **WHEN** the viewport is too narrow to fit the bar's controls on one line
- **THEN** the controls wrap onto additional lines and the page does not scroll horizontally

#### Scenario: Clearing filters
- **WHEN** I have narrowed or reordered the list and use "Clear filters"
- **THEN** the text query, type, airing-status, score and Started filters are cleared, the sort returns to alphabetical with no tiebreaker, grouping by status is on, and the selected status tab is left as it was

#### Scenario: Clear action hidden at defaults
- **WHEN** every filter and sort control is at its default value
- **THEN** no "Clear filters" action is shown

#### Scenario: The Started filter counts as off-default
- **WHEN** the Started filter is the only control I have changed
- **THEN** the "Clear filters" action is shown, and using it turns the filter off

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
The system SHALL provide a score filter in the my-list filter bar with the options Any, Rated, Unrated, and one option per score value from 10 down to 1. Rated SHALL show only entries carrying a score of 1–10; Unrated SHALL show only entries with no score; a score-value option SHALL show only entries carrying exactly that score; Any SHALL impose no restriction.

The score-value options SHALL be presented alongside Rated and Unrated in the same control, each naming the score it selects, so choosing "the anime I scored 8" is one selection rather than a filter plus a sort.

#### Scenario: Finding unrated entries
- **WHEN** I select Unrated
- **THEN** only entries with no score of my own are shown

#### Scenario: Finding rated entries
- **WHEN** I select Rated
- **THEN** only entries carrying a score of mine are shown

#### Scenario: Finding one score
- **WHEN** I select the score 8
- **THEN** only entries I scored exactly 8 are shown, and entries scored 7 or 9 are not

#### Scenario: A score with no entries
- **WHEN** I select a score I have given to nothing in the current view
- **THEN** the list reports that nothing matches, rather than falling back to every rated entry

### Requirement: My list started filter
The system SHALL provide a **Started** filter in the my-list filter bar that narrows the list to entries I have watched at least one episode of, whatever their status. Off — its default — it SHALL impose no restriction. It SHALL combine with every other filter rather than replacing them, and SHALL be presented as a two-state control in the filter bar's narrowing cluster, in the same style as the bar's other toggles.

#### Scenario: Narrowing to what I have started
- **WHEN** I turn the Started filter on
- **THEN** only entries with at least one episode watched are shown, whichever status they hold

#### Scenario: Off by default
- **WHEN** the my-list page renders without the filter being set
- **THEN** entries with no episodes watched are shown alongside the rest

#### Scenario: Combining with the type filter
- **WHEN** I turn the Started filter on and select Movie in the type filter
- **THEN** only films I have watched are shown

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

### Requirement: Top anime ranking list selector
The Top anime page SHALL offer a selector that chooses which of MyAnimeList's rankings the page shows, presented as a row of buttons — one per list — placed on its own row below the page's title, sharing that row with the ranking's pagination controls so the two are readable together without crowding the title. The buttons SHALL wrap onto further lines when the window is too narrow to hold them on one, and the pagination controls SHALL drop to their own line beneath the selector when there is no longer room for both on one line, rather than either overflowing or scrolling horizontally.

The selectable lists SHALL be exactly: **All**, **TV**, **Movie**, **OVA**, **Special**, **Popularity**, and **Favourite**. The system SHALL NOT offer an ONA or a Music list, because MyAnimeList's API does not expose either as a ranking, and SHALL NOT introduce a second data provider to supply them.

The button for the list currently shown SHALL be visibly highlighted as selected, distinctly from the unselected buttons, and SHALL expose that selected state to assistive technology rather than signalling it by colour alone. The remaining buttons SHALL stay interactive at all times, including while a newly selected list is still loading, so a mis-click can be corrected without waiting.

A fresh visit to the Top anime page SHALL show the All list. Selecting a list SHALL add a step to the browser's navigation history, so going back returns to the list previously being viewed, and SHALL reset the ranking to its first page — a page number from one list does not carry over to another. Selecting the list already shown SHALL do nothing: no history step and no reload.

Every list SHALL be presented with the same three-tier layout, pagination, and per-entry content as the All list, since each list is ranked 1–500 in its own right.

#### Scenario: Opening Top anime shows the All list
- **WHEN** I reach the Top anime page by its navbar link
- **THEN** the All list is shown and the All button is highlighted as selected

#### Scenario: Selecting a list switches the ranking
- **WHEN** I select the Movie button
- **THEN** the page shows MyAnimeList's top movies, ranked from 1, and the Movie button becomes the highlighted one while All is no longer highlighted

#### Scenario: The selected list survives leaving and coming back
- **WHEN** I select the Favourite list, open an anime from it, and then go back
- **THEN** I return to the Favourite list with its button still highlighted, not to the All list

#### Scenario: Going back steps to the previously selected list
- **WHEN** I select Movie and then select OVA, and then go back
- **THEN** I return to the Movie list

#### Scenario: Switching lists returns to the first page
- **WHEN** I am on page 4 of the All list and select the TV button
- **THEN** the TV list is shown from its first page, with its showcase and top-ten tiers visible

#### Scenario: Selecting the list already shown does nothing
- **WHEN** the Movie list is shown and I select the Movie button again
- **THEN** the page does not reload the list and going back still leads to whatever preceded the Movie list

#### Scenario: No ONA or Music button is offered
- **WHEN** I look at the ranking list selector
- **THEN** there are exactly seven buttons — All, TV, Movie, OVA, Special, Popularity, Favourite — with no ONA or Music option

#### Scenario: Selection is not signalled by colour alone
- **WHEN** the page is read by assistive technology
- **THEN** the selected list's button reports itself as pressed/selected, so the active list is discoverable without seeing the highlight

### Requirement: Fast switching between ranking lists
Switching between ranking lists SHALL NOT blank the page. A list already loaded during the current application session SHALL be shown immediately when re-selected, without a network request and without any loading state.

When a list not yet loaded in this session is selected, the page SHALL keep the previously shown list rendered — visually muted and non-interactive, so it is unmistakably not the list the highlighted button names — until the new list arrives, rather than replacing it with a loading message or an empty page. Only the page's very first load, when there is no list on screen at all, SHALL show a plain loading state.

#### Scenario: Returning to an already-loaded list is instant
- **WHEN** I select Movie, then All, then Movie again
- **THEN** the second Movie selection renders immediately with no loading state and no further request

#### Scenario: Loading an unseen list keeps the current one visible
- **WHEN** I select a list for the first time this session and its data has not arrived yet
- **THEN** the list I was reading stays on screen, muted and not interactive, until the new list replaces it

#### Scenario: The selector stays usable while a list loads
- **WHEN** a newly selected list is still loading
- **THEN** I can select a different list without waiting for the first one to finish

#### Scenario: First arrival shows a loading state
- **WHEN** I open the Top anime page for the first time in a session and no list has loaded yet
- **THEN** a plain loading indicator is shown, because there is no previous list to keep on screen

### Requirement: List membership edits apply across every loaded ranking list
Adding or editing an anime from one ranking list SHALL update that anime wherever it appears in every other ranking list loaded in the current session, since the lists overlap. Returning to another list SHALL therefore never offer to add an anime that is already in my list.

#### Scenario: Adding from one list updates another
- **WHEN** I add an anime to my list from the Movie ranking, and that anime also appears in the All ranking I loaded earlier
- **THEN** going back to the All ranking shows that anime with an "Edit" action, not "Add"

### Requirement: Top anime ranked list
The system SHALL present a Top anime page as a ranked list covering up to rank 500, where every entry — whatever form it takes — shows its rank, picture, title, my score, and MAL score. The page shows one selected ranking list at a time (see "Top anime ranking list selector"); everything below applies to whichever list is selected. Because this page ranks anime overall, an entry's anime may not be in my list; the flat-row tier (rank 11 and beyond) SHALL additionally carry a list-action button, conditional — "Add" when the anime is not yet in my list, and "Edit" when it is — the showcase and top-ten tiers (ranks 1–10) SHALL NOT carry one, keeping those tiers focused on the ranking and scores alone. The list SHALL be paginated at 50 entries per page, with page-number controls plus left/right arrows at the bottom, and left/right arrow controls at the top-right.

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
- **THEN** it shows the first page of 50 entries of the selected list, each with its rank, picture, title, my score, and MAL score, plus a list-action button for entries in the flat-row tier

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

#### Scenario: A filtered list is ranked in its own right
- **WHEN** I view the Movie list
- **THEN** its entries are numbered from rank 1, with the top three in the showcase tier, rather than carrying their positions in the overall ranking

### Requirement: Daily refresh of the Top Anime ranking
The system SHALL re-fetch a Top Anime ranking list's lean listing fields the first time that list is viewed on a local calendar day after its own last fetch, serving it from cache on same-day revisits. Each selectable list SHALL keep its own last-fetched time, so viewing one list never marks another as fetched for the day. If a list has never been viewed, it SHALL never be proactively fetched — opening the page SHALL NOT warm the lists the user has not selected.

At most one refresh per list SHALL be in flight at a time: a second request for the same list arriving while its refresh is running SHALL wait for it and then serve the refreshed cache, rather than starting a second fetch of that list. Requests for two different lists SHALL NOT wait on each other. A fetch that fails SHALL NOT count as that list's fetch for the day — the next visit to that list retries — and SHALL NOT prevent the page from serving whatever is already cached for it.

#### Scenario: New-day visit
- **WHEN** I open a Top Anime ranking list that has not been fetched yet on the current local calendar day
- **THEN** the system re-fetches that list live and updates its cache

#### Scenario: Same-day revisit
- **WHEN** I reopen the same ranking list again on the same local day
- **THEN** it is served from the cache without a live re-fetch

#### Scenario: Each list has its own daily clock
- **WHEN** I have already viewed the All list today and then select the Movie list for the first time today
- **THEN** the Movie list is fetched live, because the All list's fetch does not count as Movie's

#### Scenario: Concurrent visits share one refresh
- **WHEN** a second request for the same ranking list arrives while its refresh is already running
- **THEN** no additional MAL fetch is started, and the second request is served from the refreshed cache once the first completes

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

### Requirement: Recap a period control on my list
The system SHALL offer a **Recap a period** control on the my-list page, in the same row as the status filter tabs and visually separated from them, so that recapping a period is reachable from the list it recaps. The control is a navigation entry point rather than a filter or sort control, so it SHALL sit outside the filter bar and SHALL NOT change which entries the list is showing when it is opened or dismissed.

Activating the control SHALL open an overlay in which the user picks the recap type (multi-year, yearly, or season) and the settings that type needs — the years for a multi-year recap, the year for a yearly one, the year and season for a season one, and the time filter for the multi-year and yearly types. Confirming the selection SHALL open the recap for that period; dismissing the overlay SHALL leave the my-list page exactly as it was.

The overlay SHALL NOT offer a period or time filter that would produce an empty recap, applying the same availability rule the recap itself uses: an option covering no entries is shown as unavailable and cannot be confirmed.

#### Scenario: Opening the recap picker
- **WHEN** I activate **Recap a period** on the my-list page
- **THEN** an overlay opens offering the multi-year, yearly, and season recap types with the settings each needs

#### Scenario: Confirming a period
- **WHEN** I pick a yearly recap for 2022 and confirm
- **THEN** the recap for 2022 opens

#### Scenario: Dismissing the picker
- **WHEN** I open the overlay and dismiss it without confirming
- **THEN** the my-list page is unchanged — same status filter, same filter bar selections, same scroll position

#### Scenario: The control sits with the status tabs
- **WHEN** the my-list page renders
- **THEN** **Recap a period** appears in the status-tab row, set apart from the All/Watching/Completed/Plan to watch/On hold/Dropped tabs, and not among the filter bar's controls

#### Scenario: Unavailable options cannot be confirmed
- **WHEN** the picker offers a time filter that would include no entries for the selected period
- **THEN** that option is shown as unavailable and cannot be confirmed

### Requirement: My list opens scoped to a recap period
The system SHALL let the my-list page open scoped to a recap's period, time filter, and media type, arriving from the recap's "see all" control, and SHALL then show exactly the anime that recap included — no more and no fewer — whatever their watch statuses.

The scope SHALL be carried in the page URL so it survives a reload and back-navigation, and SHALL be shown on the page as a labelled, dismissible indicator naming the period it represents, so a narrowed list is never mistaken for the whole list. Dismissing it SHALL return the page to the unscoped list without disturbing the status filter, filter bar, or sort selections.

While a recap scope is active the page's own status tabs, filter bar, and sorting SHALL continue to work, narrowing and ordering within the scoped set rather than escaping it. The page's "showing N of M" count SHALL report against the scoped set.

The page SHALL additionally accept, alongside a scope, a narrowing that names one of the recap's stats or one of its score-distribution rows. Such a narrowing SHALL be applied by setting the page's **own** controls — the status tab, the type filter, the score filter, and the Started filter — to the values that express it, rather than as a second, hidden scope, so it is visible on arrival and can be adjusted or cleared with the page's ordinary controls. Controls the narrowing does not concern SHALL be left at their defaults, and the resulting set SHALL match the number that was followed.

A narrowing SHALL be applied once, on arrival. Changing any of those controls afterwards SHALL take effect and SHALL NOT be reverted, and dismissing the scope SHALL clear the narrowing along with it. Returning to the page with the browser's back or forward buttons SHALL restore the controls as they were left, not as they arrived.

#### Scenario: Arriving from a recap
- **WHEN** I open my list from a fall 2019 recap narrowed to TV
- **THEN** the list shows exactly the TV anime that recap included, across every watch status they hold

#### Scenario: The scope is labelled
- **WHEN** my list is showing a recap scope
- **THEN** an indicator names the period, time filter, and media type the scope represents

#### Scenario: Dismissing the scope
- **WHEN** I dismiss the scope indicator
- **THEN** the full list returns, with my status filter, filter-bar selections, and sort untouched

#### Scenario: Filtering within a scope
- **WHEN** a recap scope is active and I select the Completed status tab
- **THEN** only completed entries from within the scoped set are shown, not completed entries from the whole list

#### Scenario: Counting within a scope
- **WHEN** a recap scope is active and further filters narrow it
- **THEN** the "showing N of M" line reports N and M against the scoped set

#### Scenario: The scope survives a reload
- **WHEN** I reload the page, or return to it with the browser's back button
- **THEN** the same recap scope is still applied

#### Scenario: Arriving from a recap stat
- **WHEN** I open my list by following a 2020 recap's **Movies watched** stat
- **THEN** the page arrives scoped to 2020 with its type filter set to Movie and its Started filter on, and shows exactly the films of that period I have watched

#### Scenario: Arriving from a distribution row
- **WHEN** I open my list by following a recap distribution's score-8 row
- **THEN** the page arrives scoped to that period with its score filter set to 8, and its status tab left on All

#### Scenario: The arrival narrowing is adjustable
- **WHEN** I arrive from a recap stat and then change the control it set
- **THEN** the change takes effect and is not reverted, and "Clear filters" restores the page's defaults within the scope

#### Scenario: Dismissing clears the narrowing too
- **WHEN** I arrive from a recap stat and dismiss the scope indicator
- **THEN** the full list returns with the arrival narrowing gone, not reapplied
