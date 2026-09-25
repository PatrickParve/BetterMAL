# list-editing Specification

## Purpose
The list-editing capability governs the rules a list entry's status, dates, progress and rewatch count follow when I edit it by hand, including the aired guards that gate what may be tracked, Rewatching as its own status with restart and automatic-completion rules, a completed entry reopening when a new episode airs, the completion score prompt, and adding, removing, and placing a newly scored anime in the ranking. Every edit opens through the single overlay editor and triggers MAL sync, but what gets logged is activity-recording's and how overlays behave as a mechanism is overlay-behaviour's.
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

### Requirement: Whether every episode has aired is resolved one way

The system SHALL resolve "every episode of this anime has aired" through a single shared rule, read by every place that needs the whole run to be out — the completion rules and the rewatching eligibility rule alike. An anime SHALL be treated as having aired in full when **either**:

- MyAnimeList reports its airing status as anything other than currently airing (finished, or not recorded at all); **or**
- its total episode count is known, its aired-so-far count is known, and the aired-so-far count has reached the total.

The second arm exists because MyAnimeList's airing status is frequently stale — a show routinely still reads as currently airing for weeks after its finale. The aired-so-far count, resolved from stored AniList airing data per the `episode-airing-data` capability, is the fact that does not lag. The rule SHALL be the union of the two arms and never the intersection, so an anime with no stored airing data is judged exactly as it is today, and an anime whose airing data has run past its total is judged on that rather than on a stale status.

This rule SHALL NOT be read where the question is whether *anything* has aired: that stays with "Whether an anime has aired an episode is resolved one way", which is a separate gate with a separate purpose.

#### Scenario: A stale airing status is overruled by the aired count

- **WHEN** MyAnimeList still reports an anime as currently airing, its total is 12, and 12 episodes have aired according to stored airing data
- **THEN** the anime is treated as having aired in full

#### Scenario: A finished status needs no aired count

- **WHEN** MyAnimeList reports an anime as finished airing and no airing data is stored for it
- **THEN** the anime is treated as having aired in full

#### Scenario: Still airing with episodes to come

- **WHEN** MyAnimeList reports an anime as currently airing, its total is 12, and 7 episodes have aired
- **THEN** the anime is not treated as having aired in full

#### Scenario: Still airing with an unknown total

- **WHEN** MyAnimeList reports an anime as currently airing and its total episode count is unknown
- **THEN** the anime is not treated as having aired in full, because there is no total for the aired count to have reached

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

- **The anime has aired in full**, per "Whether every episode has aired is resolved one way" — its airing status is `finished_airing` or not recorded at all, **or** its total episode count is known and its aired-so-far count has reached that total. An anime with episodes still to come SHALL be refused; there is no complete run to go through again. Reading the shared rule rather than the airing status alone means a run whose every episode has aired is rewatchable even while MyAnimeList still reports it as currently airing, which it routinely does for weeks after a finale.
- **The user has finished the anime at least once.** This SHALL be tested against the entry's history rather than its present status, and SHALL be satisfied by **any** of: a finish date on the entry, a rewatch count above zero, or a current status of Completed.

Because the second condition looks at history, an entry that has been completed before SHALL be settable to Rewatching from **any** status it currently holds — Watching, On hold, Plan to watch, Dropped, or Completed. A rewatch abandoned partway and parked elsewhere SHALL therefore be resumable without first returning the entry to Completed.

An entry whose status is already Rewatching SHALL also satisfy this rule, so that the editor can present the entry's own current status as its selected value rather than as an unavailable one.

The entry editor SHALL present the limit rather than letting a rejected save be attempted: where the rule does not permit it, the Rewatching option SHALL be unavailable in the status dropdown.

#### Scenario: Rewatching a completed, finished anime

- **WHEN** I open the editor for a Completed entry whose anime has finished airing
- **THEN** Rewatching is offered and selecting it is saved

#### Scenario: Rewatching a run whose airing status is stale

- **WHEN** I open the editor for a Completed entry whose anime's total is 12 with all 12 aired, while MyAnimeList still reports it as currently airing
- **THEN** Rewatching is offered and selecting it is saved, because the anime has aired in full

#### Scenario: Resuming a rewatch from another status

- **WHEN** I open the editor for an entry that has a finish date but whose current status is Watching, On hold, or Dropped
- **THEN** Rewatching is offered and selecting it is saved, without the entry having to pass through Completed first

#### Scenario: A rewatch count is enough on its own

- **WHEN** I open the editor for an entry with a rewatch count above zero whose finish date has been cleared
- **THEN** Rewatching is offered, because a rewatch count can only have been earned by finishing the anime

#### Scenario: Not offered for an unfinished anime

- **WHEN** I open the editor for a Completed entry whose anime is currently airing with 7 of 12 episodes aired
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

### Requirement: A rewatch in progress behaves like watching

The system SHALL treat a Rewatching entry the way it treats a Watching one wherever an entry's being in progress is what matters: it SHALL show the editable progress row with its "+" control and in-place editable count, and SHALL be incrementable exactly as a Watching entry is.

#### Scenario: The progress row works as usual

