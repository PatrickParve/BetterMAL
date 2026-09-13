## MODIFIED Requirements

### Requirement: Full reconciliation computes a reviewable diff
The system SHALL provide a full reconciliation (scheduled weekly and manually triggerable) that pulls the complete list from MAL, diffs it against local data for entries where `pending_sync = false`, and holds the resulting differences for review rather than applying them immediately. Entries with `pending_sync = true` at diff time SHALL be excluded from that run's diff and revisited on a future reconciliation run.

A difference SHALL be one of:
- an **addition**: an anime MAL lists that has no local entry
- an **update**: an anime whose local entry differs from what MAL lists
- a **removal**: a local entry for an anime MAL no longer lists (see "Reconciliation proposes removing anime MyAnimeList no longer lists")

An anime with a pending removal SHALL likewise be excluded from the diff, even though it has no local entry at all: MAL still listing an anime whose removal has not yet been pushed is an expected in-flight state, not a difference to review, and including it would offer to restore the entry the user just deleted.

An anime MAL lists with a list status the app does not recognize SHALL be left out of the run (see "A MyAnimeList list status the app does not recognize is never guessed"). The rest of the run SHALL still compute and store its diff. A run that left any anime out SHALL NOT be reported as clean:
- a manually started run SHALL end as failed, saying how many anime it left out
- the weekly run SHALL record that it failed, with the same reason

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
- **THEN** its progress counts the anime read so far, with no total, and, where it left no anime out, it ends as complete once the diff is stored

#### Scenario: A manual run that left anime out ends as failed
- **WHEN** a manual reconciliation reads 600 anime and leaves 1 out because MyAnimeList gave it a list status the app does not recognize
- **THEN** its diff of the other anime is still stored for review, and the run ends as failed, saying that 1 anime was left out

#### Scenario: The weekly run reports nowhere
- **WHEN** the weekly run computes a diff
- **THEN** no progress is reported for it, and the manual reconciliation's state is unchanged

#### Scenario: A failed weekly run is recorded
- **WHEN** the weekly run fails because MyAnimeList cannot be reached, and the application later restarts
- **THEN** it is still recorded that the weekly run failed, when, and why

#### Scenario: A weekly run that left anime out is recorded as failed
- **WHEN** the weekly run leaves an anime out because MyAnimeList gave it a list status the app does not recognize
- **THEN** its diff of the other anime is still stored, and it is recorded that the weekly run failed, saying how many anime were left out

#### Scenario: Overlapping runs hold one diff
- **WHEN** I start a manual reconciliation while the weekly run is computing its diff
- **THEN** the manual run waits for the weekly one to finish, and afterwards exactly one diff is held for review

## ADDED Requirements

### Requirement: Reconciliation proposes removing anime MyAnimeList no longer lists
Full reconciliation SHALL propose removing each local list entry whose anime is missing from MAL's list as that run read it. This covers an anime I deleted on MyAnimeList's own site or in another client. The proposal SHALL be held in the same diff as additions and updates. The review SHALL show it marked as removed on MyAnimeList, with the values the entry has locally.

A local entry SHALL NOT be proposed for removal when:
- it has `pending_sync = true`, which includes an entry held for review. Pushing that edit is what decides MyAnimeList's state.
- its anime has a pending removal, whether that removal is waiting to be pushed or held for review
- MAL's list did include the anime, but the run left it out because of a list status the app does not recognize
- it was pushed to MyAnimeList after the run began reading MAL's list, since the list the run read is older than that push

**Accepting** the diff SHALL delete each such local entry. It SHALL NOT queue a pending removal or send anything to MyAnimeList, since MyAnimeList is already in that state. Like every other difference an accepted diff applies, it SHALL record nothing in the activity log (see `activity-recording`, "The sync paths record nothing").

When the diff is accepted, an entry SHALL be left as it is if, since the diff was computed, it:
- has gained an unsent local edit, or
- has been pushed to MyAnimeList.

