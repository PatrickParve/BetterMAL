## MODIFIED Requirements

### Requirement: My list quick-filter control
The system SHALL provide a quick-filter control on the my-list page offering at least sort by MAL score, sort by my score, and alphabetical ordering, plus sort by airing status when the Plan to watch status is the one being viewed. The control SHALL appear inline with the status title it applies to — on the same line as each visible status group's title in grouped view, or on the same line as the active filter's title (or "All") in ranked view — rather than in the page header.

#### Scenario: Applying a quick filter
- **WHEN** I choose a quick filter (MAL score, my score, alphabetical, or airing status)
- **THEN** the list reorders accordingly

#### Scenario: Sort control appears next to a status title
- **WHEN** the my-list page is grouped by status
- **THEN** the quick-filter control appears on the same line as each visible status group's title, not in the page header

#### Scenario: Airing status sort available only for Plan to watch
- **WHEN** the Plan to watch status filter is active
- **THEN** the quick-filter control offers an "Airing status" option that orders entries Finished airing → Currently airing → Not yet aired

#### Scenario: Airing status option hidden outside Plan to watch
- **WHEN** a status filter other than Plan to watch is active (including All)
- **THEN** the quick-filter control does not offer the airing status option

#### Scenario: Leaving Plan to watch while sorted by airing status
- **WHEN** airing status sort is active and I switch to a different status filter
- **THEN** the sort resets to alphabetical

### Requirement: Rank numbers when sorted by score
The system SHALL show rank numbers (e.g. `#1`) on the left of entries when the my-list page is sorted by MAL score, my score, or airing status, and SHALL show a single header line naming the active status filter (or "All") alongside the quick-filter control above the ranked list in this mode.

#### Scenario: Sorting my list by score
- **WHEN** I sort the my-list page by MAL score or my score
- **THEN** each entry shows a rank number on the left

#### Scenario: Sorting my list by airing status
- **WHEN** I sort the my-list page by airing status
- **THEN** each entry shows a rank number on the left, ordered Finished airing → Currently airing → Not yet aired

#### Scenario: Ranked view shows an active-filter header
- **WHEN** the my-list page is in ranked (non-alphabetical) mode
- **THEN** a header line above the list names the active status filter, or "All" when unfiltered, alongside the quick-filter control