- **WHEN** a Rewatching entry is shown anywhere the progress row appears
- **THEN** its bar, count, and "+" control render and behave exactly as they do for a Watching entry

#### Scenario: Incrementing a rewatch

- **WHEN** I press "+" on a Rewatching entry below the last available episode
- **THEN** its episodes watched increases by one and the started-date, activity-log, and sync rules apply as usual

### Requirement: Started-date on first progress
The system SHALL set `started_at` to today when episodes-watched is set on an anime that was previously at 0 episodes or not yet in my list, and only when all of the following hold:

- `started_at` is currently empty;
- the same edit is not itself supplying a start date — a supplied value stands on its own rather than being preceded by a filled-in one; and
- the entry's finish date is empty **once the edit has been applied in full** — that is, empty both before the edit and after it. A finish date already stored SHALL suppress the fill, and so SHALL one supplied by the same edit.

The finish-date condition is tested against the outcome of the edit rather than the stored value alone because the two would otherwise contradict each other inside a single save: an invented start date of today sitting beside a finish date in the past is an out-of-order pair, which "Start and finish dates are editable in the editor" rejects — failing an edit whose own values were consistent, and naming a date the user never entered. A stored finish date carries the same meaning: it records a run already finished, so the first episode after it begins a rewatch rather than a first start, and the entry's single date pair continues to describe the original watch, as "Completed-date lifecycle" keeps the finish date across rewatches.

An entry left with no start date by this rule SHALL NOT be treated as an error state: a finish date with no start date is a valid pair per "Start and finish dates are editable in the editor", and the start date remains editable by hand at any time.

#### Scenario: First episode watched
- **WHEN** I set episodes-watched above 0 on an anime that had 0 (or was not in my list) and has no start date
- **THEN** `started_at` is set to today

#### Scenario: Subsequent increments do not overwrite start date
- **WHEN** I increment episodes on an anime that already has a `started_at`
- **THEN** `started_at` is left unchanged

#### Scenario: First episode of a rewatch fills no start date
- **WHEN** I watch the first episode of a Rewatching entry that carries a finish date from its original watch and has no start date
- **THEN** the episode count is saved, `started_at` stays empty, and the edit is not rejected for having dates out of order

#### Scenario: Backfilling an old watch with the episode count in the same save
- **WHEN** I set an entry with no dates to Completed, type its episode count, and enter a finish date from two years ago in one save
- **THEN** the entry saves with that finish date, no start date, and no rejection

#### Scenario: A start date supplied in the same save stands on its own
- **WHEN** I set episodes-watched from 0 to above 0 and enter a start date in the same save
- **THEN** the start date I entered is stored, and today's date is never written in its place

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

### Requirement: Unknown total episodes cannot be completed

The system SHALL display an unknown total as `watched/?`, and SHALL allow an entry to be marked Completed only when both of the following hold:

- the anime's **total episode count is known** — it is the figure Completed claims to have watched, and marking Completed SHALL set episodes watched to it, mirroring MyAnimeList's own UI; and
- the anime has **aired in full**, per "Whether every episode has aired is resolved one way".

Where either fails, the system SHALL prevent the completion with a reason naming which one failed, and the entry editor SHALL present Completed as unavailable. An anime that has aired no episode SHALL be refused outright, per "Nothing may be tracked against an anime that has aired no episode".

The completion target SHALL be the total in every case, and SHALL never be the aired-so-far count. An entry cannot reach a total that has not aired in any event: episodes watched is already capped at the aired-so-far count wherever one is known, so an entry standing at its total has necessarily watched only aired episodes. Completion therefore needs no separate check that the viewing was legitimate — reaching the total is that check.

Automatic completion from the progress row SHALL apply the same two conditions and the same target: an episode-count change that takes episodes watched to the known total of an anime that has aired in full completes the entry. A change that reaches the aired-so-far count of an anime with more to come SHALL complete nothing and SHALL leave the entry Watching, which the `main-dashboard` capability's "A caught-up entry leaves the currently-watching carousel" requirement handles as a display concern rather than a status one. Unlike the explicit edit, the automatic path SHALL be silent when its conditions do not hold rather than reporting a rejection.

Because every completion now concerns an anime whose whole run is out, a finish date SHALL always be eligible to be set: an entry with no finish date SHALL have one filled in when it is completed, and an entry that already carries one SHALL keep it untouched. MyAnimeList's airing status SHALL NOT be consulted for this or for any other part of this requirement beyond its role inside the shared aired-in-full rule.

A total episode count MyAnimeList does not publish may still become known from AniList, per the `episode-airing-data` capability. Once known, this requirement reads it as it reads any other total.

#### Scenario: Blocking completion when total unknown

- **WHEN** an anime has finished airing with an unknown total episode count and I attempt to mark it Completed
- **THEN** the system prevents the completion with a reason naming the unknown total

#### Scenario: Displaying unknown total

- **WHEN** an anime with an unknown total is displayed with progress
- **THEN** progress is shown as `watched/?`

#### Scenario: Completing a finished anime fills to the total

- **WHEN** I mark an entry Completed for an anime that has finished airing with a total of 24 episodes
- **THEN** the entry becomes Completed with 24 episodes watched and a finish date

#### Scenario: Completion is refused while episodes are still to come

