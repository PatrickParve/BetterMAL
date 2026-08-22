# list-editing Specification

## Purpose
TBD - created by archiving change bootstrap-anime-tracker. Update Purpose after archive.
## Requirements
### Requirement: Whether an anime has aired an episode is resolved one way

The system SHALL resolve, for any anime, a single yes/no answer to whether at least one of its episodes has aired, and SHALL use that same answer everywhere an editing rule or a display rule depends on it.

The answer SHALL be derived as follows. When the anime's aired-so-far episode count is known (see the `episode-airing-data` capability), the answer SHALL be yes exactly when that count is one or more. When the aired-so-far count is unknown, the answer SHALL be taken from the anime's MyAnimeList airing status: no when the status is "not yet aired", and yes for every other status — including an anime whose airing status is not recorded at all, since nothing then establishes that no episode has aired.

The system SHALL NOT treat an unknown aired-so-far count as zero, and SHALL NOT project an aired count from a start date or a broadcast cadence.

#### Scenario: A stored aired episode settles it

- **WHEN** an anime has a stored airing row whose air instant has passed
- **THEN** it counts as having aired an episode, whatever its MyAnimeList airing status says

#### Scenario: Airing status decides when no count is known

- **WHEN** an anime has finished airing but has no stored airing rows, so its aired-so-far count is unknown
- **THEN** it counts as having aired an episode

#### Scenario: Not yet aired with nothing stored

- **WHEN** an anime's airing status is "not yet aired" and its aired-so-far count is unknown
- **THEN** it counts as having aired no episode

#### Scenario: Unrecorded airing status is permissive

- **WHEN** an anime has no recorded airing status and no stored airing rows
- **THEN** it counts as having aired an episode, rather than being treated as unaired

### Requirement: Nothing may be tracked against an anime that has aired no episode

For an anime that has aired no episode, the system SHALL reject every edit that would record having watched, rated, or settled it, whichever surface the edit is made from — the progress row, the entry editor, or the API directly:

- Episodes watched SHALL NOT be set above 0. The ceiling for episodes watched on such an anime is 0, not its published total.
- The entry's status SHALL be limited to Watching and Plan to watch. Completed, Dropped, On hold, and Rewatching SHALL be rejected.
- A score of 1 through 10 SHALL be rejected.
- A rewatch count above 0 SHALL be rejected.

Rewatching's own eligibility rule ("Rewatching requires a finished anime the user has finished once") already refuses it for any anime that has not finished airing, which every anime with nothing aired satisfies trivially — an anime nobody has ever been able to watch cannot have been watched once, whatever the entry's history shows. Listing it among the rejected statuses here states that plainly rather than leaving it to be derived from the two rules' overlap, and composes with — rather than overrides — Rewatching's own gating.

A rejected edit SHALL leave the entry entirely unchanged and SHALL report why, rather than failing silently or partially applying the request.

Clearing SHALL always remain possible: setting the score to "no score", setting the rewatch count to 0, and setting episodes watched to 0 SHALL be accepted on such an anime whatever the entry currently holds, so an entry that already carries a disallowed value — set before these rules existed, or arriving from MyAnimeList — can always be walked back.

The entry editor SHALL present these limits rather than letting a rejected save be attempted: the Completed, Dropped, On hold, and Rewatching options SHALL be unavailable in the status dropdown, the score control SHALL offer only "no score", and the rewatch-count field SHALL not accept a value above 0.

#### Scenario: Episodes watched blocked

- **WHEN** I try to set episodes watched above 0 on an anime that has aired no episode
- **THEN** the edit is rejected and the entry's episodes watched is unchanged

#### Scenario: Completed, Dropped, On hold, and Rewatching blocked

- **WHEN** I try to set the status of an entry to Completed, Dropped, On hold, or Rewatching on an anime that has aired no episode
- **THEN** the edit is rejected and the entry's status is unchanged

#### Scenario: Watching and Plan to watch stay available

- **WHEN** I set the status to Watching or Plan to watch on an anime that has aired no episode
- **THEN** the change is saved as normal

