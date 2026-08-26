## ADDED Requirements

### Requirement: Ranking an entry from its editor
The entry editor SHALL offer a **Rank** action whenever the entry it is editing is hand-orderable in the ranking — that is, it carries a score of mine, has aired, is not Plan to watch, is not dropped, and is not a Music, CM, or PV entry. Using it SHALL open the ranking editor over the entry editor, focused on that anime and showing only the score I gave it, so placing an anime I have just scored takes one action rather than a trip to the profile page.

The action SHALL NOT be offered for an entry that is not hand-orderable, since there is no row to open on. When a score is changed in the editor and saved, the Rank action SHALL open on the saved score rather than the score the entry carried when the editor opened.

#### Scenario: Ranking a scored entry
- **WHEN** I open the editor for an entry I scored 8 and use the Rank action
- **THEN** the ranking editor opens on score 8, with that anime's row in view and marked

#### Scenario: No Rank action without a score
- **WHEN** I open the editor for an entry with no score
- **THEN** no Rank action is offered

#### Scenario: No Rank action for a dropped entry
- **WHEN** I open the editor for a dropped entry, or for a Music, CM, or PV entry
- **THEN** no Rank action is offered, because the ranking places those by band and title

#### Scenario: Ranking follows a score just changed
- **WHEN** I change an entry's score from 7 to 9, save, and use the Rank action
- **THEN** the ranking editor opens on score 9

### Requirement: A saved score places the anime in the ranking
The system SHALL place an anime at the end of its score's hand-ordered band whenever a score is written, whatever path wrote it — the entry editor, the completion score prompt, a list row's score control, or any later addition — so no score-writing site can leave the ranking unmaintained. Placement SHALL happen as part of the same save, so the ranking is correct by the time the save reports success.

#### Scenario: Scored from the entry editor
- **WHEN** I score an anime 8 in the entry editor and save
- **THEN** it is the last hand-ordered 8 in the ranking

#### Scenario: Scored from the completion prompt
- **WHEN** I score an anime 8 in the completion prompt
- **THEN** it is the last hand-ordered 8 in the ranking, exactly as if the entry editor had written it

## MODIFIED Requirements

### Requirement: Score prompt on completion via the increment button
The system SHALL open a score prompt overlay whenever an episode-count change made from the progress row takes an entry into Completed status **with a finish date set** — whether from the "+" button or from editing the count in place — from every place that row appears (the main dashboard's currently-watching carousel, my list rows, the anime detail page, and any later addition). The overlay SHALL show the anime's picture on the left and a score dropdown offering "No score" and the values 1 through 10, pre-selected with the entry's current score.

The overlay SHALL offer three actions: skip, save, and **save and rank**. Save and rank SHALL save the chosen score and then open the ranking editor focused on that anime, so an anime can be finished, scored, and placed in one pass. It SHALL be offered only while the chosen score would leave the entry hand-orderable — with "No score" selected, or for a dropped or short-form anime, saving alone is the only save action offered.

Reaching a currently-airing anime's aired-so-far count completes the entry (per "Unknown total episodes cannot be completed" above) without a finish date, so it SHALL NOT open this prompt — asking for a score on a series that hasn't finished airing would be premature. The prompt SHALL open the first time an entry is completed with a finish date, whether that is a finished anime reaching its total or a currently-airing anime whose finale has aired and been watched once MyAnimeList's own airing status agrees.

#### Scenario: Final episode watched from any increment site
- **WHEN** I press the "+" button on an entry whose episodes-watched thereby reaches the anime's total episode count
- **THEN** a score prompt overlay opens showing that anime's picture and a score dropdown

#### Scenario: Count typed straight to the total
- **WHEN** I edit an entry's count in place and set it to the anime's total episode count
- **THEN** the same score prompt overlay opens, just as it would from the "+" button

#### Scenario: Prompt reflects an existing score
- **WHEN** the score prompt opens for an entry that already has a score
- **THEN** the dropdown is pre-selected with that score rather than "No score"

#### Scenario: The prompt offers to rank
- **WHEN** I pick a score of 1 through 10 in the completion prompt
- **THEN** a save-and-rank action is offered alongside save and skip

#### Scenario: No rank action without a score
- **WHEN** "No score" is selected in the completion prompt
- **THEN** no save-and-rank action is offered

#### Scenario: Non-final increments do not prompt
- **WHEN** I press the "+" button and episodes-watched stays below the anime's total episode count
- **THEN** no score prompt opens

#### Scenario: Catching up on a currently-airing anime does not prompt
- **WHEN** I press the "+" button on a currently-airing anime and episodes-watched thereby reaches the aired-so-far count
- **THEN** the entry becomes Completed but no score prompt opens, because no finish date was set

### Requirement: Saving or dismissing the completion score prompt
The system SHALL save a chosen score through the same entry-edit path as any other score change, so it is logged as activity, queued for MAL sync, and placed in the ranking. Dismissing the prompt — via a skip action, Esc, or a click outside the overlay — SHALL close it and leave the entry's score unchanged. Neither path SHALL undo the completion itself.

Save and rank SHALL save by that identical path and then open the ranking editor focused on the anime, on the score just saved. If the save fails, the ranking editor SHALL NOT open and the prompt SHALL behave exactly as a failed save does.

#### Scenario: Score given
- **WHEN** I pick a score in the completion prompt and confirm
- **THEN** the score is saved on the entry, logged as activity, queued for MAL sync, and the prompt closes

#### Scenario: Score given and ranked
- **WHEN** I pick a score in the completion prompt and choose save and rank
- **THEN** the score is saved exactly as a plain save would save it, and the ranking editor opens focused on that anime, on that score

#### Scenario: Prompt dismissed without a score
- **WHEN** I dismiss the completion prompt with skip, Esc, or a click outside
- **THEN** the prompt closes, the entry's score is unchanged, and the entry stays Completed

#### Scenario: Score save fails
- **WHEN** saving the chosen score fails
- **THEN** the prompt reports the failure and stays open so I can retry or dismiss it

#### Scenario: Save and rank when the save fails
- **WHEN** I choose save and rank and the save fails
- **THEN** the prompt reports the failure and stays open, and no ranking editor opens

### Requirement: Triggering view refreshes after the completion prompt closes
The system SHALL refresh the data of the view that triggered the completion prompt once the prompt closes, by either path, so the newly completed anime is reflected without a manual page reload — it disappears from the main dashboard's currently-watching carousel and reads as Completed in my list and on the anime detail page. Save and rank SHALL count as the prompt closing: the refresh SHALL run when the prompt closes rather than waiting for the ranking editor opened on top of it.

#### Scenario: Completing from the dashboard carousel
- **WHEN** I complete an anime with the "+" button in the currently-watching carousel and the prompt closes
- **THEN** the dashboard reloads and that anime is no longer in the currently-watching carousel

#### Scenario: Completing from my list
- **WHEN** I complete an anime with the "+" button in my list and the prompt closes
- **THEN** my list reloads and that entry shows as Completed, with any score I gave

#### Scenario: Completing from the anime detail page
- **WHEN** I complete an anime with the "+" button on its detail page and the prompt closes
- **THEN** the detail page reloads and shows the entry as Completed, with any score I gave

#### Scenario: Refresh is not held up by the ranking editor
- **WHEN** I choose save and rank from my list
- **THEN** my list reloads behind the ranking editor rather than waiting for me to close it
