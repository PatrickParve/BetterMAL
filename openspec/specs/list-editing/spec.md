# list-editing Specification

## Purpose
TBD - created by archiving change bootstrap-anime-tracker. Update Purpose after archive.
## Requirements
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

### Requirement: Unknown total episodes cannot be completed
The system SHALL display an unknown total as `watched/?` and SHALL NOT allow an anime with an unknown total episode count to be marked Completed.

#### Scenario: Blocking completion when total unknown
- **WHEN** an anime has an unknown total episode count and I attempt to mark it Completed
- **THEN** the system prevents the completion

#### Scenario: Displaying unknown total
- **WHEN** an anime with an unknown total is displayed with progress
- **THEN** progress is shown as `watched/?`

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

### Requirement: Adding an anime to my list
The system SHALL allow adding an anime that is not yet in my list (for example from the Top anime or Season pages), creating a UserAnimeEntry with a default status of Plan to watch and triggering the debounced sync so the new entry is pushed to MAL. Once added, the Add action SHALL become an Edit action that opens the editor overlay in place on the same page, consistent with edit actions everywhere.

#### Scenario: Adding from a page that ranks all anime
- **WHEN** I use the Add action on an anime not in my list
- **THEN** a UserAnimeEntry is created for it with status Plan to watch, and the change is logged and synced to MAL

#### Scenario: Add becomes Edit in place
- **WHEN** I have just added an anime to my list from such a page
- **THEN** its Add action becomes an Edit action that opens the editor overlay on the same page without navigating away

### Requirement: Inline editable episode count
The system SHALL make the `watched` half of the `watched/total` count directly editable in place wherever the count appears next to a plus control — the main dashboard's currently-watching carousel, my list rows, and the anime detail page. Clicking (or keyboard-focusing) the count SHALL turn it into a text field pre-filled with the current value and select its contents, so typing replaces the count rather than appending to it. At rest, the count SHALL occupy no more space than the plain `watched/total` text would, so it lines up with the equivalent static count shown elsewhere (e.g. the current-season aired/total bar).

The field SHALL accept only non-negative whole numbers: non-numeric characters SHALL be rejected, and negative values SHALL NOT be enterable or submittable, filtered as each character is typed. The enterable ceiling is the number of episodes actually available, not always the eventual total: for an anime that has finished airing (or whose total is otherwise the full known run), the ceiling is the total episode count; for an anime still airing, the ceiling is the number of episodes aired so far, which may be lower than the total (including when the total itself is still unknown). When neither an aired-so-far count nor a total is known, no upper cap applies. The field SHALL clamp to this ceiling live, as the value is typed, so a value above it is never shown even momentarily — not corrected only once confirmed. The plus control SHALL disable at this same ceiling, so it cannot increment past what the field would allow.

Confirming with Enter or by moving focus away (including clicking elsewhere on the page) SHALL save the value; Escape SHALL cancel and restore the previous count without saving. An empty or otherwise invalid entry SHALL revert to the stored value rather than saving, regardless of which of these three ways the field is closed. Confirming a value equal to the stored count SHALL NOT issue a save. A save SHALL go through the same entry-edit path as the plus control, so the started-date rule, activity log, debounced MAL sync, and completion prompt all apply, and the progress bar's fill SHALL update together with the count. If the save fails, the displayed count SHALL revert to the stored value.

While a save is in flight, the field and the plus control SHALL be disabled so the same entry cannot be edited twice concurrently.

#### Scenario: Setting a specific episode number
- **WHEN** I click the count on an entry at `3/12`, type `7`, and press Enter
- **THEN** episodes-watched is saved as 7, the count reads `7/12`, and the bar's fill grows to match

#### Scenario: Capped at the total once finished airing
- **WHEN** I try to enter a value above the total for an anime that has finished airing
- **THEN** the value is capped at the total rather than saved as entered

#### Scenario: Capped at episodes aired so far while still airing
- **WHEN** I try to enter a value above the number of episodes aired so far for an anime that is still airing
- **THEN** the value is capped at the aired-so-far count, even when the eventual total (once known) is higher

#### Scenario: Plus control disables at the aired-so-far ceiling
- **WHEN** an entry's episodes-watched already equals the number of episodes aired so far for a still-airing anime
- **THEN** the plus control is disabled, the same as it would be at the total for a finished show

#### Scenario: Value clamps live while typing
- **WHEN** I type a number above the applicable ceiling into the count field
- **THEN** the displayed value snaps down to the ceiling as I type it, rather than only being corrected once I confirm

#### Scenario: No cap when neither an aired count nor a total is known
- **WHEN** the count reads `watched/?` and no aired-so-far estimate is available either
- **THEN** the value is saved as entered, since no ceiling is known to cap against