#### Scenario: Score blocked

- **WHEN** I try to give a score of 1 through 10 to an anime that has aired no episode
- **THEN** the edit is rejected and the entry's score is unchanged

#### Scenario: Rewatch count blocked

- **WHEN** I try to set a rewatch count above 0 on an anime that has aired no episode
- **THEN** the edit is rejected and the entry's rewatch count is unchanged

#### Scenario: An existing score can still be cleared

- **WHEN** an entry for an anime that has aired no episode already carries a score and I set that score to "no score"
- **THEN** the change is saved, because clearing is always permitted

#### Scenario: An existing rewatch count can still be cleared

- **WHEN** an entry for an anime that has aired no episode already carries a rewatch count above 0 and I set it to 0
- **THEN** the change is saved

#### Scenario: The editor offers only what is allowed

- **WHEN** I open the entry editor for an anime that has aired no episode
- **THEN** its status dropdown offers only Watching and Plan to watch as selectable — with Rewatching unavailable there just as it is for any other anime the entry has never finished — its score control offers only "no score", and its rewatch-count field will not take a value above 0

#### Scenario: A rejected edit explains itself

- **WHEN** an edit is rejected by any of these rules
- **THEN** the rejection names the reason rather than failing without explanation

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

The system SHALL display an unknown total as `watched/?` and SHALL allow an entry to be marked Completed only when the system can establish how many episodes there are to have watched:

- For an anime that has **finished airing**, Completed SHALL require a known total episode count. Marking Completed SHALL set episodes watched to that total, mirroring MyAnimeList's own UI.
- For an anime that is **currently airing**, Completed means having watched every episode out so far. It SHALL require a known aired-so-far episode count, and marking Completed SHALL set episodes watched to that aired-so-far count rather than to the eventual total.
- For an anime that has **aired no episode**, Completed SHALL be rejected outright, per "Nothing may be tracked against an anime that has aired no episode".

An anime whose airing status is not recorded SHALL be treated as the finished-airing case above, since its total episode count is the only figure available.

Where the required figure is unknown, the system SHALL prevent the completion and the entry editor SHALL present Completed as unavailable.

Completing a **currently-airing** anime SHALL NOT set a finish date — the anime has not actually finished, so recording one would be false, and the automatic re-opening described by "A completed entry re-opens when a new episode airs" would otherwise leave a stale finish date on an entry that has since returned to Watching. The existing never-overwrite rule continues to apply once a finish date is eligible to be set: the first time an entry is completed while its anime reads as having **finished** airing, its finish date SHALL be filled in as normal, and every completion after that SHALL leave an already-set finish date untouched.

This requirement makes Completed reachable for a currently-airing anime, once its aired-so-far count is known — a case the "An entry is Completed only while its progress covers everything available" requirement already accounts for: where such an entry's episodes watched is later lowered below that aired-so-far count, it lands in **Watching**, not Rewatching, because Rewatching requires a finished anime and this one has not finished airing. That is the fallback case that requirement describes for exactly this reason — it exists only because Completed is reachable here on an anime that has not finished airing.

Automatic completion from the progress row mirrors the same fill target: for a **currently-airing** anime it fires when an episode-count change takes episodes watched to the **known aired-so-far count**, exactly as an explicit Completed edit would; for a **finished** anime it continues to require the **known total**. A currently-airing anime with an unknown aired-so-far count simply does not auto-complete (silently, unlike the explicit edit above, which is refused with a reason) — the same way it already didn't when the total was unknown.

#### Scenario: Blocking completion when total unknown

- **WHEN** an anime has finished airing with an unknown total episode count and I attempt to mark it Completed
- **THEN** the system prevents the completion

#### Scenario: Displaying unknown total

- **WHEN** an anime with an unknown total is displayed with progress
- **THEN** progress is shown as `watched/?`

#### Scenario: Completing a finished anime fills to the total

- **WHEN** I mark an entry Completed for an anime that has finished airing with a total of 24 episodes
- **THEN** the entry becomes Completed with 24 episodes watched

#### Scenario: Completing an airing anime fills to what has aired

