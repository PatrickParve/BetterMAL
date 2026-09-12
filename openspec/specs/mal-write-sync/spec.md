# mal-write-sync Specification

## Purpose
TBD - created by archiving change bootstrap-anime-tracker. Update Purpose after archive.
## Requirements
### Requirement: Debounced per-entry sync scheduling
The system SHALL, when a list entry changes, mark it `pending_sync = true` and start or restart a short per-entry timer (8 seconds) scoped to that specific anime.

#### Scenario: Change schedules a push
- **WHEN** an entry's episode count, status, score, or rewatch count changes
- **THEN** the entry is marked `pending_sync = true` and its per-entry timer starts

### Requirement: Timer coalescing per anime
The system SHALL reset an anime's timer on any further edit to that same anime, and SHALL keep timers for different anime independent of each other.

#### Scenario: Rapid edits coalesce into one push
- **WHEN** the same anime's episode count is incremented five times in quick succession
- **THEN** the timer resets each time and only one push occurs after the edits settle

#### Scenario: Different anime do not block each other
- **WHEN** two different anime are edited around the same time
- **THEN** each pushes on its own timer without waiting on the other

### Requirement: Push changed entry to MAL
The system SHALL, when an entry's timer expires with no further edits, push that entry to MAL via `PATCH /v2/anime/{id}/my_list_status` with the changed fields, and clear `pending_sync` on success.

#### Scenario: Successful push
- **WHEN** an entry's debounce timer expires
- **THEN** the system PATCHes `my_list_status` for that anime and, on success, sets `pending_sync = false` and updates `last_synced_at`

### Requirement: Failure retains pending state for retry
The system SHALL, when a push fails (offline, MAL down, token expired), leave `pending_sync = true` and rely on a background retry job to push it later, never silently dropping the change.

The retry job SHALL keep attempting a failed push **for as long as the application keeps running**. Where the application is restarted while an entry is still pending, that entry SHALL be held for review on the next start instead of being retried automatically, since its value has then crossed a process boundary and is of unknown age.

#### Scenario: Push failure is retried
- **WHEN** a push fails
- **THEN** the entry stays `pending_sync = true` and a background retry job later attempts it again

#### Scenario: A failure surviving a restart is held instead
- **WHEN** the application is restarted while an entry is still pending from a failed push
- **THEN** the entry is held for review on the next start rather than pushed by the retry job

### Requirement: Manual sync now
The system SHALL provide a manual "sync now" action that processes the pending queue immediately, touching only pending/changed entries and never the whole list. Pending removals SHALL be processed by the same action, alongside pending edits.

Items held for review SHALL be excluded from this action, exactly as they are excluded from the automatic push paths. "Sync now" SHALL NOT be a way to push a held change without deciding it.

Sync now SHALL run **in the background**. The request that starts it SHALL return once the run has started, and the run SHALL continue whether or not the page that started it stays open. A request made while a sync now is starting or running SHALL start nothing (see `background-jobs`, "A job is started once").

The run SHALL report its progress as the number pushed out of the pending edits and removals it set out to push, counted when it starts. It SHALL end:
- as **complete** when every one was pushed
- as **failed** when any could not be, saying how many; those SHALL stay pending for the retry job, exactly as any failed push does

The retry job pushes through the same code. Its runs SHALL NOT be reported as a sync now (see `background-jobs`, "Automatic runs of shared work are told apart from runs I started").

#### Scenario: Immediate flush of pending entries
- **WHEN** the user triggers "sync now"
- **THEN** all currently pending entries are pushed immediately without waiting out their debounce, and non-pending entries are untouched

#### Scenario: Pending removals flush too
- **WHEN** the user triggers "sync now" while a removal is pending
- **THEN** that removal is pushed to MAL in the same run

#### Scenario: Held items are not flushed
- **WHEN** the user triggers "sync now" while some items are held for review
- **THEN** those items are not pushed and stay held

#### Scenario: Sync now runs in the background
- **WHEN** I trigger "sync now" with five edits pending and leave the page
- **THEN** the pushes continue, and its progress counts them out of five

#### Scenario: A second sync now starts nothing
- **WHEN** a sync now is running and I trigger it again
- **THEN** no second run starts

