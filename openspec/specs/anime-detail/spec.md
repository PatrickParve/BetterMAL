# anime-detail Specification

## Purpose
TBD - created by archiving change bootstrap-anime-tracker. Update Purpose after archive.
## Requirements
### Requirement: Single anime detail layout
The system SHALL show a single anime page with the title and a large picture on the left, and near the top-right of the title two separate side-by-side boxes: one showing rank and MAL score (MAL score respecting the hide/unhide toggle), and one showing my score and rewatch count. Below those it SHALL show an info box (type, status, source, duration, studio, aired-from/to, and genres) and, beneath it, a synopsis/background box. Any info field for which no data is available SHALL display "No info" rather than being blank.

The info box's Status field SHALL, when the anime is currently airing and an aired-episode count is known, read `Currently airing: <aired>/<total> ep aired`, using `?` in place of an unknown total episode count. When the anime is currently airing but no aired-episode count can be determined, the field SHALL read `Currently airing` with no counts appended. Statuses other than currently airing SHALL be displayed unchanged.

#### Scenario: Rendering the detail layout
- **WHEN** I open an anime's detail page
- **THEN** it shows the title with a large picture on the left, a "rank and MAL score" box and a separate "my score and rewatch count" box side by side, an info box (type, status, source, duration, studio, aired-from/to, genres), and a synopsis/background box

#### Scenario: MAL score respects the hide toggle
- **WHEN** the hide toggle is on
- **THEN** the MAL score in the rank/score box is blurred like everywhere else it appears

#### Scenario: Missing info field
- **WHEN** an info field (e.g. source or duration) has no data for that anime
- **THEN** that field shows "No info" instead of a blank value

#### Scenario: Currently airing with a known episode count
- **WHEN** I open the detail page of an anime that is currently airing, has 12 total episodes, and has had 5 episodes air so far
- **THEN** the Status field reads "Currently airing: 5/12 ep aired"

#### Scenario: Currently airing with an unknown total
- **WHEN** I open the detail page of a currently airing anime whose total episode count is unpublished and that has had 5 episodes air so far
- **THEN** the Status field reads "Currently airing: 5/? ep aired"

#### Scenario: Currently airing with no derivable aired count
- **WHEN** I open the detail page of a currently airing anime for which no aired-episode count can be derived
- **THEN** the Status field reads "Currently airing" with no episode counts appended

#### Scenario: Other airing statuses unaffected
- **WHEN** I open the detail page of an anime that has finished airing or has not yet aired
- **THEN** the Status field reads "Finished airing" or "Not yet aired" respectively, with no episode counts appended

### Requirement: Prequel/sequel links when they exist
The system SHALL show prequel and sequel link buttons in the top-right corner of the detail page, only when such related anime exist for that anime, each linking to the related anime's detail page.

#### Scenario: Related anime exist
- **WHEN** an anime has a prequel and/or a sequel
- **THEN** the corresponding link button(s) are shown and navigate to the related anime's detail page

#### Scenario: No related anime
- **WHEN** an anime has neither a prequel nor a sequel
- **THEN** no prequel/sequel buttons are shown

### Requirement: Progress bar and overlay status editor
The system SHALL show, below the picture, a progress bar (`watched/total`, or `watched/?` when the total is unknown) with the current status and an edit button next to it. The `watched` count SHALL be directly editable in place, per the "Inline editable episode count" requirement, so a specific episode number can be set without opening the overlay. The edit button SHALL open an overlay on top of the page for updating episodes watched, rewatch count, and score, applying the list-editing business rules. The editor SHALL NOT include start/finish date fields, since those are set automatically by the app's date logic.

#### Scenario: Opening the editor
- **WHEN** I click the edit button next to the progress bar
- **THEN** an overlay opens with fields for episodes watched, rewatch count, and score, and no start/finish date fields

#### Scenario: Saving an edit
- **WHEN** I change episodes watched, rewatch count, or score in the overlay
- **THEN** the change is saved with the standard start/complete-date, activity-log, and debounced-sync behavior

#### Scenario: Editing the count in place
- **WHEN** I click the `watched` count next to the detail page's progress bar, type a number, and confirm
- **THEN** episodes-watched is saved to that number and the bar and count update in place, without the overlay opening

#### Scenario: In-place edit unavailable without an entry
- **WHEN** the anime is not in my list, so no entry exists yet
- **THEN** the count is not editable in place and adding the anime still goes through the edit overlay

### Requirement: On-demand refresh action
The system SHALL provide an on-demand refresh action on the detail page for this anime's cached data.

#### Scenario: Refreshing this anime
- **WHEN** I trigger refresh on the detail page
- **THEN** the system performs a single-anime refresh and updates the displayed cached data

### Requirement: External MyAnimeList link from id
The system SHALL provide a link to the anime's MyAnimeList page, built as a URL template from the MAL id, requiring no API call. No AniList link is shown.

#### Scenario: Building the external link
- **WHEN** the detail page renders
- **THEN** it shows a MyAnimeList link constructed from the MAL id without making an API call, and shows no AniList link