**Cancelling** the diff SHALL leave the entry as it is. A later run SHALL propose the removal again for as long as MAL still does not list the anime and none of the exclusions above apply.

#### Scenario: An anime deleted on MyAnimeList's site is proposed for removal
- **WHEN** I deleted an anime from my list on MyAnimeList's website, and reconciliation runs
- **THEN** the diff holds a removal for that anime, shown with its local values and marked as removed on MyAnimeList, and my local entry is unchanged until I review it

#### Scenario: Accepting a removal deletes it locally only
- **WHEN** I accept a diff holding a removal
- **THEN** the local entry is deleted, no removal is queued or sent to MyAnimeList, and nothing is recorded in the activity log

#### Scenario: Cancelling keeps the entry
- **WHEN** I cancel a diff holding a removal
- **THEN** the local entry stays exactly as it was

#### Scenario: An unsent edit is not proposed for removal
- **WHEN** reconciliation runs while an entry MAL does not list has `pending_sync = true`
- **THEN** no removal is proposed for it

#### Scenario: A removal of my own is not proposed again
- **WHEN** I removed an anime in this app and its removal has not been pushed yet
- **THEN** reconciliation proposes nothing about that anime

#### Scenario: An anime left out for its status is not proposed for removal
- **WHEN** MAL lists an anime I have a local entry for, with a list status the app does not recognize
- **THEN** no removal is proposed for it, and its entry stays as it is

#### Scenario: An entry pushed while the list is being read is not proposed
- **WHEN** I add an anime in this app and its push reaches MyAnimeList while reconciliation is still reading my MAL list
- **THEN** no removal is proposed for it

#### Scenario: An edit made after the diff keeps the entry
- **WHEN** a diff holds a removal, and before accepting it I edit that anime in this app, whether or not the edit has reached MyAnimeList yet
- **THEN** accepting the diff leaves that entry as it is and applies the diff's other differences

### Requirement: A MyAnimeList list status the app does not recognize is never guessed
The app SHALL recognize exactly MyAnimeList's list statuses `watching`, `completed`, `on_hold`, `dropped` and `plan_to_watch`. Where MyAnimeList reports an anime on my list with any other status value, the app SHALL NOT map it to a guessed status, and SHALL NOT let it stop work on any other anime. It SHALL log a warning naming the anime and the status value MyAnimeList gave, so the new status can be added to the app by hand.

- **Reconciliation** SHALL leave that anime out of the run:
  - no addition, update or removal proposed for it
  - no metadata cached for it
  - nothing sent to MyAnimeList

  The run is then reported as described in "Full reconciliation computes a reviewable diff".
- **The held-change review** SHALL treat MyAnimeList's side of that item as unreadable, exactly as when the read fails. The item SHALL be shown with its local side and a note that MyAnimeList's value is unavailable, and SHALL NOT be cleared as already agreeing.
- **Declining** a held change or a held removal for that anime SHALL change nothing, and SHALL be reported as a decline that could not read MyAnimeList's current value, exactly as when the read fails. Declining every held change SHALL continue past it and count it as still held.

#### Scenario: Reconciliation leaves out only that anime
- **WHEN** MAL reports one anime with a list status the app does not recognize, and other anime differ from local data
- **THEN** that anime is left out, a warning names it and the status MAL gave, and the other differences are still stored for review

#### Scenario: The held-change review still loads
- **WHEN** I open the held-change review and MyAnimeList reports one held item's anime with a list status the app does not recognize
- **THEN** the review loads every held item, and that item shows its local side with MyAnimeList's value marked unavailable, and stays held

#### Scenario: Declining such an item changes nothing
- **WHEN** I decline a held change whose anime MyAnimeList reports with a list status the app does not recognize
- **THEN** nothing changes locally, the item stays held, and the decline reports that MyAnimeList's current value could not be read

#### Scenario: Declining every held change continues past it
- **WHEN** I decline every held change and one item's anime has a list status the app does not recognize
- **THEN** every other item is declined, and that item is counted as still held
