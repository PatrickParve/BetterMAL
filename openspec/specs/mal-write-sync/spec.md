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

#### Scenario: Push failure is retried
- **WHEN** a push fails
- **THEN** the entry stays `pending_sync = true` and a background retry job later attempts it again

### Requirement: Manual sync now
The system SHALL provide a manual "sync now" action that processes the pending queue immediately, touching only pending/changed entries and never the whole list. Pending removals SHALL be processed by the same action, alongside pending edits.

#### Scenario: Immediate flush of pending entries
- **WHEN** the user triggers "sync now"
- **THEN** all currently pending entries are pushed immediately without waiting out their debounce, and non-pending entries are untouched

#### Scenario: Pending removals flush too
- **WHEN** the user triggers "sync now" while a removal is pending
- **THEN** that removal is pushed to MAL in the same run

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

#### Scenario: Removal push failure is retried
- **WHEN** a removal push fails
- **THEN** the pending removal is retained and a background retry job later attempts it again

#### Scenario: Removal survives a restart
- **WHEN** the application restarts with a removal still pending
- **THEN** the pending removal is still recorded and is retried

#### Scenario: Local list is unaffected by a failed push
- **WHEN** a removal's push to MAL has not yet succeeded
- **THEN** the anime is still absent from my list locally

### Requirement: Full reconciliation computes a reviewable diff
The system SHALL provide a full reconciliation (scheduled weekly and manually triggerable) that pulls the complete list from MAL, diffs it against local data for entries where `pending_sync = false`, and holds the resulting differences for review rather than applying them immediately. Entries with `pending_sync = true` at diff time SHALL be excluded from that run's diff and revisited on a future reconciliation run.

An anime with a pending removal SHALL likewise be excluded from the diff, even though it has no local entry at all: MAL still listing an anime whose removal has not yet been pushed is an expected in-flight state, not a difference to review, and including it would offer to restore the entry the user just deleted.

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

