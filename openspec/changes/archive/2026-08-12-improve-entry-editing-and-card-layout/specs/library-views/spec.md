## ADDED Requirements

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

## MODIFIED Requirements

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
