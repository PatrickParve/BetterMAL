## MODIFIED Requirements

### Requirement: Watching a rewatch to the end counts it automatically

The system SHALL return a Rewatching entry to Completed when its episodes watched reaches the anime's total episode count — whether by the "+" control or by an in-place count edit — and SHALL increase the entry's rewatch count by one at the same moment, without asking. The target is always the total: a Rewatching entry is always on an anime that has finished airing, per "Rewatching requires a finished anime the user has finished once", so there is no aired-so-far case here.

The completion-score prompt SHALL NOT open on this transition when the entry already carries a score. That score is from an earlier viewing, and asking again at the end of every rewatch would be noise. The score SHALL remain editable in the editor as usual. When the entry carries **no** score, the prompt SHALL open exactly as it does for a first completion, per "Completion prompt fires only on entering Completed". Whether or not the prompt opens, and however it is closed, the rewatch count SHALL already have increased.

The entry's finish date SHALL NOT be overwritten with the rewatch's finish date, per the existing never-overwrite rule.

#### Scenario: Rewatch watched to the end

- **WHEN** a Rewatching entry for a 24-episode finished anime reaches 24 episodes watched
- **THEN** its status becomes Completed and its rewatch count increases by one, with nothing asked about the rewatch count

#### Scenario: No score prompt when the rewatch already has a score

- **WHEN** a rewatch of an entry scored 8 reaches the last episode
- **THEN** no completion-score prompt opens, and the entry's score is still 8

#### Scenario: Score prompt when the rewatch has no score

- **WHEN** a rewatch of an entry with no score reaches the last episode
- **THEN** the completion-score prompt opens, exactly as it would for a first completion, and the rewatch count has already increased by one

#### Scenario: The original finish date is kept

- **WHEN** a rewatch re-completes an entry that already has a finish date
- **THEN** that finish date is left exactly as it was

### Requirement: Ending a rewatch early asks whether it counts

Where a Rewatching entry is returned to Completed by **choosing Completed in the entry editor**, rather than by its episodes watched reaching the total, the system SHALL ask whether the rewatch should be counted, and SHALL NOT decide on its own.

The system cannot distinguish a user who abandoned a rewatch partway from one who finished it and is recording that by hand, and the two call for opposite answers. The question SHALL therefore be put to the user, and SHALL default to **not** counting the rewatch — an abandoned pass is the situation that produces this transition, and a silent increment would inflate a figure the user reads as "times I have watched this".

Answering that it counts SHALL increase the rewatch count by one. Answering that it does not SHALL leave the rewatch count exactly as it was. Either answer SHALL complete the entry.

Choosing Completed SHALL still set episodes watched to the anime's total, as it does from any other status — the user has watched every episode, on the first viewing at least. Whether this pass counts as another one is the separate question being asked.

Together with "Watching a rewatch to the end counts it automatically", these SHALL be the only two places the rewatch count is increased without being typed. The one other untyped change to it is the `main-dashboard` capability's "A completion left in Currently watching can be undone from its card". That change only takes back an increase the first of these just made. The field SHALL remain independently editable, as "Rewatch count is independently editable" requires.

#### Scenario: Abandoning a rewatch partway

- **WHEN** I set a Rewatching entry at 6 of 24 episodes to Completed in the editor and answer that it should not count
- **THEN** the entry becomes Completed with 24 episodes watched and its rewatch count unchanged

#### Scenario: Recording a finished rewatch by hand

- **WHEN** I set a Rewatching entry to Completed in the editor and answer that it should count
- **THEN** the entry becomes Completed and its rewatch count increases by one

#### Scenario: The question is not asked from other statuses

- **WHEN** I set an entry whose status is Watching, On hold, Plan to watch, or Dropped to Completed
- **THEN** no rewatch question is asked and the rewatch count is unchanged, exactly as before

#### Scenario: The default does not count it

- **WHEN** the question is presented and I save without changing the answer
- **THEN** the rewatch count is unchanged

#### Scenario: Rewatch count stays editable