- **WHEN** I mark an entry Completed for a currently-airing anime with 7 of an eventual 12 episodes aired
- **THEN** the entry becomes Completed with 7 episodes watched, not 12

#### Scenario: Blocking completion when the aired count is unknown

- **WHEN** an anime is currently airing, its aired-so-far count is unknown, and I attempt to mark it Completed
- **THEN** the system prevents the completion

#### Scenario: Watching the latest aired episode completes, without a score prompt

- **WHEN** I use the "+" control to reach the last episode aired so far of a currently-airing anime whose total is higher
- **THEN** the entry becomes Completed with no finish date set, and no completion-score prompt opens, because the anime hasn't actually finished

#### Scenario: A count drop on a caught-up currently-airing anime returns to Watching

- **WHEN** an entry is Completed at the aired-so-far count for a currently-airing anime and I lower episodes watched below that count
- **THEN** the entry returns to Watching, never Rewatching, per the same fallback described above

#### Scenario: No known aired count means no auto-completion

- **WHEN** I use the "+" control on a currently-airing anime whose aired-so-far count is unknown
- **THEN** the entry stays Watching, unlike an explicit Completed edit, which would instead be refused with a reason

#### Scenario: Completing a currently-airing anime sets no finish date

- **WHEN** I mark an entry Completed for a currently-airing anime with a known aired-so-far count
- **THEN** the entry becomes Completed and its finish date is left unset

#### Scenario: A finish date is filled in once the anime has actually finished

- **WHEN** an entry with no finish date is completed while its anime reads as having finished airing
- **THEN** its finish date is set as normal, exactly as for any other completion of a finished anime

### Requirement: Rewatch count is independently editable

The system SHALL expose rewatch count as an editable field in the status editor, independent of watch status. The accepted range SHALL be 0 to 100 inclusive: a value above 100 or below 0 SHALL be rejected rather than stored, and the editor's field SHALL not allow one to be entered.

For an anime that has aired no episode, the accepted range SHALL instead be 0 alone, per "Nothing may be tracked against an anime that has aired no episode" — a rewatch count above 0 SHALL be rejected, and the editor's field SHALL not allow one to be entered.

The system SHALL additionally increase the rewatch count by one when a rewatch is watched to the end — see "Watching a rewatch to the end counts it automatically" — and when a rewatch ended early in the editor is confirmed as counting, per "Ending a rewatch early asks whether it counts". Those SHALL be the only changes to this field the user does not type; a hand-entered value SHALL always be saved as entered.

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

### Requirement: Overlays open independently of the page behind them
The system SHALL open and close the entry editor overlay and the completion-score prompt without redrawing the page underneath, so the cost of showing an overlay does not grow with how much that page is displaying. Opening the editor from a row of a list holding hundreds or thousands of entries SHALL be as immediate as opening it from a page showing a single anime — there SHALL be no perceptible delay between using an edit action and the overlay appearing.

Mounting an overlay SHALL NOT cause any page that merely holds a reference to the "open editor" or "increment episodes" action to re-render, since neither the page's data nor its own state has changed.

#### Scenario: Opening the editor on a large list
- **WHEN** I use the edit button on a my-list row while the list is showing a large library
- **THEN** the editor overlay appears immediately, with no perceptible delay, and no more slowly than it does on the anime detail page

#### Scenario: Closing the editor on a large list
- **WHEN** I close the editor overlay by saving, cancelling, or pressing Escape over a large list
- **THEN** the overlay disappears immediately and the page behind it is not redrawn

#### Scenario: Completion prompt behaves the same way
- **WHEN** an episode increment on a large list takes an entry into Completed and the score prompt opens
- **THEN** the prompt appears immediately, with the same independence from the size of the list behind it

#### Scenario: Pages are not re-rendered by an overlay opening
- **WHEN** an overlay opens or closes over any page that can open it
- **THEN** that page's own rendering is not re-run, because none of its data or state changed