#### Scenario: Unsent edits are reported
- **WHEN** a sync now pushes three of five edits and two fail
- **THEN** it ends as failed, saying two of five could not be sent, and those two stay pending for the retry job

#### Scenario: The retry job is not a sync now
- **WHEN** the retry job pushes pending edits
- **THEN** nothing is reported as a sync now

### Requirement: Entry removal is pushed to MAL
The system SHALL, when an entry is removed from my list, record a durable pending removal for that anime and push it to MAL via `DELETE /v2/anime/{id}/my_list_status`, clearing the pending removal on success. A removal SHALL be pushed without waiting out an edit debounce, since a removed entry cannot receive further edits to coalesce with.

MAL reporting that the entry does not exist (404) SHALL count as success, because the desired end state — the anime absent from my MAL list — already holds.

#### Scenario: Successful removal push
- **WHEN** an entry is removed from my list
- **THEN** the system issues `DELETE /v2/anime/{id}/my_list_status` and, on success, clears that anime's pending removal

#### Scenario: Already absent on MAL
- **WHEN** the removal push returns 404 because MAL has no such list entry
- **THEN** the pending removal is cleared as though the delete had succeeded

### Requirement: Failed removal retains pending state for retry
The system SHALL, when a removal push fails (offline, MAL down, token expired), keep the pending removal record and rely on the same background retry job that retries pending edits to push it later, never silently dropping it. The local entry SHALL stay deleted while the removal is pending, so a failed push is not visible as the anime reappearing in my list.

Where the application is restarted while a removal is still queued, that removal SHALL be held for review on the next start rather than retried automatically — a removal that has crossed a process boundary destroys a MyAnimeList entry outright if it is stale.

#### Scenario: Removal push failure is retried
- **WHEN** a removal push fails
- **THEN** the pending removal is retained and a background retry job later attempts it again

#### Scenario: Removal survives a restart
- **WHEN** the application restarts with a removal still pending
- **THEN** the pending removal is still recorded, and is held for review rather than pushed automatically

#### Scenario: Local list is unaffected by a failed push
- **WHEN** a removal's push to MAL has not yet succeeded
- **THEN** the anime is still absent from my list locally

### Requirement: Changes pending when the app starts are held for review

A change that is still waiting to be pushed at the moment the application starts SHALL be **held** rather than pushed. It is only still pending because its push failed *and* the application was closed before the retry recovered, so its value is of unknown age and another device may have moved the same entry on since.

The hold SHALL be applied to every entry with `pending_sync = true` and every queued removal, **once per application start, before anything is able to push** — before the retry job's first pass, before any debounce timer can exist, and before any request can be served.

The hold SHALL be **durable**: it SHALL survive a crash, a further restart, and a review left unfinished, and an item SHALL stay held until it is explicitly accepted or declined, released by a fresh edit, or found to have nothing left to send. Applying the hold a second time SHALL neither release an existing hold nor re-date it.

A change that becomes pending **during** a session SHALL NOT be held, however long that session runs and however many times its push has failed: the value is current and the retry job is the right answer for it.

An automatic status transition — the reopening of a completed entry whose anime has aired further episodes, or the completion of an entry that has reached its total — SHALL NOT release a hold. It is an inference from an airing schedule rather than a statement of intent, and an entry whose stored value may be weeks out of date is not made current by it.

#### Scenario: An entry pending across a restart is held
- **WHEN** the application starts with an entry still marked `pending_sync = true` from a previous session
- **THEN** that entry is held for review and is not pushed to MyAnimeList

#### Scenario: An in-session failure is not held
- **WHEN** a push fails while I am using the app and MyAnimeList later becomes reachable again
- **THEN** the entry is pushed automatically, having never been held

#### Scenario: A hold survives a further restart
- **WHEN** the application is restarted again while an item is still held and undecided
- **THEN** that item is still held, with the moment it was first held unchanged

#### Scenario: An automatic transition does not release a hold
- **WHEN** a held entry is automatically reopened or completed because its anime's airing state changed
- **THEN** the entry stays held, and the change is not pushed

### Requirement: Held changes are excluded from every automatic push

While an item is held, **no** path SHALL push it to MyAnimeList: not the per-entry debounce timer, not the background retry job, and not the manual "sync now" action. The exclusion SHALL be enforced at the point the push to MyAnimeList is made, not only where the pending queue is listed, so that any caller reaching that push inherits it.

