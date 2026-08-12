## ADDED Requirements

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

## MODIFIED Requirements

### Requirement: Manual sync now
The system SHALL provide a manual "sync now" action that processes the pending queue immediately, touching only pending/changed entries and never the whole list. Pending removals SHALL be processed by the same action, alongside pending edits.

#### Scenario: Immediate flush of pending entries
- **WHEN** the user triggers "sync now"
- **THEN** all currently pending entries are pushed immediately without waiting out their debounce, and non-pending entries are untouched

#### Scenario: Pending removals flush too
- **WHEN** the user triggers "sync now" while a removal is pending
- **THEN** that removal is pushed to MAL in the same run

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