### Requirement: An entry edit updates only the row it belongs to
The system SHALL confine the visible update from editing one entry in a list to that entry's own row. Changing a row's score, editing its episode count in place, or incrementing it — including the in-flight disabled state while the save is running — SHALL redraw that row alone, leaving every other row untouched, so a list stays responsive while entries are edited one after another.

#### Scenario: Changing one row's score
- **WHEN** I change the score on a my-list row
- **THEN** only that row updates, and the rest of the list is not redrawn

#### Scenario: In-flight state is row-scoped
- **WHEN** a row's edit is in flight and its controls are disabled
- **THEN** only that row reflects the pending state, and no other row is redrawn

#### Scenario: Repeated edits stay responsive
- **WHEN** I edit several entries in succession on a large list
- **THEN** each edit responds immediately, with no slowdown from the number of entries on the page

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

### Requirement: The editable progress row appears only once an episode has aired

Wherever the system draws an entry's editable progress row — the shared `watched/total` bar together with its in-place editable count and its "+" control — that row SHALL be absent entirely for an anime that has aired no episode, rather than shown in a disabled or zeroed state. This covers every surface that draws it: my list rows, the main dashboard's currently-watching cards, and the anime detail page.

The row SHALL reappear on all of those surfaces as soon as the anime has aired an episode, with no further action needed from the user.

Read-only aggregate progress bars are NOT covered by this requirement: the profile page's whole-list episode progress, the series page's series-wide progress, and the main dashboard's `aired/total` followed-shows-airing bars edit no entry and SHALL be unaffected.

#### Scenario: No progress row before the first episode

- **WHEN** an entry's anime has aired no episode
- **THEN** no progress bar, episode count, or "+" control is shown for it on my list, the dashboard's currently-watching cards, or the anime detail page

#### Scenario: The row returns with the first episode

- **WHEN** an anime that had aired no episode airs its first
- **THEN** the progress bar, count, and "+" control are shown for it again on every one of those surfaces

#### Scenario: Aggregate bars are untouched

- **WHEN** my list contains an anime that has aired no episode
- **THEN** the profile page's whole-list progress bar and the series page's series-wide progress bar render exactly as they did before

### Requirement: A completed entry re-opens when a new episode airs

The system SHALL return a Completed entry to Watching when its anime is currently airing and the number of episodes aired so far has grown beyond the entry's episodes watched — the entry is no longer finished, because more of the show now exists than has been seen.

An episode counts as aired the moment its stored air instant passes, without any refresh, poll, or user action (see the `episode-airing-data` capability). The re-opening SHALL follow from that directly rather than waiting on a scheduled pass: an entry SHALL already read as Watching the first time any surface displaying it is read after the episode's air instant has passed — my list, the main dashboard, and the anime detail page alike.

The system SHALL additionally evaluate the same rule on the recurring airing schedule, so that an entry the user never looks at still has its re-opening recorded and synced.

Either path SHALL treat it as an ordinary status change: written to the activity log and queued for MyAnimeList sync like any other. Applying the rule more than once SHALL have no further effect, since its condition no longer holds once applied.

The entry's episodes watched SHALL NOT be altered, and its finish date SHALL NOT be cleared — the existing rule that a finish date is never overwritten or cleared automatically continues to apply.

Only an anime that is currently airing SHALL be re-opened this way. An entry for an anime that has finished airing SHALL stay Completed, even where its aired-so-far count reads higher than its cached total episode count.

The re-opened status SHALL always be Watching, never Rewatching, and this holds without exception: "Rewatching requires a finished anime the user has finished once" permits Rewatching only for an anime that has finished airing, and this rule fires only while the anime is still currently airing — the two conditions cannot hold at once, so Rewatching is never a candidate here, whatever the entry's rewatch history shows.

#### Scenario: New episode re-opens a completed entry

- **WHEN** an entry is Completed at 12 episodes watched, its anime is currently airing, and episode 13 airs
- **THEN** the entry's status becomes Watching, the change is logged and queued for sync, and its episodes watched stays at 12

#### Scenario: The change is visible on the next look, not on the next scheduled pass