- **WHEN** I attempt to mark an entry Completed for an anime with 7 of an eventual 12 episodes aired
- **THEN** the system prevents the completion with a reason naming that the anime has not aired in full, and the entry stays as it was

#### Scenario: The final episode completes despite a stale airing status

- **WHEN** I use the "+" control to reach episode 12 of a 12-episode anime whose 12 episodes have all aired, while MyAnimeList still reports it as currently airing
- **THEN** the entry becomes Completed with 12 episodes watched and a finish date

#### Scenario: Reaching what has aired so far does not complete

- **WHEN** I use the "+" control to reach episode 7 of a 12-episode anime of which 7 have aired
- **THEN** the entry stays Watching with 7 episodes watched and no finish date is set

#### Scenario: An unknown total never auto-completes

- **WHEN** I use the "+" control on an anime whose total episode count is unknown
- **THEN** the entry stays Watching, silently, however many episodes it now has watched

#### Scenario: Every completion is eligible for a finish date

- **WHEN** an entry with no finish date is completed
- **THEN** its finish date is set, because the anime it concerns has aired in full

#### Scenario: An AniList-sourced total makes an entry completable

- **WHEN** an anime has aired in full, MyAnimeList publishes no total for it, and AniList reports 12
- **THEN** the entry may be marked Completed and doing so sets its episodes watched to 12

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

Each date field SHALL be composed of three dropdowns — year, month, and day — rather than the browser's own date control, so the field looks and behaves the same in every browser rather than following each browser's native date widget. The day dropdown SHALL offer exactly the days the selected month and year hold, including 29 February in a leap year, and SHALL NOT offer a day that month does not have. Each dropdown SHALL offer a blank option alongside its values.

Clearing SHALL be possible from the field itself and SHALL be reachable in one action: selecting the blank option in any of a field's three dropdowns SHALL clear that whole date, and each field SHALL additionally offer a clear control that empties it outright. A field whose date is already empty SHALL show every dropdown blank, and SHALL NOT pre-select today or any other value on the user's behalf. Each field SHALL additionally offer a control that sets it to today's date in one action.

A date SHALL be considered set only when all three of its parts are chosen; a partly filled field SHALL be treated as empty rather than as an error, and SHALL be sent as no date at all.

Neither date SHALL be settable to a day later than today. The dropdowns SHALL only ever offer choices that keep the field at or before today — the year list SHALL NOT reach past the current year, the month list SHALL NOT reach past the current month once the current year is chosen, and the day list SHALL NOT reach past today's day once the current year and month are both chosen — rather than offering every choice and rejecting an out-of-range result afterward. The server SHALL independently reject a start or finish date later than today on any request that sets one, so the rule holds even if a future date reaches the API by some other path than this field.

The two dates SHALL be independent: a start date with no finish date, and a finish date with no start date, SHALL each be valid and SHALL each save. Only when both are set SHALL their order be checked, and a finish date on the **same day** as the start date SHALL be accepted. The system SHALL reject a finish date earlier than the entry's start date, reporting the problem rather than saving. The check SHALL be made against the dates the entry would hold after the edit — so a finish date set earlier than a start date left untouched from a previous edit is caught too, not only two dates changed together. A date that is unchanged SHALL NOT be sent as an edit.

A changed date SHALL go through the same entry-edit path as every other field, so it is logged as activity and queued for MAL sync with the entry's other values.

#### Scenario: Revealing the date fields
- **WHEN** I open the entry editor and expand the Dates disclosure
- **THEN** start-date and finish-date fields appear, each as a year, month, and day dropdown, pre-filled with the entry's stored dates

#### Scenario: Dates collapsed by default
- **WHEN** the entry editor opens
- **THEN** the date fields are hidden behind the collapsed Dates disclosure

#### Scenario: The fields look the same in every browser
- **WHEN** I open the Dates disclosure in Safari on macOS and in another browser
- **THEN** both show the same three dropdowns, with no browser's own date widget involved

#### Scenario: Setting a date
- **WHEN** I set a start or finish date and save
- **THEN** the date is stored on the entry, logged as activity, and queued for MAL sync

#### Scenario: Clearing a date from its clear control
- **WHEN** I use a date field's clear control on a field that had a value, and save
- **THEN** the entry's date is emptied and the change is logged and queued for sync

#### Scenario: Clearing a date from a dropdown
- **WHEN** I select the blank option in the month dropdown of a filled date field
- **THEN** the whole field reads as empty, and saving empties the entry's date

#### Scenario: Days follow the month
- **WHEN** I select February 2024 and then February 2023
- **THEN** the day dropdown offers 29 days for 2024 and 28 for 2023

#### Scenario: A finish date without a start date
- **WHEN** the entry has no start date, and I set only a finish date and save
- **THEN** the finish date is stored and the save is not rejected

#### Scenario: A start date without a finish date
- **WHEN** the entry has no finish date, and I set only a start date and save
- **THEN** the start date is stored and the save is not rejected

#### Scenario: Same-day start and finish
- **WHEN** I set the start date and the finish date to the same day and save
- **THEN** the save succeeds

#### Scenario: Finish before start rejected
- **WHEN** I enter a finish date earlier than the start date and save
- **THEN** the save is rejected with an explanation naming the problem, and neither date is changed