- **WHEN** I change the rewatch count by hand in the editor
- **THEN** the new value is saved, independently of either automatic path

### Requirement: An entry is Completed only while its progress covers everything available

The system SHALL treat Completed as a claim that everything available has been watched, and SHALL NOT leave an entry Completed once that stops being true.

Where an edit lowers a Completed entry's episodes watched below its anime's total, the system SHALL move that entry out of Completed, keeping the lowered count as entered. It SHALL land in **Rewatching** where that status is permitted for the anime — a count dropping on an anime already completed once means it is being watched again — and in **Watching** where it is not.

This is distinct from an entry ceasing to be Completed because *more* became available, which always sends it to Watching: the two cases differ in cause, and so in meaning.

- Progress dropped, and the anime has aired in full → **Rewatching** (already finished once; going through it again).
- Progress dropped on an entry that is Completed while its anime has not aired in full → **Watching**, since "Rewatching requires a finished anime the user has finished once" forbids Rewatching there. Because completion itself now requires the anime to have aired in full, this case is reachable only for an entry recorded under an earlier rule that the re-opening rule has not yet returned to Watching; it is kept so such an entry never lands in an impossible status.
- The published total grew past the progress → **Watching** (more of the show exists; behind on a first viewing).

The one exception is the `main-dashboard` capability's "A completion left in Currently watching can be undone from its card". On the main page, lowering the count of a card whose completion was never committed puts the entry back to its status before that completion rather than applying the rule above. For a completion reached from Watching, that is Watching even where Rewatching is permitted: the completion is being taken back, not followed by a rewatch.

#### Scenario: Lowering the count starts a rewatch

- **WHEN** I set a Completed entry for a 24-episode finished anime to 5 episodes watched
- **THEN** its status becomes Rewatching and its episodes watched is 5

#### Scenario: Lowering to zero starts a rewatch

- **WHEN** I set a Completed entry's episodes watched to 0 on a finished anime
- **THEN** its status becomes Rewatching with 0 episodes watched, rather than staying Completed

#### Scenario: Lowering the count on an anime not fully aired

- **WHEN** I lower the episodes watched of an entry still recorded as Completed while its anime has not aired in full
- **THEN** its status becomes Watching rather than Rewatching, because Rewatching is not permitted there

#### Scenario: A count at the full amount stays Completed

- **WHEN** I confirm a Completed entry's episodes watched at the value it already holds
- **THEN** its status is unchanged

#### Scenario: The two causes are distinguished

- **WHEN** a Completed entry stops covering everything available
- **THEN** it becomes Rewatching if its own progress dropped on an anime that has aired in full, and Watching if what is available grew instead

#### Scenario: The main page's undo is the exception

- **WHEN** on the main page I complete a Watching card, cancel the completion prompt, and then lower that card's count
- **THEN** the entry becomes Watching rather than Rewatching, per the `main-dashboard` capability

### Requirement: Completed-date lifecycle
The system SHALL set `completed_at` to today when an entry becomes Completed — whether by an explicit status change or by watching the final episode — and only when `completed_at` is currently empty. The system SHALL NOT clear or overwrite `completed_at` automatically when the status later changes away from Completed, mirroring MAL, which keeps the original finish date across rewatches; clearing a finish date SHALL be done through the editor's date fields.

The one exception is the `main-dashboard` capability's "A completion left in Currently watching can be undone from its card". That undo puts `completed_at` back to the value it had before the completion being undone. A date that completion filled in is cleared, and a date that was already there is kept.

#### Scenario: Marking completed
- **WHEN** I change an entry's status to Completed and it has no finish date
- **THEN** `completed_at` is set to today

#### Scenario: Existing finish date survives re-completion
- **WHEN** an entry that already has a `completed_at` becomes Completed again
- **THEN** the stored `completed_at` is left unchanged

#### Scenario: Un-completing keeps the finish date
- **WHEN** I change a Completed entry to any other status
- **THEN** `completed_at` is left as it was, and I can clear it myself in the editor's date fields

#### Scenario: The main page's undo puts the finish date back
- **WHEN** a completion from the main page fills in a finish date on an entry that had none, and I undo it there by lowering the card's count
- **THEN** `completed_at` is empty again