A held item SHALL remain marked as pending throughout, so that nothing else in the system treats it as already synced.

#### Scenario: The retry job skips a held entry
- **WHEN** the background retry job runs while an entry is held
- **THEN** that entry is not pushed and stays held

#### Scenario: A debounce timer firing on a held entry pushes nothing
- **WHEN** a debounced push fires for an anime whose entry is held
- **THEN** nothing is sent to MyAnimeList and the entry stays held

#### Scenario: Sync now does not flush held items
- **WHEN** I trigger "sync now" while some items are held and others are pending in-session
- **THEN** the in-session pending items are pushed and the held ones are left held

### Requirement: A held change is reviewed on one surface only

A held entry SHALL remain excluded from reconciliation diffs for as long as it is held, exactly as any entry with unsent local edits already is, so that one anime is never awaiting my decision on two review surfaces at once.

Once a held change has been accepted or declined the entry stops being pending, and a later reconciliation run SHALL compare it normally.

#### Scenario: A held entry raises no reconciliation difference
- **WHEN** reconciliation runs while an entry is held and MyAnimeList reports different values for that anime
- **THEN** the entry is excluded from that run's diff

#### Scenario: A decided entry reconciles normally
- **WHEN** reconciliation runs after that entry's held change has been accepted or declined
- **THEN** the entry is compared like any other

### Requirement: The review carries the context to judge each held change

Each held item SHALL be presented with enough to judge it on its own, without opening another page:

- **which anime** it is, named the way the rest of the app names anime;
- **what the unsent change was** — the individual changes recorded against that anime since the last time it synced successfully, described exactly as the app's own history describes them;
- **when it was made**, taken from those same records rather than from when the hold was applied;
- **the values that would be pushed** if it is accepted;
- **what MyAnimeList currently holds** for that anime, so a stale local value can be judged against what another device did.

The MyAnimeList side SHALL be **best-effort**: where it cannot be read, the item SHALL still be shown with its local side and SHALL say that the MyAnimeList side is unavailable, rather than being hidden or blocking the review.

Where an item accumulated many unsent changes, the review SHALL show the most recent of them and state how many further ones there are, rather than listing all of them.

#### Scenario: A held entry names what changed and when
- **WHEN** I review an entry held after I raised its episode count and then scored it
- **THEN** both changes are listed, described as the app's own history describes them, each with the moment it was made

#### Scenario: MyAnimeList's current values are shown beside mine
- **WHEN** I review a held entry whose anime MyAnimeList still lists
- **THEN** the values that would be pushed and the values MyAnimeList currently holds are both shown

#### Scenario: An unreadable MyAnimeList side does not hide the item
- **WHEN** MyAnimeList cannot be reached while the review is being read
- **THEN** each held item is still listed with its local side, marked as having no MyAnimeList side available

### Requirement: A held change MyAnimeList already agrees with clears itself

Where the values a held item would send are **already** the values MyAnimeList holds for that anime, there is nothing to send and nothing to decide. The item SHALL stop being pending and stop being held, without being presented for review and without anything being pushed.

This is the ordinary outcome of a push that reached MyAnimeList moments before the application stopped, leaving only the local flag uncleared. Presenting it would ask me to authorise a write that changes nothing.

Agreement SHALL be judged over the fields that would be pushed — status, episodes watched, score, start date, finish date, and rewatch count — using the same rewatch-preserving rule every other MyAnimeList read-back applies, so a local **Rewatching** entry against a remote `watching` counts as agreeing rather than differing. That rule SHALL be the one shared rule rather than restated for this path.

A **queued removal** agrees with MyAnimeList when MyAnimeList no longer lists the anime at all; such a removal SHALL likewise be discarded without review, its end state having already been reached.

**Any** difference SHALL keep the item held, including one where my stored episode count is *higher* than MyAnimeList's. A local value being further along is not evidence that it is newer, and it is precisely the case in which an automatic push would overwrite a correction made elsewhere. No difference SHALL ever resolve itself by pushing.

Clearing an item this way SHALL record no activity, since no stored value changed.

Where MyAnimeList's current value cannot be read, nothing SHALL clear: every item stays held, and the next reading of the review tries again.