#### Scenario: Finish before a start date I did not touch
- **WHEN** the entry already has a start date, and I set a finish date earlier than it and save
- **THEN** the save is rejected with the same explanation, and neither date is changed

#### Scenario: Setting a date to today in one action
- **WHEN** I use a date field's "Today" control
- **THEN** all three of its dropdowns are set to today's year, month, and day

#### Scenario: The dropdowns never offer a future date
- **WHEN** I open a date field and select the current year
- **THEN** the month dropdown offers no month later than the current one, and once the current month is also selected, the day dropdown offers no day later than today's

#### Scenario: A future date is rejected even if it reaches the server
- **WHEN** a request sets a start or finish date later than today
- **THEN** the save is rejected with an explanation naming the problem, and neither date is changed

### Requirement: Manually set dates are never overwritten
The system SHALL treat a date the user supplies as authoritative — whether it is already stored on the entry or is arriving in the same edit — and SHALL fill a date automatically only where doing so contradicts nothing the user has said. The automatic started-on-first-progress and finished-on-completion rules SHALL only fill a date that is empty, SHALL NOT overwrite an existing value regardless of how it was set, and SHALL NOT fill a date that the same edit's own values rule out. Clearing a date manually SHALL make it eligible to be filled automatically again by those rules.

An automatic fill SHALL never be the cause of a rejection: where filling a date would put the entry's two dates out of order, the fill SHALL be skipped rather than applied and then reported as an error.

#### Scenario: Automatic rule leaves a manual start date alone
- **WHEN** I set a start date manually and then set episodes-watched above 0 on that entry
- **THEN** the start date I set is kept, not replaced with today

#### Scenario: Automatic rule leaves a manual finish date alone
- **WHEN** I set a finish date manually and then mark the entry Completed
- **THEN** the finish date I set is kept, not replaced with today

#### Scenario: Cleared date can be auto-filled again
- **WHEN** I clear an entry's start date and later set episodes-watched from 0 to above 0
- **THEN** the start date is set to today by the automatic rule

#### Scenario: An automatic fill never causes a rejection
- **WHEN** an edit would have a date filled in automatically that lands out of order against a date the same edit supplies
- **THEN** the automatic fill is skipped and the edit saves, rather than being rejected over a date the user did not enter

### Requirement: Entry editor controls share one size
The system SHALL render every control in the entry editor — dropdowns and text/number inputs alike — at the same width and the same height, so the form reads as a single aligned column rather than a mix of control sizes. Controls added to the editor later SHALL inherit the same sizing rather than defining their own.

A date field, being three dropdowns and a clear control rather than one input, SHALL as a whole occupy the same width and the same height as a single control of the form, its parts sharing that width between them on one line. Its dropdowns SHALL be the same height as every other control, so the Dates disclosure adds no new control size to the form.

#### Scenario: Dropdowns match inputs
- **WHEN** the entry editor is open
- **THEN** its status and score dropdowns are the same width and height as its episodes-watched and rewatch-count inputs

#### Scenario: Date fields match the rest
- **WHEN** the Dates disclosure is expanded
- **THEN** each date field spans the same width and stands at the same height as every other control in the editor, with its three dropdowns sharing that width on one line

### Requirement: A rejected edit reports the reason it was rejected
When the server rejects an entry edit and states why, the system SHALL show that reason to the user in place of a generic failure message, so a rejection the user can act on — a finish date before a start date, a status that anime cannot take, a score on an anime that has aired nothing — reads as what it is rather than as "something went wrong".

The reason SHALL be reported wherever the edit was made: in the entry editor for a save made there, and through the app-wide failure notice the `action-failure-notices` capability defines for an edit made from a control outside it. A rejection that carries no stated reason, and a failure that is not a rejection at all (the server unreachable, an unexpected error), SHALL fall back to the existing generic message.

The reasons the server states SHALL be written for the reader: they SHALL NOT carry an anime's numeric id, a parameter name, an exception type, or any other internal detail, and SHALL read as a sentence naming what was refused and why.

A rejected edit SHALL leave the form as it was, with the user's values intact, so the problem can be corrected and the save retried without re-entering anything.

#### Scenario: A date-order rejection reads as one
- **WHEN** I save a finish date earlier than the entry's start date
- **THEN** the editor reports that the finish date cannot be earlier than the start date, rather than "Could not save changes. Please try again."

#### Scenario: The reason carries no internal detail
- **WHEN** any rejection message is shown
- **THEN** it contains no anime id, parameter name, or exception type

#### Scenario: An unexplained failure keeps the generic message
- **WHEN** a save fails without the server stating a reason
- **THEN** the editor shows its existing generic failure message

#### Scenario: The form survives a rejection
- **WHEN** a save is rejected
- **THEN** every value I had entered is still in the form, and I can correct the problem and save again

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

