## ADDED Requirements

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

### Requirement: A fresh edit releases a hold

An edit I make to an anime whose change is currently held SHALL release that hold and push on the ordinary debounce, with no review. The edit is deliberate and current, and supersedes whatever stale value was being held.

#### Scenario: Editing a held anime pushes normally
- **WHEN** I change the episode count of an anime whose earlier change is held
- **THEN** the hold is released and the entry pushes on its normal debounce

#### Scenario: The released item leaves the review
- **WHEN** I look at the review after making that edit
- **THEN** that anime is no longer listed as held

## MODIFIED Requirements

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

#### Scenario: Immediate flush of pending entries
- **WHEN** the user triggers "sync now"
- **THEN** all currently pending entries are pushed immediately without waiting out their debounce, and non-pending entries are untouched

#### Scenario: Pending removals flush too
- **WHEN** the user triggers "sync now" while a removal is pending
- **THEN** that removal is pushed to MyAnimeList in the same run

#### Scenario: Held items are not flushed
- **WHEN** the user triggers "sync now" while some items are held for review
- **THEN** those items are not pushed and stay held

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
