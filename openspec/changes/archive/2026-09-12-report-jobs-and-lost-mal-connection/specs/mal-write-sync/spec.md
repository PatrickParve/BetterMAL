## MODIFIED Requirements

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

## ADDED Requirements

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
