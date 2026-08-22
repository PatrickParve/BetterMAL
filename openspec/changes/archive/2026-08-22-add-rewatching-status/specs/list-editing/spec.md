## ADDED Requirements

### Requirement: Rewatching is a watch status

The system SHALL offer **Rewatching** as a watch status alongside Watching, Completed, On hold, Plan to watch, and Dropped. It SHALL be stored, logged, and synced like any other status, and SHALL be displayed like any other status — see "Rewatching is shown wherever a watch status is shown".

Rewatching means the user has finished the anime at least once and is going through it again. It SHALL be reachable by choosing it in the entry editor, subject to "Rewatching requires a finished anime the user has finished once", and by the automatic transition in "An entry is Completed only while its progress covers everything available".

#### Scenario: Choosing Rewatching

- **WHEN** I set an eligible entry's status to Rewatching in the editor
- **THEN** the status is saved, written to the activity log, and queued for MyAnimeList sync like any other status change

### Requirement: Rewatching requires a finished anime the user has finished once

The system SHALL permit an entry's status to be set to Rewatching only when **both** of the following hold, and SHALL reject the edit otherwise, leaving the entry unchanged and reporting why:

- **The anime has finished airing.** Its airing status is `finished_airing`, or is not recorded at all. An anime that is `currently_airing` or `not_yet_aired` SHALL be refused — there is no complete run to go through again.
- **The user has finished the anime at least once.** This SHALL be tested against the entry's history rather than its present status, and SHALL be satisfied by **any** of: a finish date on the entry, a rewatch count above zero, or a current status of Completed.

Because the second condition looks at history, an entry that has been completed before SHALL be settable to Rewatching from **any** status it currently holds — Watching, On hold, Plan to watch, Dropped, or Completed. A rewatch abandoned partway and parked elsewhere SHALL therefore be resumable without first returning the entry to Completed.

An entry whose status is already Rewatching SHALL also satisfy this rule, so that the editor can present the entry's own current status as its selected value rather than as an unavailable one.

The entry editor SHALL present the limit rather than letting a rejected save be attempted: where the rule does not permit it, the Rewatching option SHALL be unavailable in the status dropdown.

#### Scenario: Rewatching a completed, finished anime

- **WHEN** I open the editor for a Completed entry whose anime has finished airing
- **THEN** Rewatching is offered and selecting it is saved

#### Scenario: Resuming a rewatch from another status

- **WHEN** I open the editor for an entry that has a finish date but whose current status is Watching, On hold, or Dropped
- **THEN** Rewatching is offered and selecting it is saved, without the entry having to pass through Completed first

#### Scenario: A rewatch count is enough on its own

- **WHEN** I open the editor for an entry with a rewatch count above zero whose finish date has been cleared
- **THEN** Rewatching is offered, because a rewatch count can only have been earned by finishing the anime

#### Scenario: Not offered for an unfinished anime

- **WHEN** I open the editor for a Completed entry whose anime is currently airing
- **THEN** Rewatching is unavailable, and an edit setting it directly is rejected with the entry left unchanged

#### Scenario: Not offered for an anime never finished

- **WHEN** I open the editor for an entry with no finish date, a rewatch count of zero, and a status other than Completed
- **THEN** Rewatching is unavailable, and an edit setting it directly is rejected with the entry left unchanged

#### Scenario: An anime with no recorded airing status

- **WHEN** I open the editor for a Completed entry whose anime has no recorded airing status
- **THEN** Rewatching is offered, consistent with such an anime being treated as finished when it was completed

#### Scenario: An entry already rewatching keeps its own status

- **WHEN** I open the editor for an entry whose status is already Rewatching
- **THEN** Rewatching is shown as its selected status and is not presented as unavailable

#### Scenario: Restoring a finish date restores eligibility

- **WHEN** an entry that lost every record of having been finished has its finish date set again in the editor
- **THEN** Rewatching becomes available for it once more

### Requirement: Rewatching is shown wherever a watch status is shown

The system SHALL display Rewatching in every place a watch status is displayed, with its own label and its own colour, rather than being rendered as Watching, as Completed, or as a missing value. This covers my-list rows, the anime detail page's status text, series-page entry rows, the entry editor, and any other surface that names an entry's status.