- **WHEN** episode 13's stored air instant passes and I then open my list, the main dashboard, or that anime's detail page
- **THEN** the entry already reads as Watching, without waiting for a refresh or a scheduled pass to have run

#### Scenario: A re-opened show reaches the dashboard's currently-watching section

- **WHEN** episode 13's stored air instant passes and the home page is the first place I open
- **THEN** the show appears in Currently watching on that same visit, because it is re-opened before the dashboard selects which entries are Watching — not skipped for still being Completed at the moment the page was built

#### Scenario: An entry nobody looks at is still re-opened

- **WHEN** a new episode airs past a Completed entry's episodes watched and no page showing that entry is opened
- **THEN** the recurring airing schedule records the change anyway, so it is logged and queued for sync

#### Scenario: Re-opening twice does nothing further

- **WHEN** the rule is evaluated again on an entry it has already re-opened
- **THEN** nothing further changes, no second activity row is written, and no second sync is queued

#### Scenario: The finish date survives re-opening

- **WHEN** a Completed entry with a finish date is re-opened by a new episode
- **THEN** its finish date is left exactly as it was

#### Scenario: A finished anime is never re-opened

- **WHEN** a Completed entry's anime has finished airing and its aired-so-far count reads higher than its cached total episode count
- **THEN** the entry stays Completed

#### Scenario: Caught-up entries are left alone

- **WHEN** a Completed entry's episodes watched already equals its anime's aired-so-far count
- **THEN** its status is unchanged

### Requirement: Score prompt on completion via the increment button
The system SHALL open a score prompt overlay whenever an episode-count change made from the progress row takes an entry into Completed status **with a finish date set** — whether from the "+" button or from editing the count in place — from every place that row appears (the main dashboard's currently-watching carousel, my list rows, the anime detail page, and any later addition). The overlay SHALL show the anime's picture on the left and a score dropdown offering "No score" and the values 1 through 10, pre-selected with the entry's current score.

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

#### Scenario: Non-final increments do not prompt
- **WHEN** I press the "+" button and episodes-watched stays below the anime's total episode count
- **THEN** no score prompt opens

#### Scenario: Catching up on a currently-airing anime does not prompt
- **WHEN** I press the "+" button on a currently-airing anime and episodes-watched thereby reaches the aired-so-far count
- **THEN** the entry becomes Completed but no score prompt opens, because no finish date was set

### Requirement: Completion prompt fires only on entering Completed

The system SHALL open the completion score prompt only on the transition into Completed status **with a finish date set**, and SHALL NOT open it for an entry that was already Completed before the episode-count change, for an anime whose completion target (total, or aired-so-far count while currently airing) is unknown, for a currently-airing anime's "caught up" completion (which sets no finish date), or when a Rewatching entry finishes and returns to Completed.

The rewatch exclusion is separate from the finish-date rule above: a rewatch's completion does carry a finish date, since its original one is kept rather than overwritten (per "Watching a rewatch to the end counts it automatically"), so it needs its own carve-out. A rewatch ends with a score the entry has carried since its first viewing, so re-asking for one at the end of every rewatch would be noise rather than a decision; the score stays editable in the editor.

#### Scenario: Rewatch increment on a completed entry
- **WHEN** I press the "+" button on an entry that is already Completed
- **THEN** no score prompt opens

#### Scenario: Re-entering the same total on a completed entry
- **WHEN** I edit the count in place on an already-Completed entry and confirm a value equal to the total
- **THEN** no score prompt opens, because the entry was already Completed

#### Scenario: Unknown total episode count
- **WHEN** I press the "+" button on an anime with an unknown total episode count
- **THEN** no score prompt opens, because the entry does not auto-complete

#### Scenario: Reaching what's aired on a still-airing anime does not prompt
- **WHEN** I press the "+" button on a currently-airing anime and episodes-watched reaches the aired-so-far count
- **THEN** no score prompt opens, because the resulting completion sets no finish date

#### Scenario: Rewatch finishing does not prompt
- **WHEN** a Rewatching entry reaches everything available and returns to Completed
- **THEN** no score prompt opens, and the entry's existing score is unchanged

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

