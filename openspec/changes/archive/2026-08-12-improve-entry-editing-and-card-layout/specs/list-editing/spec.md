## ADDED Requirements

### Requirement: Removing an entry from my list
The system SHALL provide, in the entry editor, a Delete action that removes the anime from my list entirely — locally and on MAL. The action SHALL be offered only for an anime that is already in my list, and SHALL NOT appear when the editor is open for an anime being added.

Because the action is destructive and irreversible from within the app, it SHALL require an explicit confirmation step naming the anime before anything is removed; abandoning the confirmation SHALL leave the entry untouched.

On confirmation the system SHALL delete the UserAnimeEntry, write an ActivityLog record of the removal, and queue the MAL removal durably (see the MAL write-sync capability), then close the editor. The anime's cached metadata SHALL be kept, since it is cache rather than user data and is still needed by browse pages.

The view the editor was opened from SHALL reflect the removal without a manual reload: a list row for the removed anime SHALL disappear, and a page dedicated to that anime SHALL show it as no longer in my list, with its edit action reverting to an add action.

#### Scenario: Deleting an entry
- **WHEN** I open the editor for an anime in my list, choose Delete, and confirm
- **THEN** the entry is removed from my list, the removal is logged, its MAL removal is queued, and the editor closes

#### Scenario: Cancelling the confirmation
- **WHEN** I choose Delete and then dismiss the confirmation without confirming
- **THEN** the entry is left exactly as it was and the editor stays open

#### Scenario: Delete not offered when adding
- **WHEN** the editor is open for an anime that is not yet in my list
- **THEN** no Delete action is shown

#### Scenario: Originating view updates in place
- **WHEN** I delete an entry from a list view
- **THEN** that row disappears from the list without a page reload

#### Scenario: Deleting from the anime's own page
- **WHEN** I delete an entry from the anime detail page
- **THEN** the page shows the anime as not in my list and offers an add action in place of the edit action

#### Scenario: Cached metadata survives removal
- **WHEN** an entry is removed from my list
- **THEN** the anime's cached metadata row is retained, so the anime still renders on browse and ranking pages

### Requirement: Start and finish dates are editable in the editor
The system SHALL expose the entry's start date and finish date in the entry editor behind a collapsed "Dates" disclosure, so the fields are available without adding permanent height to the form. Expanding the disclosure SHALL reveal both date fields pre-filled with the entry's stored dates, each of which SHALL be settable to a date or cleared to empty.

The system SHALL reject a finish date earlier than the entry's start date, reporting the problem rather than saving. A date that is unchanged SHALL NOT be sent as an edit.

A changed date SHALL go through the same entry-edit path as every other field, so it is logged as activity and queued for MAL sync with the entry's other values.

#### Scenario: Revealing the date fields
- **WHEN** I open the entry editor and expand the Dates disclosure
- **THEN** start-date and finish-date fields appear, pre-filled with the entry's stored dates

#### Scenario: Dates collapsed by default
- **WHEN** the entry editor opens
- **THEN** the date fields are hidden behind the collapsed Dates disclosure

#### Scenario: Setting a date
- **WHEN** I set a start or finish date and save
- **THEN** the date is stored on the entry, logged as activity, and queued for MAL sync

#### Scenario: Clearing a date
- **WHEN** I clear a date field that had a value and save
- **THEN** the entry's date is emptied and the change is logged and queued for sync

#### Scenario: Finish before start rejected
- **WHEN** I enter a finish date earlier than the start date and save
- **THEN** the save is rejected with an explanation and neither date is changed

### Requirement: Manually set dates are never overwritten
The system SHALL treat a date already present on an entry as authoritative: the automatic started-on-first-progress and finished-on-completion rules SHALL only fill a date that is empty, and SHALL NOT overwrite an existing value regardless of how it was set. Clearing a date manually SHALL make it eligible to be filled automatically again by those rules.

