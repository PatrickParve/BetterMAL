## RENAMED Requirements

- FROM: `### Requirement: Every path that changes my list records what it changed`
- TO: `### Requirement: Every change made in the app records what it changed`

- FROM: `### Requirement: The import that establishes the baseline records nothing`
- TO: `### Requirement: The sync paths record nothing`

## MODIFIED Requirements

### Requirement: Every change made in the app records what it changed

The system SHALL record an entry in the activity log wherever a change made in this app reaches a stored list entry. The changes SHALL be: my own edits and removals made in the app; the automatic completion of a caught-up entry once its full run is known; the automatic reopening of a completed entry whose anime is still to come; and the **declining of a change held for review**, which discards an unsent local change by applying MyAnimeList's current value in its place.

The activity log SHALL hold nothing else. Each device's log therefore records only what was done on that device, so the logs of two devices can be combined without the same change appearing twice.

Declining a held change SHALL be recorded even though what it applies is MyAnimeList's value. It undoes an edit of mine that only this device knows was made, and where MyAnimeList holds no entry for the anime it removes that anime from my list, which SHALL NOT happen without a record.

Recording SHALL happen at the point the change is applied to the stored entry, not at the point a change is proposed or reviewed.

Two reviews use the word *decline* for opposite outcomes, and the rule follows what is applied rather than what the action is called. Declining a **reconciliation diff** applies nothing to my list and SHALL record nothing. Declining a **change held for review** applies MyAnimeList's current value to a stored entry, and SHALL therefore be recorded like any other change made in the app — one record per field it actually moved, none where it moved nothing. **Accepting** a held change pushes my stored values outward without altering them, so it SHALL record nothing. **Accepting** a reconciliation diff applies MyAnimeList's values through a sync path and SHALL record nothing, per "The sync paths record nothing".

**Granularity.** A change SHALL be recorded at the same granularity whichever of these changes produced it:

- An entry that did not exist locally SHALL be recorded as a single **addition**, naming the status it arrived with, rather than as one record per field it arrived carrying.
- An entry that already existed SHALL be recorded as **one record per field that actually changed** — status, episodes watched, score, start date, finish date, or rewatch count — established by comparing the entry's stored values against the values being applied. A field whose value is rewritten to what it already held SHALL record nothing, and an application in which no field changed SHALL record nothing at all.
- A change that leaves an entry **Completed** when it was not before SHALL be recorded as a **completion**, not as a generic status change, so that it reads as a completion on every surface that distinguishes the two. Every other status change SHALL be recorded as a status change, naming the status it moved from and to.
- An episode-count change SHALL be recorded together with the count the entry held before it, so a reader can tell an increase from a decrease without reconstructing it from surrounding records.

Each record SHALL carry the same human-readable description of the change whichever of these changes produced it, so that every surface reading the log describes a change in one way.

**Ordering within one application.** Where applying one entry's change produces several records at the same moment, they SHALL be ordered so that surfaces reading the log treat them exactly as they treat the equivalent edit of my own — in particular, a completion and a score applied together SHALL be recorded in the order that lets a reading surface report them as one completion carrying a score.

**Recording changes nothing.** Recording SHALL NOT alter which changes are applied, in what order, or under what conditions, and SHALL NOT mark any entry as having changes to push to MyAnimeList. A record SHALL never itself become a change pushed back to MyAnimeList.

#### Scenario: My own edit records each field it moved

- **WHEN** I save an edit that raises one anime's episodes watched and changes its score, leaving its dates as they are
- **THEN** the activity log holds one episode-progress record and one score record for that anime, and no record for its dates

#### Scenario: An edit that changes nothing records nothing

- **WHEN** I save an entry with values it already holds
- **THEN** no activity is recorded for that entry

#### Scenario: Removing an anime records a removal

- **WHEN** I remove an anime from my list in the app
- **THEN** the activity log holds one removal for that anime

#### Scenario: An automatic completion is recorded

- **WHEN** a Watching entry is completed because it has reached its anime's known total and that anime has aired in full
- **THEN** the activity log holds one completion for that anime

#### Scenario: An automatic reopening is recorded

- **WHEN** a Completed entry is returned to Watching because its anime is still airing and the entry has not watched the known total
- **THEN** the activity log holds one status change for that anime naming Completed and Watching

#### Scenario: A declined held change records what it applied

- **WHEN** I decline a change held for review and MyAnimeList's current value raises that entry's episodes watched and clears its score
- **THEN** the activity log holds one episode-progress record and one score record for that anime

#### Scenario: A declined held change that moves nothing records nothing

- **WHEN** I decline a change held for review and MyAnimeList already holds every value the local entry holds
- **THEN** no activity is recorded for that anime