The field SHALL accept only non-negative whole numbers: non-numeric characters SHALL be rejected, and negative values SHALL NOT be enterable or submittable, filtered as each character is typed. The enterable ceiling is the number of episodes actually available, and never more than the anime's own total: where an aired-so-far count and a total episode count are both known, the ceiling SHALL be the **lower of the two**. For an anime that has finished airing that resolves to the total; for an anime still airing it resolves to the aired-so-far count, which may be lower than the total. Where only one of the two figures is known, that figure is the ceiling. When neither an aired-so-far count nor a total is known, no upper cap applies. Stored airing data reporting **more** episodes than the published total SHALL NOT raise the ceiling above the total — this happens where AniList groups into one entry what MyAnimeList splits into two, or numbers a season continuously from an earlier one, and it is a difference between sources rather than episodes the user can watch against this entry. The field SHALL clamp to this ceiling live, as the value is typed, so a value above it is never shown even momentarily — not corrected only once confirmed. The plus control SHALL disable at this same ceiling, so it cannot increment past what the field would allow.

This ceiling SHALL be the single one the whole system enforces. The entry editor's own episodes-watched field SHALL use it rather than the total alone, so the editor never accepts a count the server will reject, and the server SHALL apply it to every edit path so no client can save above it. Keeping the ceiling at or below the total also keeps "An entry is Completed only while its progress covers everything available" reachable: a count that could exceed the total would sit permanently past the completion target and never complete.

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

#### Scenario: Stored airing data above the total does not raise the ceiling
- **WHEN** an anime publishes a total of 12 episodes while stored airing data holds 13 aired episodes, and I try to enter 13
- **THEN** the value is capped at 12, and reaching 12 completes the entry as it would for any other finished anime

#### Scenario: The entry editor shares the same ceiling
- **WHEN** I open the entry editor for a still-airing anime with 3 episodes aired of a published total of 12 and try to enter 12
- **THEN** the editor caps the value at 3, rather than accepting a count the server would reject

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

### Requirement: A caught-up entry is completed once its full run is known

The system SHALL move a **Watching** entry to **Completed** when its episodes watched has reached its anime's known total episode count and that anime has aired in full — the case where the entry arrived at the figure before the figure was known, so no edit was in flight at the moment it became true.

This is the counterpart of "A completed entry re-opens while more of its anime is to come", and SHALL be evaluated on the same read paths and the same recurring airing schedule, so an entry the user never looks at is still settled, and an entry they do look at already reads as Completed the first time any surface displaying it is read.

It exists chiefly for a total that arrives late. An anime whose total MyAnimeList never published may acquire one from AniList (per the `episode-airing-data` capability) long after the user watched every episode there was; without this rule that entry would sit at Watching against a total it has already met.

The completion SHALL fill in a finish date where the entry has none and SHALL leave an existing one untouched, per "Manually set dates are never overwritten". It SHALL be written to the activity log and queued for MyAnimeList sync like any other status change, and SHALL NOT alter the entry's episodes watched. Applying it again SHALL have no further effect.

It SHALL apply to **Watching** entries only. A Rewatching entry reaching its total is governed by "Watching a rewatch to the end counts it automatically", which additionally raises the rewatch count; a rewatch SHALL NOT be completed by this rule and SHALL NOT have its rewatch count raised by it.

Because this completion is applied by the system rather than by an edit from the progress row, it SHALL NOT open the completion score prompt.

#### Scenario: A late-arriving total completes a caught-up entry

- **WHEN** an entry is Watching at 12 episodes watched against an unknown total, and a refresh then establishes the anime's total as 12 with all 12 aired
- **THEN** the entry becomes Completed with a finish date, the change is logged and queued for sync, and its episodes watched stays at 12

#### Scenario: The change is visible on the next look

- **WHEN** that total is established and I then open my list, the main dashboard, or the anime's detail page
- **THEN** the entry already reads as Completed, without waiting for a scheduled pass to have run

#### Scenario: An entry nobody looks at is still completed

- **WHEN** the condition becomes true and no page showing that entry is opened
- **THEN** the recurring airing schedule records the completion anyway, so it is logged and queued for sync

#### Scenario: Behind the total is not completed

- **WHEN** an entry is Watching at 9 episodes watched and its anime's total is 12
- **THEN** the entry stays Watching at 9

#### Scenario: An unknown total is not completed

- **WHEN** an entry is Watching and its anime's total episode count is unknown
- **THEN** the entry stays Watching

#### Scenario: A run not yet fully aired is not completed

- **WHEN** an entry is Watching at 12 episodes watched, its anime's total is 12, but stored airing data reports only 11 aired and MyAnimeList reports the anime as currently airing
- **THEN** the entry stays Watching, because the anime has not aired in full

#### Scenario: A rewatch is left alone

- **WHEN** a Rewatching entry's episodes watched equals its anime's total
- **THEN** this rule changes nothing about it, and its rewatch count is not raised

#### Scenario: An existing finish date survives

- **WHEN** a caught-up entry that already carries a finish date is completed by this rule
- **THEN** its finish date is left exactly as it was

#### Scenario: No score prompt from an automatic completion

- **WHEN** this rule completes an entry while I am looking at the page that triggered it
- **THEN** no completion score prompt opens

### Requirement: A completed entry re-opens when a new episode airs

The system SHALL return a Completed entry to Watching while its anime is still to come: where MyAnimeList reports the anime as currently airing and the entry has **not** watched the anime's known total. Such an entry is not finished — either more has since aired than it has seen, or it was recorded as Completed under an earlier rule that allowed a partial count to count as complete.