#### Scenario: Automatic rule leaves a manual start date alone
- **WHEN** I set a start date manually and then set episodes-watched above 0 on that entry
- **THEN** the start date I set is kept, not replaced with today

#### Scenario: Automatic rule leaves a manual finish date alone
- **WHEN** I set a finish date manually and then mark the entry Completed
- **THEN** the finish date I set is kept, not replaced with today

#### Scenario: Cleared date can be auto-filled again
- **WHEN** I clear an entry's start date and later set episodes-watched from 0 to above 0
- **THEN** the start date is set to today by the automatic rule

### Requirement: Entry editor controls share one size
The system SHALL render every control in the entry editor — dropdowns and text/number inputs alike — at the same width and the same height, so the form reads as a single aligned column rather than a mix of control sizes. Controls added to the editor later SHALL inherit the same sizing rather than defining their own.

#### Scenario: Dropdowns match inputs
- **WHEN** the entry editor is open
- **THEN** its status and score dropdowns are the same width and height as its episodes-watched and rewatch-count inputs

#### Scenario: Date fields match the rest
- **WHEN** the Dates disclosure is expanded
- **THEN** the date fields are the same width and height as every other control in the editor

## MODIFIED Requirements

### Requirement: Started-date on first progress
The system SHALL set `started_at` to today when episodes-watched is set on an anime that was previously at 0 episodes or not yet in my list, and only when `started_at` is currently empty.

#### Scenario: First episode watched
- **WHEN** I set episodes-watched above 0 on an anime that had 0 (or was not in my list) and has no start date
- **THEN** `started_at` is set to today

#### Scenario: Subsequent increments do not overwrite start date
- **WHEN** I increment episodes on an anime that already has a `started_at`
- **THEN** `started_at` is left unchanged

### Requirement: Completed-date lifecycle
The system SHALL set `completed_at` to today when an entry becomes Completed — whether by an explicit status change or by watching the final episode — and only when `completed_at` is currently empty. The system SHALL NOT clear or overwrite `completed_at` automatically when the status later changes away from Completed, mirroring MAL, which keeps the original finish date across rewatches; clearing a finish date SHALL be done through the editor's date fields.

#### Scenario: Marking completed
- **WHEN** I change an entry's status to Completed and it has no finish date
- **THEN** `completed_at` is set to today

#### Scenario: Existing finish date survives re-completion
- **WHEN** an entry that already has a `completed_at` becomes Completed again
- **THEN** the stored `completed_at` is left unchanged

#### Scenario: Un-completing keeps the finish date
- **WHEN** I change a Completed entry to any other status
- **THEN** `completed_at` is left as it was, and I can clear it myself in the editor's date fields

### Requirement: Rewatch count is independently editable
The system SHALL expose rewatch count as an editable field in the status editor, independent of watch status. The accepted range SHALL be 0 to 100 inclusive: a value above 100 or below 0 SHALL be rejected rather than stored, and the editor's field SHALL not allow one to be entered.

#### Scenario: Editing rewatch count
- **WHEN** I change the rewatch count on any entry regardless of its status
- **THEN** the new rewatch count is saved

#### Scenario: Rewatch count above the limit
- **WHEN** a rewatch count above 100 is submitted
- **THEN** it is rejected and the stored rewatch count is unchanged

#### Scenario: Editor caps the field
- **WHEN** I try to enter a rewatch count above 100 in the editor
- **THEN** the field does not accept the value

## REMOVED Requirements

### Requirement: Editor excludes start/finish dates
**Reason**: Superseded by "Start and finish dates are editable in the editor" — MAL exposes these dates and there was no way to correct an automatically derived one from inside the app, so a wrong date could only be fixed on MAL's own site and then pulled back in as a reconciliation diff.
**Migration**: None. The automatic start/finish date rules still apply exactly as before for empty dates (see "Manually set dates are never overwritten"); the editor now additionally shows the two fields behind a collapsed Dates disclosure.
