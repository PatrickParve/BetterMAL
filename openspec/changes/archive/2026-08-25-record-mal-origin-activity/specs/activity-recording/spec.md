## ADDED Requirements

### Requirement: Every path that changes my list records what it changed

The system SHALL record an entry in the activity log wherever a change reaches a stored list entry, whichever path applied it. The paths SHALL be: my own edits and removals made in the app, the automatic reopening of a completed entry whose anime has aired further episodes, the background import of anime found on MyAnimeList but missing locally, the acceptance of a reconciliation diff, and the corrective re-sync.

Recording SHALL happen at the point the change is applied to the stored entry, not at the point a change is proposed or reviewed. A proposed change that is never applied — a reconciliation diff that is declined, or one skipped because the entry has unpushed local edits — SHALL record nothing.

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

### Requirement: A record names where the change came from

Every activity record SHALL carry the origin of the change it describes. The origins SHALL be: my own editing in the app, the background import, an accepted reconciliation diff, and the corrective re-sync.

Records written before origins were recorded SHALL count as my own editing, since every path that wrote them was one of my own edits.

The origin SHALL be recorded at this granularity even where a surface presents the three sync origins as one, so that which sync path applied a change remains answerable from the log itself.

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

### Requirement: The import that establishes the baseline records nothing

An import that runs while **no activity has been recorded at all** SHALL record nothing. Such a run is establishing what my list already is rather than reporting things that happened, and recording it would fill the history with one addition per anime, all sharing a single moment, before any activity worth reading exists.

The judgement SHALL be made once per run, from the state at the run's start, so a change recorded while a long import is in flight does not make that same import start recording halfway through.

Because the judgement is "nothing has been recorded yet" rather than "this is the first run", it SHALL hold across interruption: a baseline import interrupted partway and resumed still records nothing, for as long as nothing else has recorded anything in between.

Every import that runs once history exists SHALL record normally, so an anime added on MyAnimeList's own site and picked up by a later import appears as an addition.

#### Scenario: A first import writes no history

- **WHEN** the list is imported for the first time, with no activity recorded before it
- **THEN** the activity log holds no records for the anime it imported

#### Scenario: An interrupted first import writes no history either

- **WHEN** a first import is interrupted partway and resumes on the next restart, with nothing recorded in between
- **THEN** the anime imported by the resumed run are not recorded either

#### Scenario: An edit during the baseline import does not start it recording

- **WHEN** I edit an entry while a baseline import is still running
- **THEN** the rest of that import's anime are still not recorded

#### Scenario: A later import is recorded

- **WHEN** an anime is added on MyAnimeList's own site after my history has begun, and a later import creates its entry
- **THEN** that anime is recorded as an addition