An entry that has watched the full published total SHALL NOT be re-opened, whatever MyAnimeList's airing status says. That is what stops a stale `currently_airing` — routinely still set weeks after a show ends — from repeatedly un-completing a run the user has genuinely finished, and it is why this rule reads the total rather than the aired-so-far count.

An entry whose anime MyAnimeList does not report as currently airing SHALL NOT be re-opened at all, so a list imported from MyAnimeList with entries marked completed at a partial count is left exactly as MyAnimeList holds it.

An episode counts as aired the moment its stored air instant passes, without any refresh, poll, or user action (see the `episode-airing-data` capability), and a total episode count may change on any refresh. The re-opening SHALL follow from either directly rather than waiting on a scheduled pass: an entry SHALL already read as Watching the first time any surface displaying it is read after the condition became true — my list, the main dashboard, and the anime detail page alike.

The system SHALL additionally evaluate the same rule on the recurring airing schedule, so that an entry the user never looks at still has its re-opening recorded and synced.

Either path SHALL treat it as an ordinary status change: written to the activity log and queued for MyAnimeList sync like any other. Applying the rule more than once SHALL have no further effect, since its condition no longer holds once applied.

The entry's episodes watched SHALL NOT be altered, and its finish date SHALL NOT be cleared — the existing rule that a finish date is never overwritten or cleared automatically continues to apply.

The re-opened status SHALL always be Watching, never Rewatching: an anime this rule fires for has episodes still to come, so it has not aired in full, and "Rewatching requires a finished anime the user has finished once" forbids Rewatching there — whatever the entry's rewatch history shows.

#### Scenario: The published total grows past a completed entry

- **WHEN** an entry is Completed at 12 episodes watched, its anime is currently airing, and its total is revised to 24
- **THEN** the entry's status becomes Watching, the change is logged and queued for sync, and its episodes watched stays at 12

#### Scenario: An entry completed at a partial count is re-opened

- **WHEN** an entry is Completed at 7 episodes watched on a currently-airing anime whose total is 12
- **THEN** the entry's status becomes Watching with 7 episodes watched, because it has not watched the published total

#### Scenario: A stale airing status does not un-complete a finished run

- **WHEN** an entry is Completed at 12 episodes watched, its anime's total is 12, and MyAnimeList still reports the anime as currently airing
- **THEN** the entry stays Completed

#### Scenario: The change is visible on the next look, not on the next scheduled pass

- **WHEN** the condition becomes true and I then open my list, the main dashboard, or that anime's detail page
- **THEN** the entry already reads as Watching, without waiting for a refresh or a scheduled pass to have run

#### Scenario: A re-opened show reaches the dashboard's currently-watching section

- **WHEN** the condition becomes true and the home page is the first place I open
- **THEN** the show appears in Currently watching on that same visit, because it is re-opened before the dashboard selects which entries are Watching — not skipped for still being Completed at the moment the page was built

#### Scenario: An entry nobody looks at is still re-opened

- **WHEN** the condition becomes true and no page showing that entry is opened
- **THEN** the recurring airing schedule records the change anyway, so it is logged and queued for sync

#### Scenario: Re-opening twice does nothing further

- **WHEN** the rule is evaluated again on an entry it has already re-opened
- **THEN** nothing further changes, no second activity row is written, and no second sync is queued

#### Scenario: The finish date survives re-opening

- **WHEN** a Completed entry is re-opened
- **THEN** its finish date is left exactly as it was

#### Scenario: A finished anime is never re-opened

- **WHEN** a Completed entry's anime is not reported as currently airing, whatever its aired-so-far and total counts read
- **THEN** the entry stays Completed

#### Scenario: An imported partial completion is left alone

- **WHEN** MyAnimeList holds an entry as completed with 0 episodes watched for an anime it reports as finished airing
- **THEN** the entry stays Completed and nothing is pushed back to MyAnimeList

