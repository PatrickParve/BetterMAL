## ADDED Requirements

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

Rewatching's own eligibility rule (the `add-rewatching-status` capability's "Rewatching requires a finished anime the user has finished once") already refuses it for any anime that has not finished airing, which every anime with nothing aired satisfies trivially — an anime nobody has ever been able to watch cannot have been watched once, whatever the entry's history shows. Listing it among the rejected statuses here states that plainly rather than leaving it to be derived from the two rules' overlap, and composes with — rather than overrides — Rewatching's own gating.

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

The re-opened status SHALL always be Watching, never Rewatching, and this holds without exception: the `add-rewatching-status` capability's "Rewatching requires a finished anime the user has finished once" permits Rewatching only for an anime that has finished airing, and this rule fires only while the anime is still currently airing — the two conditions cannot hold at once, so Rewatching is never a candidate here, whatever the entry's rewatch history shows.

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

## MODIFIED Requirements

### Requirement: Unknown total episodes cannot be completed

The system SHALL display an unknown total as `watched/?` and SHALL allow an entry to be marked Completed only when the system can establish how many episodes there are to have watched:

- For an anime that has **finished airing**, Completed SHALL require a known total episode count. Marking Completed SHALL set episodes watched to that total, mirroring MyAnimeList's own UI.
- For an anime that is **currently airing**, Completed means having watched every episode out so far. It SHALL require a known aired-so-far episode count, and marking Completed SHALL set episodes watched to that aired-so-far count rather than to the eventual total.
- For an anime that has **aired no episode**, Completed SHALL be rejected outright, per "Nothing may be tracked against an anime that has aired no episode".

An anime whose airing status is not recorded SHALL be treated as the finished-airing case above, since its total episode count is the only figure available.

Where the required figure is unknown, the system SHALL prevent the completion and the entry editor SHALL present Completed as unavailable.

Completing a **currently-airing** anime SHALL NOT set a finish date — the anime has not actually finished, so recording one would be false, and D6's re-opening would otherwise leave a stale finish date on an entry that has since returned to Watching. The existing never-overwrite rule continues to apply once a finish date is eligible to be set: the first time an entry is completed while its anime reads as having **finished** airing, its finish date SHALL be filled in as normal, and every completion after that SHALL leave an already-set finish date untouched.

This requirement makes Completed reachable for a currently-airing anime, once its aired-so-far count is known — a case the `add-rewatching-status` capability's "An entry is Completed only while its progress covers everything available" requirement already accounts for: where such an entry's episodes watched is later lowered below that aired-so-far count, it lands in **Watching**, not Rewatching, because Rewatching requires a finished anime and this one has not finished airing. That is the fallback case that requirement describes for exactly this reason — it exists only because Completed is reachable here on an anime that has not finished airing.

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

The system SHALL open the completion score prompt only on the transition into Completed status **with a finish date set**, and SHALL NOT open it for an entry that was already Completed before the episode-count change, for an anime whose completion target (total, or aired-so-far count while currently airing) is unknown, or for a currently-airing anime's "caught up" completion, which sets no finish date.

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