#### Scenario: The status reads as itself

- **WHEN** an entry whose status is Rewatching is shown on any surface that names a watch status
- **THEN** it reads as Rewatching, in the Rewatching colour, rather than as any other status

#### Scenario: No surface is left out

- **WHEN** I look at a Rewatching entry on my list, on its detail page, and on its series page
- **THEN** all three name its status as Rewatching

### Requirement: Entering Rewatching restarts progress

The system SHALL set an entry's episodes watched to 0 when its status becomes Rewatching, so the new run is tracked from the beginning — the same way entering Completed fills the count to what has been watched.

The entry's start date, finish date, score, and rewatch count SHALL be left as they are. In particular the finish date SHALL NOT be cleared, consistent with the existing rule that a finish date is never cleared automatically.

#### Scenario: Progress restarts

- **WHEN** I set a Completed entry with 24 episodes watched to Rewatching
- **THEN** its episodes watched becomes 0 and its status becomes Rewatching

#### Scenario: Dates and score survive

- **WHEN** an entry becomes Rewatching
- **THEN** its start date, finish date, score, and rewatch count are unchanged

### Requirement: Watching a rewatch to the end counts it automatically

The system SHALL return a Rewatching entry to Completed when its episodes watched reaches the anime's total episode count — whether by the "+" control or by an in-place count edit — and SHALL increase the entry's rewatch count by one at the same moment, without asking. The target is always the total: a Rewatching entry is always on an anime that has finished airing, per "Rewatching requires a finished anime the user has finished once", so there is no aired-so-far case here.

The completion-score prompt SHALL NOT open on this transition. The prompt exists for settling a score on a first viewing, and the entry already carries one; the score SHALL remain editable in the editor as usual.

The entry's finish date SHALL NOT be overwritten with the rewatch's finish date, per the existing never-overwrite rule.

#### Scenario: Rewatch watched to the end

- **WHEN** a Rewatching entry for a 24-episode finished anime reaches 24 episodes watched
- **THEN** its status becomes Completed and its rewatch count increases by one, with nothing asked

#### Scenario: No score prompt on re-completion

- **WHEN** a rewatch reaches the last episode
- **THEN** no completion-score prompt opens, and the entry's existing score is unchanged

#### Scenario: The original finish date is kept

- **WHEN** a rewatch re-completes an entry that already has a finish date
- **THEN** that finish date is left exactly as it was

### Requirement: Ending a rewatch early asks whether it counts

Where a Rewatching entry is returned to Completed by **choosing Completed in the entry editor**, rather than by its episodes watched reaching the total, the system SHALL ask whether the rewatch should be counted, and SHALL NOT decide on its own.

The system cannot distinguish a user who abandoned a rewatch partway from one who finished it and is recording that by hand, and the two call for opposite answers. The question SHALL therefore be put to the user, and SHALL default to **not** counting the rewatch — an abandoned pass is the situation that produces this transition, and a silent increment would inflate a figure the user reads as "times I have watched this".

Answering that it counts SHALL increase the rewatch count by one. Answering that it does not SHALL leave the rewatch count exactly as it was. Either answer SHALL complete the entry.

Choosing Completed SHALL still set episodes watched to the anime's total, as it does from any other status — the user has watched every episode, on the first viewing at least. Whether this pass counts as another one is the separate question being asked.

Together with "Watching a rewatch to the end counts it automatically", these SHALL be the only two places the rewatch count changes without being typed; the field SHALL remain independently editable, as "Rewatch count is independently editable" requires.

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

Where an edit lowers a Completed entry's episodes watched below what is available, the system SHALL move that entry out of Completed, keeping the lowered count as entered. It SHALL land in **Rewatching** where that status is permitted for the anime — a count dropping on a finished anime already completed once means it is being watched again — and in **Watching** where it is not.

This is distinct from an entry ceasing to be Completed because *more* became available, which always sends it to Watching: the two cases differ in cause, and so in meaning.

- Progress dropped, and the anime has finished airing → **Rewatching** (already finished once; going through it again).
- Progress dropped, but the anime is still airing → **Watching**, since "Rewatching requires a finished anime the user has finished once" forbids Rewatching there.
- What is available grew past the progress → **Watching** (new episodes exist; behind on a first viewing).

