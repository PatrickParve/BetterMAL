## ADDED Requirements

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

## MODIFIED Requirements

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

### Requirement: An entry is Completed only while its progress covers everything available

The system SHALL treat Completed as a claim that everything available has been watched, and SHALL NOT leave an entry Completed once that stops being true.

Where an edit lowers a Completed entry's episodes watched below its anime's total, the system SHALL move that entry out of Completed, keeping the lowered count as entered. It SHALL land in **Rewatching** where that status is permitted for the anime — a count dropping on an anime already completed once means it is being watched again — and in **Watching** where it is not.

This is distinct from an entry ceasing to be Completed because *more* became available, which always sends it to Watching: the two cases differ in cause, and so in meaning.

- Progress dropped, and the anime has aired in full → **Rewatching** (already finished once; going through it again).
- Progress dropped on an entry that is Completed while its anime has not aired in full → **Watching**, since "Rewatching requires a finished anime the user has finished once" forbids Rewatching there. Because completion itself now requires the anime to have aired in full, this case is reachable only for an entry recorded under an earlier rule that the re-opening rule has not yet returned to Watching; it is kept so such an entry never lands in an impossible status.
- The published total grew past the progress → **Watching** (more of the show exists; behind on a first viewing).

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
The system SHALL open a score prompt overlay whenever an episode-count change made from the progress row takes an entry into Completed status — whether from the "+" button or from editing the count in place — from every place that row appears (the main dashboard's currently-watching carousel, my list rows, the anime detail page, and any later addition). The overlay SHALL show the anime's picture on the left and a score dropdown offering "No score" and the values 1 through 10, pre-selected with the entry's current score.

The overlay SHALL offer three actions: skip, save, and **save and rank**. Save and rank SHALL save the chosen score and then open the ranking editor focused on that anime, so an anime can be finished, scored, and placed in one pass. It SHALL be offered only while the chosen score would leave the entry hand-orderable — with "No score" selected, or for a dropped or short-form anime, saving alone is the only save action offered.

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
- **THEN** a save-and-rank action is offered alongside save and skip

#### Scenario: No rank action without a score
- **WHEN** "No score" is selected in the completion prompt
- **THEN** no save-and-rank action is offered

#### Scenario: Non-final increments do not prompt
- **WHEN** I press the "+" button and episodes-watched stays below the anime's total episode count
- **THEN** no score prompt opens

#### Scenario: Catching up on a run still to come does not prompt
- **WHEN** I press the "+" button and episodes-watched thereby reaches the aired-so-far count of an anime whose total is higher
- **THEN** the entry stays Watching and no score prompt opens

### Requirement: Completion prompt fires only on entering Completed

The system SHALL open the completion score prompt only on the transition into Completed status, and SHALL NOT open it for an entry that was already Completed before the episode-count change, for an anime whose total episode count is unknown, for an anime that has not aired in full (where no episode-count change completes anything), when a Rewatching entry finishes and returns to Completed, or when the system itself completes a caught-up entry because its full run became known.

The rewatch exclusion is separate from the others: a rewatch's completion is a real transition into Completed, so it needs its own carve-out. A rewatch ends with a score the entry has carried since its first viewing, so re-asking for one at the end of every rewatch would be noise rather than a decision; the score stays editable in the editor.

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

#### Scenario: Rewatch finishing does not prompt
- **WHEN** a Rewatching entry reaches everything available and returns to Completed
- **THEN** no score prompt opens, and the entry's existing score is unchanged

#### Scenario: A system completion does not prompt
- **WHEN** a caught-up entry is completed because its total became known while the page was open
- **THEN** no score prompt opens