#### Scenario: An entry MyAnimeList already agrees with is not presented
- **WHEN** a held entry's values are identical to what MyAnimeList currently holds for that anime
- **THEN** it stops being pending and held, is not listed for review, and nothing is pushed or recorded

#### Scenario: A rewatch pushed as watching counts as agreeing
- **WHEN** a held entry is locally Rewatching, MyAnimeList reports `watching`, and every other pushed field matches
- **THEN** the item is treated as agreeing and clears itself, staying Rewatching locally

#### Scenario: A locally higher episode count stays held
- **WHEN** a held entry reports 12 episodes watched and MyAnimeList reports 4
- **THEN** the item stays held and is presented for review rather than pushed

#### Scenario: A removal MyAnimeList has already applied is discarded
- **WHEN** a held removal's anime is no longer listed on MyAnimeList
- **THEN** the queued removal is discarded without review and nothing is pushed

#### Scenario: An unreadable MyAnimeList clears nothing
- **WHEN** MyAnimeList cannot be reached while the review is read
- **THEN** no item clears itself, and every held item is still held afterwards

### Requirement: Accepting a held change pushes it

Accepting a held change SHALL release its hold and push it to MyAnimeList immediately, without waiting out any debounce.

If that push fails, the item SHALL stay pending **without** its hold, so the ordinary retry job carries it — having explicitly accepted it, I SHALL NOT be asked about it again in the same session. If the application is closed before the accepted push lands, the next start SHALL hold it again, because it has crossed a process boundary again for the same reason as before.

Accepting SHALL act on one item at a time. An action that accepts every held item SHALL apply the same per-item rule in turn, and an item whose push fails SHALL NOT stop the remaining ones.

#### Scenario: Accepting pushes straight away
- **WHEN** I accept a held entry
- **THEN** its values are pushed to MyAnimeList immediately and it stops being pending on success

#### Scenario: A failed accept is retried automatically
- **WHEN** the push for an accepted entry fails because MyAnimeList is unreachable
- **THEN** the entry stays pending with no hold, and the retry job pushes it once MyAnimeList is reachable again

#### Scenario: Accepting one item leaves the others held
- **WHEN** I accept one of three held items
- **THEN** the other two are still held and still unpushed

#### Scenario: Accepting all continues past a failure
- **WHEN** I accept every held item and one of the pushes fails
- **THEN** the remaining items are still attempted, and the failed one stays pending

### Requirement: Declining a held change adopts MyAnimeList's current value

Declining a held change SHALL discard the unsent local change and take MyAnimeList's current value for that anime, so that local and MyAnimeList agree afterwards rather than diverging silently.

- Where MyAnimeList holds a list entry for the anime, its status, episodes watched, score, start date, finish date, and rewatch count SHALL be written onto the local entry, which SHALL then no longer be pending or held. The incoming status SHALL be resolved by the same rewatch-preserving rule every other MyAnimeList read-back applies, so a local Rewatching entry is not demoted by MyAnimeList's `watching`.
- Where MyAnimeList holds **no** entry for the anime, the held change was a local addition that never reached it. Adopting MyAnimeList's state means the local entry is **removed**, and no removal SHALL be queued for MyAnimeList, which is already in the desired state. The review SHALL state this outcome for that item **before** the decline is chosen.
- Where MyAnimeList's current value cannot be read, declining SHALL change nothing at all: the item stays held and pending, and the failure SHALL be reported. A decline SHALL never be applied on an assumption about the remote value.

Declining SHALL act on one item at a time, and an action that declines every held item SHALL apply the same per-item rule in turn.

#### Scenario: Declining takes MyAnimeList's values
- **WHEN** I decline a held entry and MyAnimeList reports a higher episode count for that anime
- **THEN** the local entry takes MyAnimeList's values and is no longer pending or held

#### Scenario: Declining does not demote a rewatch
- **WHEN** I decline a held entry that is locally Rewatching and MyAnimeList reports `watching`
- **THEN** the entry stays Rewatching and MyAnimeList's other values are applied

#### Scenario: Declining an entry MyAnimeList does not list
- **WHEN** I decline a held entry for an anime MyAnimeList has no list entry for
- **THEN** the local entry is removed, nothing is queued to be pushed, and the review said that this is what declining would do

#### Scenario: A decline that cannot read MyAnimeList changes nothing
- **WHEN** MyAnimeList cannot be reached as I decline a held entry
- **THEN** the entry is unchanged, still held and still pending, and the failure is reported

