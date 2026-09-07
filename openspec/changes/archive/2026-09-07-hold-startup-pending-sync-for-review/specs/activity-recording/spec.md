## MODIFIED Requirements

### Requirement: Every path that changes my list records what it changed

The system SHALL record an entry in the activity log wherever a change reaches a stored list entry, whichever path applied it. The paths SHALL be: my own edits and removals made in the app, the automatic reopening of a completed entry whose anime has aired further episodes, the background import of anime found on MyAnimeList but missing locally, the acceptance of a reconciliation diff, the corrective re-sync, and the **declining of a change held for review**, which discards an unsent local change by applying MyAnimeList's current value in its place.

Recording SHALL happen at the point the change is applied to the stored entry, not at the point a change is proposed or reviewed. A proposed change that is never applied — a reconciliation diff that is declined, or one skipped because the entry has unpushed local edits — SHALL record nothing.

Two reviews use the word *decline* for opposite outcomes, and the rule follows what is applied rather than what the action is called. Declining a **reconciliation diff** applies nothing to my list and SHALL record nothing. Declining a **change held for review** applies MyAnimeList's current value to a stored entry, and SHALL therefore be recorded exactly like any other application — one record per field it actually moved, none where it moved nothing. **Accepting** a held change pushes my stored values outward without altering them, so it SHALL record nothing.

**Granularity.** A change SHALL be recorded at the same granularity whichever path applied it:

- An entry that did not exist locally SHALL be recorded as a single **addition**, naming the status it arrived with, rather than as one record per field it arrived carrying.
- An entry that already existed SHALL be recorded as **one record per field that actually changed** — status, episodes watched, score, start date, finish date, or rewatch count — established by comparing the entry's stored values against the values being applied. A field whose value is rewritten to what it already held SHALL record nothing, and an application in which no field changed SHALL record nothing at all.
- A change that leaves an entry **Completed** when it was not before SHALL be recorded as a **completion**, not as a generic status change, so that it reads as a completion on every surface that distinguishes the two. Every other status change SHALL be recorded as a status change, naming the status it moved from and to.
- An episode-count change SHALL be recorded together with the count the entry held before it, so a reader can tell an increase from a decrease without reconstructing it from surrounding records.

Each record SHALL carry the same human-readable description of the change whichever path produced it, so that every surface reading the log describes a change in one way regardless of origin.

**Ordering within one application.** Where applying one entry's change produces several records at the same moment, they SHALL be ordered so that surfaces reading the log treat them exactly as they treat the equivalent local edit — in particular, a completion and a score applied together SHALL be recorded in the order that lets a reading surface report them as one completion carrying a score.

**Recording changes nothing.** Recording SHALL NOT alter which changes a path applies, in what order, or under what conditions, and SHALL NOT mark any entry as having changes to push to MyAnimeList. A record SHALL never itself become a change pushed back to MyAnimeList.

#### Scenario: An anime imported from MyAnimeList is recorded

- **WHEN** the background import finds an anime on MyAnimeList that has no local entry and creates one
- **THEN** the activity log holds one addition for that anime, naming the status it arrived with

#### Scenario: An accepted diff records each field it moved

- **WHEN** I accept a reconciliation diff whose entry raises one anime's episodes watched and changes its score, leaving its dates as they are
- **THEN** the activity log holds one episode-progress record and one score record for that anime, and no record for its dates

#### Scenario: An application that changes nothing records nothing

- **WHEN** a path applies values to an entry that already holds every one of them
- **THEN** no activity is recorded for that entry

#### Scenario: A declined diff records nothing

- **WHEN** a pending reconciliation diff is declined
- **THEN** no activity is recorded for any anime it covered

#### Scenario: A skipped entry records nothing

- **WHEN** a reconciliation diff is accepted but one of its entries is skipped because that entry has local edits not yet pushed
- **THEN** no activity is recorded for that anime

#### Scenario: A completion applied from MyAnimeList reads as a completion

- **WHEN** a sync applies a status of Completed to an entry that was not completed
- **THEN** the change is recorded as a completion rather than as a generic status change

#### Scenario: Another status change applied from MyAnimeList

- **WHEN** a sync moves an entry from Watching to Dropped
- **THEN** the change is recorded as a status change naming Watching and Dropped

#### Scenario: An episode change records what it moved from

- **WHEN** a sync raises an entry's episodes watched from 3 to 7
- **THEN** the record reports episode 7 and carries 3 as the count held before it

#### Scenario: A completion and a score applied together

- **WHEN** a sync applies both a completion and a score to the same entry in one application
- **THEN** the records are ordered so that a surface reading them reports one completion carrying that score, exactly as it does for a completion and score I saved together myself

#### Scenario: Recording never pushes to MyAnimeList

- **WHEN** any sync path records what it applied
- **THEN** no entry is marked as having changes to push, and nothing is sent to MyAnimeList as a result of the recording

#### Scenario: A declined held change records what it applied

- **WHEN** I decline a change held for review and MyAnimeList's current value raises that entry's episodes watched and clears its score
- **THEN** the activity log holds one episode-progress record and one score record for that anime

#### Scenario: A declined held change that moves nothing records nothing

- **WHEN** I decline a change held for review and MyAnimeList already holds every value the local entry holds
- **THEN** no activity is recorded for that anime

#### Scenario: Declining a held change for an anime MyAnimeList does not list

- **WHEN** I decline a change held for review and MyAnimeList holds no entry for that anime, so the local entry is removed
- **THEN** the activity log holds one removal for that anime

#### Scenario: Accepting a held change records nothing

- **WHEN** I accept a change held for review and it is pushed to MyAnimeList
- **THEN** no activity is recorded, because nothing about the stored entry changed

### Requirement: A record names where the change came from

Every activity record SHALL carry the origin of the change it describes. The origins SHALL be: my own editing in the app, the background import, an accepted reconciliation diff, the corrective re-sync, and a declined change held for review.

Records written before origins were recorded SHALL count as my own editing, since every path that wrote them was one of my own edits.

The origin SHALL be recorded at this granularity even where a surface presents the MyAnimeList-side origins as one, so that which sync path applied a change remains answerable from the log itself. A declined held change SHALL carry an origin of its own rather than reusing the accepted-reconciliation-diff one: both write MyAnimeList's values onto an entry, and the log SHALL stay able to say which surface I did it from.

The origin SHALL describe where a change came from, and SHALL NOT be used to decide whether the change is applied, kept, or pushed.

#### Scenario: A sync record names its path

- **WHEN** the corrective re-sync records a change
- **THEN** that record's origin is the corrective re-sync, distinguishable from a change recorded by the background import or by an accepted reconciliation diff

#### Scenario: My own edit names me

- **WHEN** I edit an entry in the app
- **THEN** the record's origin is my own editing

#### Scenario: Existing history keeps reading correctly

- **WHEN** the activity log holds records written before origins were recorded
- **THEN** those records read as my own editing rather than as unknown or as coming from MyAnimeList

#### Scenario: A declined held change names its own origin

- **WHEN** declining a change held for review records what it applied
- **THEN** that record's origin is the declined held change, distinguishable from an accepted reconciliation diff, the background import, and the corrective re-sync

#### Scenario: It still reads as coming from MyAnimeList

- **WHEN** a surface that groups the MyAnimeList-side origins together displays that record
- **THEN** it reads as coming from MyAnimeList rather than as my own editing
