## ADDED Requirements

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

## MODIFIED Requirements

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