### Requirement: A held removal is accepted or declined the same way

A removal queued before the application started SHALL be held on the same rule as a held edit, and presented in the same review, distinguishable as a removal.

- **Accepting** SHALL release its hold and push the removal to MyAnimeList immediately, with the same failed-accept behaviour a held edit has.
- **Declining** SHALL discard the queued removal. Where MyAnimeList still lists the anime, the local entry SHALL be restored from MyAnimeList's current list status; where MyAnimeList no longer lists it, the anime SHALL simply stay absent locally, since local and MyAnimeList already agree.

#### Scenario: A removal queued across a restart is held
- **WHEN** the application starts with a removal still queued from a previous session
- **THEN** the removal is held for review and is not pushed

#### Scenario: Accepting a held removal deletes on MyAnimeList
- **WHEN** I accept a held removal
- **THEN** the removal is pushed to MyAnimeList immediately and the queued removal is cleared on success

#### Scenario: Declining a held removal restores the entry
- **WHEN** I decline a held removal and MyAnimeList still lists that anime
- **THEN** the queued removal is discarded and the local entry is restored from MyAnimeList's current values

#### Scenario: Declining a removal MyAnimeList already applied
- **WHEN** I decline a held removal for an anime MyAnimeList no longer lists
- **THEN** the queued removal is discarded and the anime stays absent locally

### Requirement: Deciding every held change runs in the background

Accepting every held change and declining every held change SHALL run in the background. The request that starts either SHALL return once it has started, and the run SHALL continue whether or not the page stays open.

They SHALL be **one job** between them. While either is starting or running:
- neither SHALL start
- no single held change SHALL be accepted or declined; such a request SHALL be refused without acting, saying that held changes are being decided

The run SHALL:
- report how many held changes it has decided, out of those held when it started
- apply the per-item rules of "Accepting a held change pushes it" and "Declining a held change adopts MyAnimeList's current value" unchanged
- end as **complete** when every one was decided
- end as **failed** when any stayed held, saying how many

#### Scenario: Accept all reports its progress
- **WHEN** I accept all with four changes held
- **THEN** its progress counts the changes decided out of four until it ends

#### Scenario: Decline all waits for accept all
- **WHEN** accept all is running and I press decline all
- **THEN** nothing new starts, and accept all carries on

#### Scenario: A single decision waits for the bulk run
- **WHEN** decline all is running and a request to accept one held change arrives
- **THEN** that request is refused without acting, saying held changes are being decided

#### Scenario: Items that stay held are counted
- **WHEN** accept all ends with one push that failed
- **THEN** it ends as failed, saying one of the held changes could not be applied

### Requirement: A fresh edit releases a hold

An edit I make to an anime whose change is currently held SHALL release that hold and push on the ordinary debounce, with no review. The edit is deliberate and current, and supersedes whatever stale value was being held.

#### Scenario: Editing a held anime pushes normally
- **WHEN** I change the episode count of an anime whose earlier change is held
- **THEN** the hold is released and the entry pushes on its normal debounce

#### Scenario: The released item leaves the review
- **WHEN** I look at the review after making that edit
- **THEN** that anime is no longer listed as held

### Requirement: Full reconciliation computes a reviewable diff
The system SHALL provide a full reconciliation (scheduled weekly and manually triggerable) that pulls the complete list from MAL, diffs it against local data for entries where `pending_sync = false`, and holds the resulting differences for review rather than applying them immediately. Entries with `pending_sync = true` at diff time SHALL be excluded from that run's diff and revisited on a future reconciliation run.

An anime with a pending removal SHALL likewise be excluded from the diff, even though it has no local entry at all: MAL still listing an anime whose removal has not yet been pushed is an expected in-flight state, not a difference to review, and including it would offer to restore the entry the user just deleted.

A **manually started run** SHALL run in the background, starting once in the way `background-jobs` requires. It SHALL report how many anime it has read from my MyAnimeList list so far. MyAnimeList does not say how long the list is, so the run SHALL report no total. Comparing what was read against local data makes no MyAnimeList calls, and SHALL NOT be reported as a separate stage.

The **scheduled weekly run** SHALL NOT report progress anywhere a manual run does. It SHALL record, kept across restarts:
- when it last ran
- whether that run failed
- the reason, when it did

