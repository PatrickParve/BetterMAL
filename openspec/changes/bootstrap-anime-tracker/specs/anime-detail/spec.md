## ADDED Requirements

### Requirement: Single anime detail layout
The system SHALL show a single anime page with the title and a large picture on the left, and near the top-right of the title two separate side-by-side boxes: one showing rank and MAL score (MAL score respecting the hide/unhide toggle), and one showing my score and rewatch count. Below those it SHALL show an info box (type, studio, aired-from/to, genre, and other available short-form fields) and, beneath it, a synopsis/background box.

#### Scenario: Rendering the detail layout
- **WHEN** I open an anime's detail page
- **THEN** it shows the title with a large picture on the left, a "rank and MAL score" box and a separate "my score and rewatch count" box side by side, an info box (type, studio, aired-from/to, genre, and other short fields), and a synopsis/background box

#### Scenario: MAL score respects the hide toggle
- **WHEN** the hide toggle is on
- **THEN** the MAL score in the rank/score box is blurred like everywhere else it appears

### Requirement: Prequel/sequel links when they exist
The system SHALL show prequel and sequel link buttons in the top-right corner of the detail page, only when such related anime exist for that anime, each linking to the related anime's detail page.

#### Scenario: Related anime exist
- **WHEN** an anime has a prequel and/or a sequel
- **THEN** the corresponding link button(s) are shown and navigate to the related anime's detail page

#### Scenario: No related anime
- **WHEN** an anime has neither a prequel nor a sequel
- **THEN** no prequel/sequel buttons are shown

### Requirement: External MAL and AniList links from id
The system SHALL provide links to the anime's MAL and AniList pages, built as URL templates from the MAL id, requiring no API call.

#### Scenario: Building external links
- **WHEN** the detail page renders
- **THEN** it shows MAL and AniList links constructed from the MAL id without making an API call

### Requirement: Progress bar and overlay status editor
The system SHALL show, below the picture, a progress bar (`watched/total`, or `watched/?` when the total is unknown) with the current status and an edit button next to it. The edit button SHALL open an overlay on top of the page for updating episodes watched, rewatch count, and score, applying the list-editing business rules. The editor SHALL NOT include start/finish date fields, since those are set automatically by the app's date logic.

#### Scenario: Opening the editor
- **WHEN** I click the edit button next to the progress bar
- **THEN** an overlay opens with fields for episodes watched, rewatch count, and score, and no start/finish date fields

#### Scenario: Saving an edit
- **WHEN** I change episodes watched, rewatch count, or score in the overlay
- **THEN** the change is saved with the standard start/complete-date, activity-log, and debounced-sync behavior

### Requirement: On-demand refresh action
The system SHALL provide an on-demand refresh action on the detail page for this anime's cached data.

#### Scenario: Refreshing this anime
- **WHEN** I trigger refresh on the detail page
- **THEN** the system performs a single-anime refresh and updates the displayed cached data
