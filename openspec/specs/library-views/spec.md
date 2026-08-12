# library-views Specification

## Purpose
TBD - created by archiving change bootstrap-anime-tracker. Update Purpose after archive.
## Requirements
### Requirement: My list grouped and ordered by status
The system SHALL present my list as one list grouped and ordered as Currently watching → On hold → Plan to watch → Completed → Dropped, where each entry shows picture, title, type (TV/movie), progress, my score, MAL score (respecting the hide/unhide toggle), and an edit button. For entries in the **Plan to watch** group, each row SHALL additionally show an airing-status indicator alongside the type — **Not aired**, **Airing**, or **Aired** (mapped from the anime's `not_yet_aired`, `currently_airing`, and `finished_airing` values) — so the user can tell at a glance whether a queued show is already out, still airing, or has not yet started; when the airing status is unknown, no indicator is shown. Rows in other status groups SHALL NOT show the airing-status indicator.

#### Scenario: Rendering the grouped list
- **WHEN** the my-list page loads
- **THEN** entries appear grouped in the order Currently watching, On hold, Plan to watch, Completed, Dropped, each showing picture, title, type, progress, my score, MAL score, and an edit button

#### Scenario: Plan-to-watch row shows airing status
- **WHEN** the Plan to watch group renders an entry whose anime has a known airing status
- **THEN** that row shows an airing-status indicator (Not aired, Airing, or Aired) next to the type

#### Scenario: Airing status only on Plan to watch
- **WHEN** an entry is in any group other than Plan to watch
- **THEN** its row shows the type without an airing-status indicator

#### Scenario: Unknown airing status shows no indicator
- **WHEN** a Plan to watch entry's anime has no known airing status
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
The system SHALL show rank numbers (e.g. `#1`) on the left of entries when the my-list page is sorted by MAL score, my score, or airing status, and SHALL show a single header line naming the active status filter (or "All") alongside the quick-filter control above the ranked list in this mode.

The rank SHALL occupy a fixed-width column sized for the longest rank the list can produce (at least three digits), independent of the rank actually shown on a given row. The `#` SHALL start at the same horizontal position on every row, and the poster that follows SHALL start at the same horizontal position on every row, so a rank of any length neither shifts the posters out of alignment nor runs underneath one.

#### Scenario: Sorting my list by score
- **WHEN** I sort the my-list page by MAL score or my score
- **THEN** each entry shows a rank number on the left

#### Scenario: Sorting my list by airing status
- **WHEN** I sort the my-list page by airing status
- **THEN** each entry shows a rank number on the left, ordered Finished airing → Currently airing → Not yet aired

#### Scenario: Ranked view shows an active-filter header
- **WHEN** the my-list page is in ranked (non-alphabetical) mode
- **THEN** a header line above the list names the active status filter, or "All" when unfiltered, alongside the quick-filter control

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

### Requirement: My list quick-filter control
The system SHALL provide a quick-filter control on the my-list page offering at least sort by MAL score, sort by my score, and alphabetical ordering, plus sort by airing status when the Plan to watch status is the one being viewed.

Exactly one quick-filter control SHALL be shown at a time, above the entries it orders. When a single status is being viewed — in either grouped or ranked mode — the control SHALL sit inline with that status's title. When the All filter is active in grouped mode, so several status groups are on screen at once, the control SHALL appear once above the first group rather than repeating on each group's header, since it applies to every group alike.

#### Scenario: Applying a quick filter
- **WHEN** I choose a quick filter (MAL score, my score, alphabetical, or airing status)
- **THEN** the list reorders accordingly

#### Scenario: Sort control appears next to a single status title
- **WHEN** the my-list page is filtered to one status
- **THEN** the quick-filter control appears on the same line as that status's title, not in the page header

#### Scenario: One control for the All view
- **WHEN** the All filter is active and the page shows several status groups
- **THEN** the quick-filter control appears once above the groups, and no status group header carries its own copy

#### Scenario: The single control orders every group
- **WHEN** I change the quick filter in the All view
- **THEN** every status group reorders accordingly

#### Scenario: Airing status sort available only for Plan to watch
- **WHEN** the Plan to watch status filter is active
- **THEN** the quick-filter control offers an "Airing status" option that orders entries Finished airing → Currently airing → Not yet aired

#### Scenario: Airing status option hidden outside Plan to watch
- **WHEN** a status filter other than Plan to watch is active (including All)
- **THEN** the quick-filter control does not offer the airing status option

#### Scenario: Leaving Plan to watch while sorted by airing status
- **WHEN** airing status sort is active and I switch to a different status filter
- **THEN** the sort resets to alphabetical

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
The system SHALL present a Top anime page as a ranked list covering up to rank 500, where each row shows rank number, picture, title, my score, MAL score (right-aligned), and a list-action button. Because this page ranks anime overall, a row's anime may not be in my list; the button SHALL therefore be conditional — "Add" when the anime is not yet in my list, and "Edit" when it is. The list SHALL be paginated at 50 rows per page, with page-number controls plus left/right arrows at the bottom, and left/right arrow controls at the top-right.

The page-number controls SHALL show the first page, the last page, and the current page with at most one page on each side of it, collapsing any gap between those groups into an ellipsis. On page 6 of 10 this yields `1 … 5 6 7 … 10`.

#### Scenario: Rendering the top-anime ranking
- **WHEN** the top-anime page loads
- **THEN** it shows the first page of 50 rows, each with its rank number, picture, title, my score, a right-aligned MAL score, and a list-action button

#### Scenario: Paginating the ranking
- **WHEN** I use the page-number controls or the left/right arrows (at the bottom or top-right)
- **THEN** the list shows the corresponding 50-row page, up to rank 500

#### Scenario: Page numbers in the middle of the range
- **WHEN** I am on page 6 of 10
- **THEN** the page-number controls show 1, an ellipsis, 5, 6, 7, an ellipsis, and 10 — and no other page numbers

#### Scenario: Page numbers near an edge of the range
- **WHEN** I am on page 2 of 10
- **THEN** the page-number controls show 1, 2, 3, an ellipsis, and 10, with no ellipsis between adjacent pages

#### Scenario: Row not in my list
- **WHEN** a top-anime row's anime is not in my list
- **THEN** its button reads "Add"; using it adds the anime with status Plan to watch and the button changes in place to "Edit"

#### Scenario: Row already in my list
- **WHEN** a top-anime row's anime is already in my list
- **THEN** its button reads "Edit" and opens the editor overlay in place when used

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

### Requirement: Consistent list placement across my-list sort modes
The system SHALL place the my-list entry list at the same vertical offset below its status header in ranked view (sorted by MAL score, my score, or airing status) as in grouped view (alphabetical), for every status filter. Changing the sort SHALL NOT shift the list up or down relative to the header it sits under. The header-to-list spacing SHALL be defined by a single rule shared by both view modes, rather than by per-mode values that can diverge.

#### Scenario: Switching sort does not shift the list
- **WHEN** I change the my-list sort from Alphabetical to MAL score, my score, or airing status
- **THEN** the first entry row stays at the same vertical offset below its status header, and only the ordering of the rows changes

#### Scenario: Spacing is consistent under every status filter
- **WHEN** the my-list page is in ranked view under any status filter — All, Watching, Completed, Plan to watch, On hold, or Dropped
- **THEN** the gap between the header line and the first entry row matches the gap shown in grouped view

#### Scenario: Grouped view spacing is unchanged
- **WHEN** the my-list page is in grouped view
- **THEN** each status group's list sits directly below its group header at the established spacing, and consecutive status groups remain separated by the page's section spacing