#### Scenario: Lowering the count starts a rewatch

- **WHEN** I set a Completed entry for a 24-episode finished anime to 5 episodes watched
- **THEN** its status becomes Rewatching and its episodes watched is 5

#### Scenario: Lowering to zero starts a rewatch

- **WHEN** I set a Completed entry's episodes watched to 0 on a finished anime
- **THEN** its status becomes Rewatching with 0 episodes watched, rather than staying Completed

#### Scenario: Lowering the count on a still-airing anime

- **WHEN** I lower the episodes watched of a Completed entry whose anime is currently airing
- **THEN** its status becomes Watching rather than Rewatching, because Rewatching is not permitted for an anime that has not finished airing

#### Scenario: A count at the full amount stays Completed

- **WHEN** I confirm a Completed entry's episodes watched at the value it already holds
- **THEN** its status is unchanged

#### Scenario: The two causes are distinguished

- **WHEN** a Completed entry stops covering everything available
- **THEN** it becomes Rewatching if its own progress dropped on a finished anime, and Watching if what is available grew instead

### Requirement: A rewatch in progress behaves like watching

The system SHALL treat a Rewatching entry the way it treats a Watching one wherever an entry's being in progress is what matters: it SHALL show the editable progress row with its "+" control and in-place editable count, and SHALL be incrementable exactly as a Watching entry is.

#### Scenario: The progress row works as usual

- **WHEN** a Rewatching entry is shown anywhere the progress row appears
- **THEN** its bar, count, and "+" control render and behave exactly as they do for a Watching entry

#### Scenario: Incrementing a rewatch

- **WHEN** I press "+" on a Rewatching entry below the last available episode
- **THEN** its episodes watched increases by one and the started-date, activity-log, and sync rules apply as usual

## MODIFIED Requirements

### Requirement: Rewatch count is independently editable

The system SHALL expose rewatch count as an editable field in the status editor, independent of watch status. The accepted range SHALL be 0 to 100 inclusive: a value above 100 or below 0 SHALL be rejected rather than stored, and the editor's field SHALL not allow one to be entered.

The system SHALL additionally increase the rewatch count by one when a rewatch is watched to the end — see "Watching a rewatch to the end counts it automatically" — and when a rewatch ended early in the editor is confirmed as counting, per "Ending a rewatch early asks whether it counts". Those SHALL be the only changes to this field the user does not type; a hand-entered value SHALL always be saved as entered.

#### Scenario: Editing rewatch count

- **WHEN** I change the rewatch count on any entry regardless of its status
- **THEN** the new rewatch count is saved

#### Scenario: Rewatch count above the limit

- **WHEN** a rewatch count above 100 is submitted
- **THEN** it is rejected and the stored rewatch count is unchanged

#### Scenario: Editor caps the field

- **WHEN** I try to enter a rewatch count above 100 in the editor
- **THEN** the field does not accept the value

#### Scenario: A finished rewatch adds one

- **WHEN** a Rewatching entry reaches everything available
- **THEN** its rewatch count is one higher than before

### Requirement: Completion prompt fires only on entering Completed

The system SHALL open the completion score prompt only on the transition into Completed status, and SHALL NOT open it for an entry that was already Completed before the episode-count change, nor for an anime whose total episode count is unknown, nor when a Rewatching entry finishes and returns to Completed.

The rewatch exclusion is the substantive addition. A rewatch ends with a score the entry has carried since its first viewing, so re-asking for one at the end of every rewatch would be noise rather than a decision; the score stays editable in the editor.

#### Scenario: Rewatch finishing does not prompt

- **WHEN** a Rewatching entry reaches everything available and returns to Completed
- **THEN** no score prompt opens, and the entry's existing score is unchanged

#### Scenario: Re-entering the same total on a completed entry

- **WHEN** I edit the count in place on an already-Completed entry and confirm a value equal to the total
- **THEN** no score prompt opens, because the entry was already Completed

#### Scenario: Unknown total episode count

- **WHEN** I press the "+" button on an anime with an unknown total episode count
- **THEN** no score prompt opens, because the entry does not auto-complete