A run that was interrupted before it ended SHALL record neither outcome.

A manual run and the weekly run SHALL never compute a diff at the same time. Whichever starts second SHALL wait for the other to finish, so exactly one diff is ever held.

#### Scenario: Reconciliation computes a diff without applying it
- **WHEN** full reconciliation runs and the MAL list differs from local data
- **THEN** the differences are identified and stored for review, and no local data changes yet

#### Scenario: In-flight edits are excluded
- **WHEN** an entry has `pending_sync = true` at the time reconciliation computes its diff
- **THEN** that entry is excluded from this run's diff and is compared again on a future reconciliation run

#### Scenario: In-flight removals are excluded
- **WHEN** reconciliation runs while an anime's removal is still pending and MAL still lists that anime
- **THEN** the anime is excluded from this run's diff rather than proposed as an addition

#### Scenario: Removal reconciles normally once pushed
- **WHEN** reconciliation runs after a removal has been pushed successfully
- **THEN** MAL no longer lists that anime, so nothing about it appears in the diff

#### Scenario: A manual run reports what it has read
- **WHEN** I start a full reconciliation and my MyAnimeList list is being paged
- **THEN** its progress counts the anime read so far, with no total, and it ends as complete once the diff is stored

#### Scenario: The weekly run reports nowhere
- **WHEN** the weekly run computes a diff
- **THEN** no progress is reported for it, and the manual reconciliation's state is unchanged

#### Scenario: A failed weekly run is recorded
- **WHEN** the weekly run fails because MyAnimeList cannot be reached, and the application later restarts
- **THEN** it is still recorded that the weekly run failed, when, and why

#### Scenario: Overlapping runs hold one diff
- **WHEN** I start a manual reconciliation while the weekly run is computing its diff
- **THEN** the manual run waits for the weekly one to finish, and afterwards exactly one diff is held for review

### Requirement: Reconciliation review with accept/cancel
The system SHALL surface the most recent reconciliation run's pending diff on the settings page with an Accept action that applies every difference in that diff, and a Cancel action that discards the diff without applying anything.

#### Scenario: Reviewing a pending diff
- **WHEN** a reconciliation run has produced a diff awaiting review
- **THEN** the settings page shows those differences along with Accept and Cancel actions

#### Scenario: Accepting the diff
- **WHEN** I choose Accept
- **THEN** every difference in that run's diff is applied to local data

#### Scenario: Cancelling the diff
- **WHEN** I choose Cancel
- **THEN** none of that run's differences are applied, and the next reconciliation run computes a fresh diff

#### Scenario: No diff awaiting review
- **WHEN** no reconciliation diff is currently pending review
- **THEN** the settings page shows nothing to review

### Requirement: Rewatching is pushed to MyAnimeList as watching

MyAnimeList's status values are `watching`, `completed`, `on_hold`, `dropped`, and `plan_to_watch`; it has no rewatching status. The system SHALL therefore push a Rewatching entry as **`watching`**, sending its episodes watched unchanged, so the rewatch reads on MyAnimeList as a run in progress and its episode figure matches the local one at every point.

Every locally defined status SHALL have a mapping to a MyAnimeList status value. A status with no mapping SHALL NOT be allowed to reach the push path, since an unmapped value would fail the sync of that entry rather than degrade.

#### Scenario: A rewatch is pushed as watching

- **WHEN** a Rewatching entry with 3 episodes watched is pushed to MyAnimeList
- **THEN** its status is sent as `watching` and its episode count as 3

#### Scenario: Rewatch count is still pushed

- **WHEN** a rewatch finishes and the entry returns to Completed with its rewatch count increased
- **THEN** the push sends `completed` together with the new rewatch count

#### Scenario: Every status maps

- **WHEN** an entry of any locally defined status is pushed
- **THEN** a MyAnimeList status value is sent for it, and no status causes the push to fail for lack of a mapping

### Requirement: Reconciliation does not demote a rewatch

Because a Rewatching entry is pushed as `watching`, MyAnimeList reports it back as `watching`. Reconciliation SHALL treat a local **Rewatching** entry as **matching** a remote `watching` status, and SHALL NOT record a difference or rewrite the entry to Watching on that basis.

The rule SHALL hold across the whole reconciliation path, not the comparison alone:

