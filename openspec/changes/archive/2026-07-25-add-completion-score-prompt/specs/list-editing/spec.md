## ADDED Requirements

### Requirement: Score prompt on completion via the increment button
The system SHALL open a score prompt overlay whenever an episode increment from the "+" button takes an entry into Completed status, from every place the "+" button appears (the main dashboard's currently-watching carousel, my list rows, the anime detail page, and any later addition). The overlay SHALL show the anime's picture on the left and a score dropdown offering "No score" and the values 1 through 10, pre-selected with the entry's current score.

#### Scenario: Final episode watched from any increment site
- **WHEN** I press the "+" button on an entry whose episodes-watched thereby reaches the anime's total episode count
- **THEN** a score prompt overlay opens showing that anime's picture and a score dropdown

#### Scenario: Prompt reflects an existing score
- **WHEN** the score prompt opens for an entry that already has a score
- **THEN** the dropdown is pre-selected with that score rather than "No score"

#### Scenario: Non-final increments do not prompt
- **WHEN** I press the "+" button and episodes-watched stays below the anime's total episode count
- **THEN** no score prompt opens

### Requirement: Completion prompt fires only on entering Completed
The system SHALL open the completion score prompt only on the transition into Completed status, and SHALL NOT open it for an entry that was already Completed before the increment nor for an anime whose total episode count is unknown.

#### Scenario: Rewatch increment on a completed entry
- **WHEN** I press the "+" button on an entry that is already Completed
- **THEN** no score prompt opens

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