### Requirement: Rewatch count is independently editable

The system SHALL expose rewatch count as an editable field in the status editor, independent of watch status. The accepted range SHALL be 0 to 100 inclusive: a value above 100 or below 0 SHALL be rejected rather than stored, and the editor's field SHALL not allow one to be entered.

For an anime that has aired no episode, the accepted range SHALL instead be 0 alone, per "Nothing may be tracked against an anime that has aired no episode" — a rewatch count above 0 SHALL be rejected, and the editor's field SHALL not allow one to be entered.

The system SHALL additionally increase the rewatch count by one when a rewatch is watched to the end — see "Watching a rewatch to the end counts it automatically" — and when a rewatch ended early in the editor is confirmed as counting, per "Ending a rewatch early asks whether it counts". The `main-dashboard` capability's "A completion left in Currently watching can be undone from its card" SHALL take back exactly the increase that the undone completion made. Those SHALL be the only changes to this field the user does not type; a hand-entered value SHALL always be saved as entered.

#### Scenario: Editing rewatch count

- **WHEN** I change the rewatch count on any entry whose anime has aired at least one episode, regardless of its status
- **THEN** the new rewatch count is saved

#### Scenario: Rewatch count above the limit
- **WHEN** a rewatch count above 100 is submitted
- **THEN** it is rejected and the stored rewatch count is unchanged

#### Scenario: Editor caps the field
- **WHEN** I try to enter a rewatch count above 100 in the editor
- **THEN** the field does not accept the value

#### Scenario: Rewatch count on an anime that has aired nothing

- **WHEN** I try to enter a rewatch count above 0 for an anime that has aired no episode
- **THEN** the field does not accept the value, and a value above 0 submitted directly is rejected

#### Scenario: A finished rewatch adds one

- **WHEN** a Rewatching entry reaches everything available
- **THEN** its rewatch count is one higher than before

#### Scenario: The main page's undo takes the increase back

- **WHEN** a rewatch completed from the main page raised the rewatch count from 1 to 2, and I undo that completion there by lowering the card's count
- **THEN** the rewatch count is 1 again

