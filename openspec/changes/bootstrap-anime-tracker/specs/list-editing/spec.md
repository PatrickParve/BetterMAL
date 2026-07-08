## ADDED Requirements

### Requirement: Started-date on first progress
The system SHALL set `started_at` to today when episodes-watched is set on an anime that was previously at 0 episodes or not yet in my list.

#### Scenario: First episode watched
- **WHEN** I set episodes-watched above 0 on an anime that had 0 (or was not in my list)
- **THEN** `started_at` is set to today

#### Scenario: Subsequent increments do not overwrite start date
- **WHEN** I increment episodes on an anime that already has a `started_at`
- **THEN** `started_at` is left unchanged

### Requirement: Completed-date lifecycle
The system SHALL set `completed_at` only when status is explicitly changed to Completed, and SHALL clear `completed_at` when status is changed away from Completed.

#### Scenario: Marking completed
- **WHEN** I change an entry's status to Completed
- **THEN** `completed_at` is set

#### Scenario: Un-completing
- **WHEN** I change a Completed entry to any other status
- **THEN** `completed_at` is cleared

### Requirement: Unknown total episodes cannot be completed
The system SHALL display an unknown total as `watched/?` and SHALL NOT allow an anime with an unknown total episode count to be marked Completed.

#### Scenario: Blocking completion when total unknown
- **WHEN** an anime has an unknown total episode count and I attempt to mark it Completed
- **THEN** the system prevents the completion

#### Scenario: Displaying unknown total
- **WHEN** an anime with an unknown total is displayed with progress
- **THEN** progress is shown as `watched/?`

### Requirement: Rewatch count is independently editable
The system SHALL expose rewatch count as an editable field in the status editor, independent of watch status.

#### Scenario: Editing rewatch count
- **WHEN** I change the rewatch count on any entry regardless of its status
- **THEN** the new rewatch count is saved

### Requirement: Edits log activity and trigger sync
The system SHALL, for every tracked field change (episode count, status, score, rewatch count), write an ActivityLog entry and trigger the debounced MAL sync.

#### Scenario: Change produces log and sync trigger
- **WHEN** a tracked field on an entry changes
- **THEN** an ActivityLog row is written and the entry's debounced sync is triggered

### Requirement: Edit and add-to-list open an overlay
The system SHALL open the entry editor as an overlay on top of the current page wherever an edit or add-to-list action appears (my list, top anime, season, detail, and dashboard), consistent across the app.

#### Scenario: Editing via overlay
- **WHEN** I trigger an edit or add-to-list action anywhere in the app
- **THEN** an editor overlay opens on top of the current page rather than navigating away

### Requirement: Editor excludes start/finish dates
The system SHALL NOT expose start or finish date fields in the entry editor; those dates are set automatically by the start/complete-date logic.

#### Scenario: No date fields in the editor
- **WHEN** the entry editor is open
- **THEN** it shows episode count, status, score, and rewatch count but no start/finish date fields

### Requirement: Adding an anime to my list
The system SHALL allow adding an anime that is not yet in my list (for example from the Top anime or Season pages), creating a UserAnimeEntry with a default status of Plan to watch and triggering the debounced sync so the new entry is pushed to MAL. Once added, the Add action SHALL become an Edit action that opens the editor overlay in place on the same page, consistent with edit actions everywhere.

#### Scenario: Adding from a page that ranks all anime
- **WHEN** I use the Add action on an anime not in my list
- **THEN** a UserAnimeEntry is created for it with status Plan to watch, and the change is logged and synced to MAL

#### Scenario: Add becomes Edit in place
- **WHEN** I have just added an anime to my list from such a page
- **THEN** its Add action becomes an Edit action that opens the editor overlay on the same page without navigating away