#### Scenario: Negatives and non-numbers rejected
- **WHEN** I try to type a negative number or non-numeric text into the count field
- **THEN** the input is rejected and no negative or non-numeric value is ever saved

#### Scenario: Cancelling an edit
- **WHEN** I have typed a new value into the count field and press Escape
- **THEN** the field closes and the previous count is restored without saving

#### Scenario: Empty entry reverts on confirm or on clicking away
- **WHEN** I clear the count field and either press Enter or click elsewhere on the page
- **THEN** the stored value is restored and no save is issued, the same way in both cases

#### Scenario: Unchanged value issues no save
- **WHEN** I open the count field and confirm without changing the number
- **THEN** no save request is issued and the count stays as it was

#### Scenario: Failed save reverts
- **WHEN** I confirm a new count and the save request fails
- **THEN** the displayed count reverts to the stored value

### Requirement: Score prompt on completion via the increment button
The system SHALL open a score prompt overlay whenever an episode-count change made from the progress row takes an entry into Completed status — whether from the "+" button or from editing the count in place — from every place that row appears (the main dashboard's currently-watching carousel, my list rows, the anime detail page, and any later addition). The overlay SHALL show the anime's picture on the left and a score dropdown offering "No score" and the values 1 through 10, pre-selected with the entry's current score.

#### Scenario: Final episode watched from any increment site
- **WHEN** I press the "+" button on an entry whose episodes-watched thereby reaches the anime's total episode count
- **THEN** a score prompt overlay opens showing that anime's picture and a score dropdown

#### Scenario: Count typed straight to the total
- **WHEN** I edit an entry's count in place and set it to the anime's total episode count
- **THEN** the same score prompt overlay opens, just as it would from the "+" button

#### Scenario: Prompt reflects an existing score
- **WHEN** the score prompt opens for an entry that already has a score
- **THEN** the dropdown is pre-selected with that score rather than "No score"

#### Scenario: Non-final increments do not prompt
- **WHEN** I press the "+" button and episodes-watched stays below the anime's total episode count
- **THEN** no score prompt opens

### Requirement: Completion prompt fires only on entering Completed
The system SHALL open the completion score prompt only on the transition into Completed status, and SHALL NOT open it for an entry that was already Completed before the episode-count change nor for an anime whose total episode count is unknown.

#### Scenario: Rewatch increment on a completed entry
- **WHEN** I press the "+" button on an entry that is already Completed
- **THEN** no score prompt opens

#### Scenario: Re-entering the same total on a completed entry
- **WHEN** I edit the count in place on an already-Completed entry and confirm a value equal to the total
- **THEN** no score prompt opens, because the entry was already Completed

#### Scenario: Unknown total episode count
- **WHEN** I press the "+" button on an anime with an unknown total episode count
- **THEN** no score prompt opens, because the entry does not auto-complete

### Requirement: Saving or dismissing the completion score prompt
The system SHALL save a chosen score through the same entry-edit path as any other score change, so it is logged as activity and queued for MAL sync. Dismissing the prompt — via a skip action, Esc, or a click outside the overlay — SHALL close it and leave the entry's score unchanged. Neither path SHALL undo the completion itself.

#### Scenario: Score given
- **WHEN** I pick a score in the completion prompt and confirm
- **THEN** the score is saved on the entry, logged as activity, queued for MAL sync, and the prompt closes

#### Scenario: Prompt dismissed without a score
- **WHEN** I dismiss the completion prompt with skip, Esc, or a click outside
- **THEN** the prompt closes, the entry's score is unchanged, and the entry stays Completed

#### Scenario: Score save fails
- **WHEN** saving the chosen score fails
- **THEN** the prompt reports the failure and stays open so I can retry or dismiss it

### Requirement: Triggering view refreshes after the completion prompt closes
The system SHALL refresh the data of the view that triggered the completion prompt once the prompt closes, by either path, so the newly completed anime is reflected without a manual page reload — it disappears from the main dashboard's currently-watching carousel and reads as Completed in my list and on the anime detail page.

#### Scenario: Completing from the dashboard carousel
- **WHEN** I complete an anime with the "+" button in the currently-watching carousel and the prompt closes
- **THEN** the dashboard reloads and that anime is no longer in the currently-watching carousel

#### Scenario: Completing from my list
- **WHEN** I complete an anime with the "+" button in my list and the prompt closes
- **THEN** my list reloads and that entry shows as Completed, with any score I gave

#### Scenario: Completing from the anime detail page
- **WHEN** I complete an anime with the "+" button on its detail page and the prompt closes
- **THEN** the detail page reloads and shows the entry as Completed, with any score I gave