#### Scenario: Declining a held change for an anime MyAnimeList does not list

- **WHEN** I decline a change held for review and MyAnimeList holds no entry for that anime, so the local entry is removed
- **THEN** the activity log holds one removal for that anime

#### Scenario: Declining a held removal for an anime MyAnimeList still lists

- **WHEN** I decline a held removal and MyAnimeList still lists that anime, so the local entry is restored from MyAnimeList's current value
- **THEN** the activity log holds one addition for that anime, naming the status it was restored with

#### Scenario: A completion applied by declining reads as a completion

- **WHEN** declining a held change applies a status of Completed to an entry that was not completed
- **THEN** the change is recorded as a completion rather than as a generic status change

#### Scenario: Another status change applied by declining

- **WHEN** declining a held change moves an entry from Watching to Dropped
- **THEN** the change is recorded as a status change naming Watching and Dropped

#### Scenario: An episode change records what it moved from

- **WHEN** declining a held change raises an entry's episodes watched from 3 to 7
- **THEN** the record reports episode 7 and carries 3 as the count held before it

#### Scenario: A completion and a score applied together

- **WHEN** declining a held change applies both a completion and a score to the same entry
- **THEN** the records are ordered so that a surface reading them reports one completion carrying that score, exactly as it does for a completion and score I saved together myself

#### Scenario: A declined diff records nothing

- **WHEN** a pending reconciliation diff is declined
- **THEN** no activity is recorded for any anime it covered

#### Scenario: Accepting a held change records nothing

- **WHEN** I accept a change held for review and it is pushed to MyAnimeList
- **THEN** no activity is recorded, because nothing about the stored entry changed

#### Scenario: Recording never pushes to MyAnimeList

- **WHEN** declining a held change records what it applied
- **THEN** no entry is marked as having changes to push, and nothing is sent to MyAnimeList as a result of the recording

### Requirement: The sync paths record nothing

The three sync paths — the background import of anime found on MyAnimeList but missing locally, the acceptance of a reconciliation diff, and the corrective re-sync — SHALL record nothing in the activity log, on any run, whatever they apply.

A change these paths apply was made somewhere else: in the app on another device, which records it in its own log, or on MyAnimeList's own site. Recording it here as well would put the same change in the log twice once two devices' logs are combined — at a later moment than it was made, and coarser, since a sync sees only the net change and not the steps that led to it. Such records SHALL NOT be written, rather than written and removed later.

The consequence SHALL be stated plainly rather than worked around: a change made on MyAnimeList's own site SHALL NOT appear in the activity log on any device.

These paths SHALL go on applying exactly what they apply today, in the same order and under the same conditions; only the recording stops. Whether an import records anything SHALL NOT depend on whether it is the first run, whether it was interrupted and resumed, or whether any activity has been recorded before it.

Declining a change held for review is not a sync path in this sense. It is a change made in the app and is recorded per "Every change made in the app records what it changed".

#### Scenario: A first import writes no history

- **WHEN** the list is imported for the first time
- **THEN** the activity log holds no records for the anime it imported

#### Scenario: A later import writes no history either

- **WHEN** an anime is added on MyAnimeList's own site after my history has begun, and a later import creates its entry
- **THEN** the entry is created exactly as before, and no activity is recorded for that anime

#### Scenario: An accepted diff records nothing

- **WHEN** I accept a reconciliation diff that raises one anime's episodes watched, changes its score, and creates an entry for another anime not tracked locally
- **THEN** both entries hold the diff's values, and no activity is recorded for either anime

#### Scenario: The corrective re-sync records nothing

- **WHEN** the corrective re-sync changes one entry's status and creates an entry for an anime not tracked locally
- **THEN** both changes are applied, and no activity is recorded for either anime

#### Scenario: A completion made on MyAnimeList's site stays out of my history

- **WHEN** I complete an anime on MyAnimeList's own site and a sync path applies that completion here
- **THEN** the entry reads as Completed, and no record of the completion appears in the activity log, the Latest updates feed, or the full edit history

## REMOVED Requirements

### Requirement: A record names where the change came from

**Reason**: The sync paths no longer record anything (see "The sync paths record nothing"), so every record in the log is a change made in this app. There is nothing left for an origin to tell apart, and a column holding one fixed value would only invite code to branch on it.

**Migration**: The origin is removed from every record. In the same migration, the records previously written by the three sync paths are deleted: without an origin they would read as my own edits, and they would bring back the duplication this change exists to prevent. Records of declined held changes are kept and read like any other record. The deletion is irreversible, so a database dump is taken before the migration runs.
