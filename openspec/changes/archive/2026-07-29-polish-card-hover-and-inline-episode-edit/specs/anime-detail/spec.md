## MODIFIED Requirements

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
