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
The system SHALL provide a manual "sync now" action that processes the pending queue immediately, touching only pending/changed entries and never the whole list.

#### Scenario: Immediate flush of pending entries
- **WHEN** the user triggers "sync now"
- **THEN** all currently pending entries are pushed immediately without waiting out their debounce, and non-pending entries are untouched

### Requirement: Full reconciliation computes a reviewable diff
The system SHALL provide a full reconciliation (scheduled weekly and manually triggerable) that pulls the complete list from MAL, diffs it against local data for entries where `pending_sync = false`, and holds the resulting differences for review rather than applying them immediately. Entries with `pending_sync = true` at diff time SHALL be excluded from that run's diff and revisited on a future reconciliation run.

#### Scenario: Reconciliation computes a diff without applying it
- **WHEN** full reconciliation runs and the MAL list differs from local data
- **THEN** the differences are identified and stored for review, and no local data changes yet

#### Scenario: In-flight edits are excluded
- **WHEN** an entry has `pending_sync = true` at the time reconciliation computes its diff
- **THEN** that entry is excluded from this run's diff and is compared again on a future reconciliation run

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

