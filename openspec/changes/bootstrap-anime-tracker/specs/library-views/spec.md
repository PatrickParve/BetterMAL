## ADDED Requirements

### Requirement: My list grouped and ordered by status
The system SHALL present my list as one list grouped and ordered as Currently watching → On hold → Plan to watch → Completed → Dropped, where each entry shows picture, title, type (TV/movie), progress, my score, MAL score (respecting the hide/unhide toggle), and an edit button.

#### Scenario: Rendering the grouped list
- **WHEN** the my-list page loads
- **THEN** entries appear grouped in the order Currently watching, On hold, Plan to watch, Completed, Dropped, each showing picture, title, type, progress, my score, MAL score, and an edit button

### Requirement: Rank numbers when sorted by score
The system SHALL show rank numbers (e.g. `#1`) on the left of entries when the my-list page is sorted by score.

#### Scenario: Sorting my list by score
- **WHEN** I sort the my-list page by score
- **THEN** each entry shows a rank number on the left

### Requirement: My list status filter tabs
The system SHALL provide status filter controls on the my-list page — All, Watching, Completed, Plan to watch, On hold, Dropped — so that selecting one shows only entries in that status.

#### Scenario: Filtering by status
- **WHEN** I select a status filter other than All
- **THEN** only entries in that status are shown

#### Scenario: Showing all statuses
- **WHEN** I select All
- **THEN** entries in every status are shown, grouped in the standard order

### Requirement: My list quick-filter control
The system SHALL provide a quick-filter control on the my-list page offering at least sort by MAL score, sort by my score, and alphabetical ordering.

#### Scenario: Applying a quick filter
- **WHEN** I choose a quick filter (MAL score, my score, or alphabetical)
- **THEN** the list reorders accordingly

### Requirement: My list edit opens an overlay
The system SHALL open the entry editor as an overlay on top of the my-list page when an entry's edit button is used, consistent with the editor overlay used everywhere edit/add-to-list actions appear.

#### Scenario: Opening the editor overlay
- **WHEN** I click an entry's edit button
- **THEN** an editor overlay opens on top of the page for that entry

### Requirement: Top anime ranked list
The system SHALL present a Top anime page as a ranked list where each row shows rank number, picture, title, my score, MAL score (right-aligned), and a list-action button. Because this page ranks anime overall, a row's anime may not be in my list; the button SHALL therefore be conditional — "Add" when the anime is not yet in my list, and "Edit" when it is.

#### Scenario: Rendering the top-anime ranking
- **WHEN** the top-anime page loads
- **THEN** each row shows its rank number, picture, title, my score, a right-aligned MAL score, and a list-action button

#### Scenario: Row not in my list
- **WHEN** a top-anime row's anime is not in my list
- **THEN** its button reads "Add"; using it adds the anime with status Plan to watch and the button changes in place to "Edit"

#### Scenario: Row already in my list
- **WHEN** a top-anime row's anime is already in my list
- **THEN** its button reads "Edit" and opens the editor overlay in place when used

### Requirement: Daily refresh of the Top Anime ranking
The system SHALL re-fetch the Top Anime ranking's lean listing fields the first time it is visited on a local calendar day after its last fetch, serving it from cache on same-day revisits. If the ranking has never been visited, it SHALL never be proactively fetched.

#### Scenario: New-day visit
- **WHEN** I open the Top Anime page and it has not been fetched yet on the current local calendar day
- **THEN** the system re-fetches the ranking live and updates the cache

#### Scenario: Same-day revisit
- **WHEN** I reopen the Top Anime page again on the same local day
- **THEN** it is served from the cache without a live re-fetch

#### Scenario: Never visited stays unfetched
- **WHEN** the Top Anime ranking has never been visited
- **THEN** no background job fetches it