### Requirement: Score prompt on completion via the increment button
The system SHALL open a score prompt overlay whenever an episode-count change made from the progress row takes an entry into Completed status — whether from the "+" button or from editing the count in place — from every place that row appears (the main dashboard's currently-watching carousel, my list rows, the anime detail page, and any later addition), except where "Completion prompt fires only on entering Completed" rules it out. The overlay SHALL show the anime's picture, large and whole, on the left, and beside it a score dropdown offering "No score" and the values 1 through 10, pre-selected with the entry's current score. Its layout is governed by "The completion prompt is laid out around a large, whole picture".

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

### Requirement: The completion prompt is laid out around a large, whole picture
The completion score prompt SHALL present the anime's picture as its main visual, far larger than a list-row thumbnail, and SHALL draw it **whole, at its own proportions**, with nothing cut off.

A picture that is not landscape SHALL be drawn at one fixed width and take whatever height its own proportions give it there. This covers a poster, an upright picture and a square picture. A **landscape** picture, strictly wider than it is tall, SHALL be drawn at one wider fixed width, again at its own height, so a wide, short picture is not reduced to a strip. The picture's width SHALL depend on that landscape test alone. It SHALL take one of those two widths, never a width computed per picture. The prompt's picture SHALL NOT be drawn under the row-slot rule the app's list rows use.

Until the picture has loaded, the prompt SHALL reserve a portrait box of the fixed width, so the prompt does not collapse and then expand as the picture arrives. When the anime has no picture, a placeholder SHALL occupy that portrait box. A picture that is slow to arrive SHALL fade in over the box's placeholder surface, under the same timing rules as the pictures `artwork-presentation` governs. A picture the browser already holds, or one that arrives within about 150 ms, SHALL show at once. Under reduced motion it SHALL appear without animating. A picture that fails to load SHALL leave the placeholder surface showing.

The anime's title, the prompt's hint and the score dropdown SHALL sit beside the picture, aligned to its top. The prompt SHALL be wide enough for that text to read comfortably beside a landscape picture as well as beside a portrait one.

The actions SHALL sit in a row of their own beneath the picture and the text, spanning the prompt's full width:

- Cancel SHALL sit at the row's start.
- Save SHALL sit at the row's end.
- Save and rank, when it is offered, SHALL sit immediately before Save.

Save SHALL therefore not move when Save and rank appears or disappears as the chosen score changes. Every action SHALL be drawn at one shared height, with its label on a single line. No action's label SHALL wrap inside the action. Where the row is too narrow for all of them, a whole action SHALL move onto a new line instead.

On a viewport too narrow for the picture and the text to sit side by side, the picture SHALL move above the text, centred, and the action row SHALL follow beneath the text.

This requirement governs presentation only. The actions offered, their labels, when Save and rank is offered, and what each action does SHALL stay as "Score prompt on completion via the increment button" and "Saving or dismissing the completion score prompt" define them.

#### Scenario: A poster is shown large and whole
- **WHEN** the completion prompt opens for an anime whose picture is a 2:3 poster
- **THEN** the poster is drawn whole at 2:3, at the prompt's fixed picture width, and far larger than a list-row thumbnail

#### Scenario: A landscape picture is shown wider
- **WHEN** the completion prompt opens for an anime whose picture is wider than it is tall
- **THEN** the picture is drawn whole at the wider fixed width, at its own height, and the title, hint and score dropdown still sit beside it

#### Scenario: A square picture keeps the portrait width
- **WHEN** the completion prompt opens for an anime whose picture is exactly square
- **THEN** it is drawn whole and square at the portrait width, not at the landscape width

#### Scenario: The actions stay on one line
- **WHEN** the completion prompt opens and I pick a score of 1 through 10, so Save and rank is offered
- **THEN** Cancel, Save and rank, and Save each show their label on a single line and are drawn at the same height, in a row beneath the picture and the text

#### Scenario: Save does not move
- **WHEN** I change the chosen score from 8 to "No score", so Save and rank is withdrawn
- **THEN** Save stays at the end of the action row where it was, and only Save and rank disappears

#### Scenario: A narrow screen stacks the prompt
- **WHEN** the completion prompt opens on a phone-width viewport
- **THEN** the picture sits centred above the title, hint and score dropdown, the action row follows beneath them, and no action's label wraps

#### Scenario: No picture
- **WHEN** the completion prompt opens for an anime that has no picture
- **THEN** a placeholder occupies the portrait picture box, and the rest of the prompt is laid out as it is with a picture

#### Scenario: A slow picture fades in
- **WHEN** the prompt's picture finishes downloading more than about 150 ms after the prompt opened
- **THEN** the picture fades in over the reserved box rather than appearing abruptly or being drawn part-way

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

### Requirement: Raising progress resumes an entry

The system SHALL treat raising an entry's episodes watched as a statement that the anime is being watched now. When an edit raises the count **above** the stored one and the new count does **not** reach everything available, the system SHALL set the entry's status to **Watching**, unless its stored status is Watching, Rewatching, or Completed.

- Watching and Rewatching are excluded because there is nothing to resume — a rewatch in progress stays a rewatch in progress, per "A rewatch in progress behaves like watching".
- Completed is excluded because "An entry is Completed only while its progress covers everything available" already governs a Completed entry whose progress no longer covers everything, and sends it to Rewatching where that is permitted.
- Reaching everything available is unaffected: that case still completes the entry, per the existing completion rules.

The rule SHALL apply however the count is raised — the "+" control, the in-place editable count, or the entry editor — and SHALL apply when there is no known total or aired-so-far count to reach, since a raise can then reach nothing by definition.

The rule SHALL NOT fire on an edit that lowers the count or leaves it unchanged: lowering a Dropped or On hold entry's count corrects a record of something set aside, and says nothing about watching it now.

A status supplied explicitly in the same edit SHALL take precedence over this rule, **including the status the entry already holds** — so an edit that raises the count while stating "still Dropped" saves as Dropped. The entry editor SHALL make that possible and visible: while its episode-count field holds a value above the entry's stored count, and the conditions above are otherwise met, its status control SHALL show Watching, updating as the count is typed rather than only on save; whatever its status control shows when the edit is saved SHALL be what is saved.

A resumption SHALL be an ordinary status change in every other respect — written to the activity log and queued for MAL sync like any other, per "Edits log activity and trigger sync".

#### Scenario: Watching an episode of something planned

- **WHEN** I press "+" on a Plan to watch entry for a 12-episode anime at 0 episodes watched
- **THEN** its episodes watched becomes 1 and its status becomes Watching

#### Scenario: Picking a dropped show back up

- **WHEN** I set the in-place count on a Dropped entry from 5 to 7 of 24 episodes
- **THEN** its episodes watched becomes 7 and its status becomes Watching

#### Scenario: Resuming something on hold

- **WHEN** I press "+" on an On hold entry below the last available episode
- **THEN** its status becomes Watching

#### Scenario: Reaching the end still completes

- **WHEN** I press "+" on a Plan to watch entry at episode 11 of 12 available
- **THEN** its status becomes Completed rather than Watching

#### Scenario: An unknown total still resumes

- **WHEN** I raise the count on a Dropped entry whose anime has no known total or aired-so-far count
- **THEN** its status becomes Watching

#### Scenario: Lowering a count changes nothing

- **WHEN** I lower a Dropped entry's episodes watched from 5 to 3
- **THEN** its status stays Dropped

#### Scenario: A rewatch is not resumed

- **WHEN** I press "+" on a Rewatching entry below the last available episode
- **THEN** its status stays Rewatching

#### Scenario: A completed entry follows the Completed rule instead

- **WHEN** an edit leaves a Completed entry's progress short of everything available
- **THEN** it lands in Rewatching or Watching per "An entry is Completed only while its progress covers everything available", not by this rule

#### Scenario: The editor shows the resumption before saving

- **WHEN** I open the editor on a Dropped entry and type an episode count above the one it holds, short of everything available
- **THEN** the editor's status control changes to Watching while I am still editing

#### Scenario: Keeping the original status from the editor

- **WHEN** I raise the count in the editor, set its status control back to Dropped, and save
- **THEN** the entry saves with the raised count and its status stays Dropped

#### Scenario: Choosing a different status from the editor

- **WHEN** I raise the count in the editor and set its status control to On hold before saving
- **THEN** the entry saves as On hold with the raised count

#### Scenario: A resumption is logged and synced

- **WHEN** an entry is resumed to Watching by a raised count
- **THEN** an activity entry records the status change and the entry is queued for MAL sync, exactly as a status change made by hand would be

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

### Requirement: Automatic status settling treats each entry on its own
When one evaluation of "A caught-up entry is completed once its full run is known" and "A completed entry re-opens when a new episode airs" moves more than one entry, the system SHALL treat each moved entry exactly as if it had been the only one. The evaluation can happen on any read path or on the recurring airing schedule. Each moved entry SHALL get its own activity-log row and its own MyAnimeList sync request, and re-openings and completions SHALL be able to happen in the same evaluation. How many entries are moved together SHALL NOT change what happens to any one of them.

Each entry SHALL be checked against its stored status again immediately before it is moved. Some other change may already have moved it off the status the rule starts from: another read settling it first, an edit from the progress row or the entry editor, or an import or sync. Such an entry SHALL be left as that change left it, with no status change, activity-log row or sync request from this evaluation.

The same SHALL hold for a change that reaches an entry after that check but before the move is saved. That one entry SHALL NOT be moved by this evaluation, and SHALL NOT receive a log row or a sync request from it. The other entries in the same evaluation SHALL still be moved, logged and queued for sync as normal, and SHALL NOT be discarded along with it.

Either way, a page whose read ran the evaluation SHALL show each such entry with the status the database holds for it. An entry left alone this way is not skipped for good: the rules are evaluated again on the next read and on the recurring airing schedule, as they always are.

#### Scenario: Several entries complete on one read
- **WHEN** I open my list and three Watching entries have each watched their anime's known total, and each anime has aired in full
- **THEN** all three become Completed with a finish date, each with its own activity-log row, and each is queued for sync once

#### Scenario: Several entries re-open on one read
- **WHEN** I open my list and three Completed entries are each on a currently-airing anime whose known total they haven't watched
- **THEN** all three become Watching, each with its own activity-log row, and each is queued for sync once

#### Scenario: A re-opening and a completion on the same read
- **WHEN** I open my list and one entry qualifies to be re-opened while another qualifies to be completed
- **THEN** the first becomes Watching and the second becomes Completed, each with its own activity-log row and its own sync request

#### Scenario: An entry another change already moved is left alone
- **WHEN** two Watching entries qualify to be completed when my list's read picks them, and before settling checks them again one of them is marked Dropped from the entry editor
- **THEN** that entry stays Dropped, no completion is logged or queued for it, and my list shows it as Dropped, while the other entry is still completed, logged and queued for sync

#### Scenario: A change landing just before the save doesn't hold up the rest
- **WHEN** three entries qualify to be moved on a read, and a sync push saves one of them after settling has checked it but before settling saves
- **THEN** that one entry is not moved by this read and gets no log row or sync request from it, the other two are moved, logged and queued for sync, and the next read evaluates the first entry again

#### Scenario: Nothing is queued for an entry that wasn't moved
- **WHEN** an evaluation picks four entries and one of them turns out to have been moved by another change already
- **THEN** exactly three sync requests are queued, one for each entry this evaluation actually moved