### Requirement: Score prompt on completion via the increment button
The system SHALL open a score prompt overlay whenever an episode-count change made from the progress row takes an entry into Completed status — whether from the "+" button or from editing the count in place — from every place that row appears (the main dashboard's currently-watching carousel, my list rows, the anime detail page, and any later addition), except where "Completion prompt fires only on entering Completed" rules it out. The overlay SHALL show the anime's picture on the left and a score dropdown offering "No score" and the values 1 through 10, pre-selected with the entry's current score.

The overlay SHALL offer three actions: **cancel**, save, and **save and rank**. Cancel skips scoring only. The entry is already Completed by the time the prompt opens, and Cancel SHALL NOT undo that. Save and rank SHALL save the chosen score and then open the ranking editor focused on that anime, so an anime can be finished, scored, and placed in one pass. It SHALL be offered only while the chosen score would leave the entry hand-orderable — with "No score" selected, or for a dropped or short-form anime, saving alone is the only save action offered.

Every completion reachable from the progress row now concerns an anime that has aired in full and carries a finish date, so no finish-date condition is placed on the prompt. In particular the prompt SHALL open for the final episode of a run MyAnimeList still reports as currently airing, since that is a genuine finish. Reaching the aired-so-far count of an anime with episodes still to come completes nothing and SHALL NOT open this prompt.

#### Scenario: Final episode watched from any increment site
- **WHEN** I press the "+" button on an entry whose episodes-watched thereby reaches the anime's total episode count
- **THEN** a score prompt overlay opens showing that anime's picture and a score dropdown

#### Scenario: Final episode of a run with a stale airing status
- **WHEN** I press the "+" button to reach the total of an anime whose episodes have all aired but which MyAnimeList still reports as currently airing
- **THEN** the score prompt opens, because the entry entered Completed

#### Scenario: Count typed straight to the total
- **WHEN** I edit an entry's count in place and set it to the anime's total episode count
- **THEN** the same score prompt overlay opens, just as it would from the "+" button

#### Scenario: Prompt reflects an existing score
- **WHEN** the score prompt opens for an entry that already has a score
- **THEN** the dropdown is pre-selected with that score rather than "No score"

#### Scenario: The prompt offers to rank
- **WHEN** I pick a score of 1 through 10 in the completion prompt
- **THEN** a save-and-rank action is offered alongside save and cancel

#### Scenario: No rank action without a score
- **WHEN** "No score" is selected in the completion prompt
- **THEN** no save-and-rank action is offered

#### Scenario: The dismiss action reads Cancel
- **WHEN** the completion prompt opens
- **THEN** its action for closing without saving is labelled "Cancel"

#### Scenario: Non-final increments do not prompt
- **WHEN** I press the "+" button and episodes-watched stays below the anime's total episode count
- **THEN** no score prompt opens

#### Scenario: Catching up on a run still to come does not prompt
- **WHEN** I press the "+" button and episodes-watched thereby reaches the aired-so-far count of an anime whose total is higher
- **THEN** the entry stays Watching and no score prompt opens

### Requirement: Completion prompt fires only on entering Completed

The system SHALL open the completion score prompt only on the transition into Completed status. It SHALL NOT open it:

- for an entry that was already Completed before the episode-count change;
- for an anime whose total episode count is unknown;
- for an anime that has not aired in full (where no episode-count change completes anything);
- when a Rewatching entry that **already carries a score** finishes and returns to Completed;
- when the system itself completes a caught-up entry because its full run became known.

The rewatch exclusion is separate from the others and narrower than them. A rewatch's completion is a real transition into Completed, so it needs its own carve-out, and the carve-out covers only a rewatch that already has a score. That score has been carried since an earlier viewing, so re-asking for one at the end of every rewatch would be noise rather than a decision; the score stays editable in the editor. A rewatch with no score has no such score to protect. It SHALL get the same prompt a first completion gets, with the same actions.

#### Scenario: Rewatch increment on a completed entry
- **WHEN** I press the "+" button on an entry that is already Completed
- **THEN** no score prompt opens

#### Scenario: Re-entering the same total on a completed entry
- **WHEN** I edit the count in place on an already-Completed entry and confirm a value equal to the total
- **THEN** no score prompt opens, because the entry was already Completed

#### Scenario: Unknown total episode count
- **WHEN** I press the "+" button on an anime with an unknown total episode count
- **THEN** no score prompt opens, because the entry does not auto-complete

#### Scenario: Reaching what's aired on a run still to come does not prompt
- **WHEN** I press the "+" button and episodes-watched reaches the aired-so-far count of an anime with more episodes to come
- **THEN** no score prompt opens, because the entry does not enter Completed

#### Scenario: A scored rewatch finishing does not prompt
- **WHEN** a Rewatching entry that has a score reaches everything available and returns to Completed
- **THEN** no score prompt opens, and the entry's existing score is unchanged

#### Scenario: An unscored rewatch finishing prompts
- **WHEN** a Rewatching entry with no score reaches everything available and returns to Completed
- **THEN** the score prompt opens with "No score" pre-selected, offering the same actions as for a first completion

#### Scenario: A system completion does not prompt
- **WHEN** a caught-up entry is completed because its total became known while the page was open
- **THEN** no score prompt opens

### Requirement: Saving or dismissing the completion score prompt
The system SHALL save a chosen score through the same entry-edit path as any other score change, so it is logged as activity, queued for MAL sync, and placed in the ranking. Dismissing the prompt — via the Cancel action, Esc, or a click outside the overlay — SHALL close it and leave the entry's score unchanged. Neither path SHALL undo the completion itself.

Save SHALL count as saving even when the score confirmed is the one the prompt pre-selected, "No score" included. The entry keeps that score. Where nothing changed, nothing is logged, queued for sync, or placed in the ranking. The prompt closes exactly as it does after any other save. In particular Save SHALL NOT be treated as a dismissal because the score was left as it was.

Save and rank SHALL save by that identical path and then open the ranking editor focused on the anime, on the score just saved. If the save fails, the ranking editor SHALL NOT open and the prompt SHALL behave exactly as a failed save does.

#### Scenario: Score given
- **WHEN** I pick a score in the completion prompt and confirm
- **THEN** the score is saved on the entry, logged as activity, queued for MAL sync, and the prompt closes

#### Scenario: Saving the pre-selected score
- **WHEN** I press Save in the completion prompt without changing the pre-selected score
- **THEN** the prompt closes as a save, the entry keeps that score, and no activity row is written for the score

#### Scenario: Score given and ranked
- **WHEN** I pick a score in the completion prompt and choose save and rank
- **THEN** the score is saved exactly as a plain save would save it, and the ranking editor opens focused on that anime, on that score

#### Scenario: Prompt dismissed without a score
- **WHEN** I dismiss the completion prompt with Cancel, Esc, or a click outside
- **THEN** the prompt closes, the entry's score is unchanged, and the entry stays Completed

#### Scenario: Score save fails
- **WHEN** saving the chosen score fails
- **THEN** the prompt reports the failure and stays open so I can retry or dismiss it

#### Scenario: Save and rank when the save fails
- **WHEN** I choose save and rank and the save fails
- **THEN** the prompt reports the failure and stays open, and no ranking editor opens

### Requirement: Triggering view refreshes after the completion prompt closes
The system SHALL update the view that triggered the completion prompt once the prompt closes **with a score saved**, by save or by save and rank, so the newly completed anime is reflected without a manual page reload:

- The main dashboard SHALL reload its data, so the anime disappears from the currently-watching carousel. Whether an entry belongs there is decided server-side, and the saved entry doesn't describe it.
- My list and the anime detail page SHALL apply the saved entry the prompt hands back, without re-reading, so the entry reads as Completed with the score given. My list places the row by that entry, the same way its own score control and the entry editor do.

Save and rank SHALL count as the prompt closing: the update SHALL happen when the prompt closes rather than waiting for the ranking editor opened on top of it.

Closing the prompt without saving — Cancel, Esc, or a click outside — SHALL NOT refresh the view. A completion that opens no prompt, such as a rewatch that already has a score finishing, SHALL NOT refresh it either. In both cases the entry is already Completed, and each view shows what its own edit response tells it:

- My list and the anime detail page show the entry as Completed.
- The main dashboard keeps the card where it sits at its full count, per the `main-dashboard` capability's "A completion left in Currently watching can be undone from its card".

#### Scenario: Completing from the dashboard carousel
- **WHEN** I complete an anime with the "+" button in the currently-watching carousel and save a score in the prompt
- **THEN** the dashboard reloads and that anime is no longer in the currently-watching carousel

#### Scenario: Completing from my list
- **WHEN** I complete an anime with the "+" button in my list and save a score in the prompt
- **THEN** that entry shows as Completed, with the score I gave, and my list is not re-read

#### Scenario: Completing from the anime detail page
- **WHEN** I complete an anime with the "+" button on its detail page and save a score in the prompt
- **THEN** the detail page shows the entry as Completed, with the score I gave

#### Scenario: Refresh is not held up by the ranking editor
- **WHEN** I choose save and rank from the currently-watching carousel
- **THEN** the dashboard reloads behind the ranking editor rather than waiting for me to close it

#### Scenario: My list is updated before the ranking editor closes
- **WHEN** I choose save and rank from my list
- **THEN** the row already shows the entry as Completed with the saved score behind the ranking editor, and my list is not re-read

#### Scenario: Cancelling refreshes nothing
- **WHEN** I complete an anime from the carousel, my list, or its detail page and close the prompt with Cancel, Esc, or a click outside
- **THEN** the view that triggered the prompt is not re-read, and the entry is Completed

#### Scenario: A silent rewatch completion refreshes nothing
- **WHEN** a Rewatching entry that already has a score reaches its total from any progress row
- **THEN** no prompt opens, the view is not re-read, and the row's count shows the total