- **Comparing.** A local Rewatching entry against a remote `watching` status SHALL NOT itself constitute a difference.
- **Recording.** Where a difference is raised for that entry by some *other* field — its episode count, score, dates, or rewatch count edited on MyAnimeList's own site — the difference held for review SHALL record the entry's status as **Rewatching**, not as the `watching` MyAnimeList reported. The review screen SHALL therefore show the status the entry will actually hold once accepted.
- **Applying.** Accepting a held difference SHALL re-check the entry as it stands at that moment: where the entry is Rewatching and the difference carries Watching, the entry SHALL stay Rewatching. A difference computed before an entry became a rewatch SHALL NOT demote it when accepted afterwards.

This SHALL constrain the status only. A remote status that is anything other than `watching` SHALL be diffed, recorded, and applied normally, so a change genuinely made on MyAnimeList still reaches the local entry. Every other field — episodes watched, score, dates, rewatch count — SHALL be compared and applied exactly as it is today.

Without this rule the first reconciliation after a rewatch is pushed would silently replace the user's Rewatching entry with Watching, destroying the distinction the status exists to hold.

#### Scenario: A rewatch survives reconciliation

- **WHEN** reconciliation compares a local Rewatching entry against a MyAnimeList entry reporting `watching`
- **THEN** no status difference is recorded and the entry stays Rewatching

#### Scenario: A difference raised by another field records Rewatching

- **WHEN** a local Rewatching entry's episode count was changed on MyAnimeList's own site, so reconciliation raises a difference for it while MyAnimeList reports its status as `watching`
- **THEN** the difference held for review records the status as Rewatching, and the review screen shows Rewatching

#### Scenario: Accepting that difference keeps the rewatch

- **WHEN** I accept a difference that carries the new episode count for that Rewatching entry
- **THEN** the episode count is applied and the entry's status is still Rewatching

#### Scenario: A stale difference does not demote a newer rewatch

- **WHEN** a difference was computed while an entry was Watching, the entry has since become Rewatching, and I then accept the difference
- **THEN** the entry stays Rewatching, and the difference's other fields are applied as usual

#### Scenario: A real remote change still applies

- **WHEN** reconciliation compares a local Rewatching entry against a MyAnimeList entry reporting `dropped`
- **THEN** the difference is recorded and applied like any other remote status change

#### Scenario: Other fields still diff

- **WHEN** a local Rewatching entry and its MyAnimeList copy differ in episodes watched or score
- **THEN** those differences are recorded exactly as they would be for any other status

### Requirement: A corrective re-sync does not demote a rewatch

The corrective re-sync re-reads every MyAnimeList list entry and applies it to local data immediately, without review. It SHALL apply the same rewatch rule reconciliation applies: where the local entry is **Rewatching** and MyAnimeList reports `watching`, the entry SHALL stay **Rewatching**.

The rule SHALL be the one rule, shared, rather than restated per path — every path that reads a MyAnimeList list status onto an entry that already exists locally SHALL resolve the incoming status through it, so no future read-back path can be added that demotes a rewatch by omission.

An entry being created for the first time from MyAnimeList data has no local status to preserve, so the rule SHALL have no effect there: the incoming status is used as reported.

Every other field the re-sync writes — episodes watched, score, dates, rewatch count — SHALL be applied exactly as it is today, and every other remote status SHALL be applied as reported.

#### Scenario: A rewatch survives a corrective re-sync

- **WHEN** a corrective re-sync reads a local Rewatching entry's MyAnimeList copy, which reports `watching`
- **THEN** the entry's status is still Rewatching after the run, and no status change is written to the activity log

#### Scenario: The re-sync still applies other fields to a rewatch

- **WHEN** that same entry's episode count differs between MyAnimeList and local data
- **THEN** the MyAnimeList episode count is applied and the status stays Rewatching

#### Scenario: A real remote change still applies during a re-sync

- **WHEN** a corrective re-sync reads a local Rewatching entry whose MyAnimeList copy reports `completed`
- **THEN** the entry becomes Completed, exactly as any other remote status change is applied

#### Scenario: A newly imported entry is unaffected

- **WHEN** a corrective re-sync encounters a MyAnimeList entry with no local counterpart and MyAnimeList reports `watching`
- **THEN** the entry is created as Watching

