## ADDED Requirements

### Requirement: The ranking's stored order carries one last-modified time
The system SHALL store the ranking's order as one position per anime. Alongside it, it SHALL store a single **ranking last-modified time**, which belongs to the ranking as a whole rather than to any position. This time SHALL be held on a single bookkeeping record that belongs to no anime. No per-position time SHALL be stored.

Every write of the stored order SHALL set the ranking last-modified time in the same transaction as the order itself. The order can then never be stored without its time, and the time can never advance without the order. This SHALL hold for every write:
- a reorder in the ranking editor
- a reorder on a series page
- the automatic placement that follows a saved score

It SHALL NOT depend on whether the written order differs from the one it replaces, nor on which of these caused the write.

An absent ranking last-modified time SHALL mean the ranking has never been arranged on this database.

The stored position SHALL remain the only thing that orders the ranking. No ordering SHALL be read from the time.

#### Scenario: A reorder stamps the ranking
- **WHEN** I reorder a score in the ranking editor and the change is saved
- **THEN** the ranking last-modified time is the time of that save

#### Scenario: A score placement stamps the ranking
- **WHEN** I save a new score for an anime and it is placed last among the hand-ordered anime of that score
- **THEN** the ranking last-modified time is the time of that placement

#### Scenario: An unchanged order is still stamped
- **WHEN** the stored order is written with exactly the order it already held
- **THEN** the ranking last-modified time still advances to the time of that write

#### Scenario: A ranking never arranged has no time
- **WHEN** no order has ever been written on a database
- **THEN** no ranking last-modified time is stored there

#### Scenario: Positions carry no time of their own
- **WHEN** the stored ranking is inspected
- **THEN** each anime in it carries only its position, and exactly one time is stored for the whole ranking

### Requirement: The stored ranking can be replaced exactly
The system SHALL provide a way to replace the stored ranking order with a given list of anime and a given time, in one transaction. Afterwards the stored order SHALL be exactly that list:
- each listed anime SHALL hold the position the list gives it
- no anime missing from the list SHALL hold a stored position

The ranking last-modified time SHALL become the given time, not the time the replacement ran. The replacement SHALL NOT compare the given time with the stored one; deciding which ranking is newer belongs to the caller.

This replacement SHALL be distinct from the slot-preserving merge used by the ranking editor, the series page and score placement. That merge SHALL keep behaving exactly as it does: anime it is not given keep their slots.

A list naming an anime the store holds no record of SHALL be rejected, and both the stored order and its time SHALL be left exactly as they were. Where the list names an anime more than once, its first occurrence SHALL decide its position.

An empty list SHALL leave the stored order empty, with the given time recorded. An emptied ranking therefore still records when it was emptied.

#### Scenario: Anime missing from the list are dropped
- **WHEN** the stored order is `[A, B, C]` and it is replaced with `[C, A]`
- **THEN** the stored order is `[C, A]` and `B` holds no position

#### Scenario: The merge still keeps slots it is not given
- **WHEN** the stored order is `[A, B, C]` and the editor's merge is given `[C, A]`
- **THEN** the stored order is `[C, B, A]`

#### Scenario: The given time is stored, not the time of the replacement
- **WHEN** the stored order is replaced with a list and a time three days in the past
- **THEN** the ranking last-modified time is that time from three days ago

#### Scenario: An unknown anime is rejected
- **WHEN** a replacement list names an anime the store holds no record of
- **THEN** the replacement fails, and the stored order and the ranking last-modified time are unchanged

#### Scenario: A repeated anime takes its first position
- **WHEN** the stored order is replaced with `[A, B, A]`
- **THEN** the stored order is `[A, B]`

#### Scenario: An empty replacement records its time
- **WHEN** the stored order is replaced with an empty list and a time `T`
- **THEN** no anime holds a stored position, and the ranking last-modified time is `T`

### Requirement: The portability migration keeps the ranking's time and identifies every log record
The migration that introduces the ranking last-modified time SHALL set that time from the latest per-position time stored when it runs, and only then remove the per-position times. An existing ranking therefore arrives carrying the time it was last written, rather than no time at all. A database holding no ranking positions SHALL be left with no ranking last-modified time.

The same migration SHALL give every existing activity-log record an identifier distinct from every other record's. For activity-log records, it SHALL NOT:
- change any record's timestamp, local number, change type or detail
- add or remove any record

For the ranking, it SHALL NOT change any position, nor add or remove one.

Reversing the migration SHALL restore a per-position time on every position, equal to the ranking last-modified time. That is the state every position was in before the migration ran.

#### Scenario: The current ranking keeps its time
- **WHEN** the migration runs against 455 stored positions that all hold the time 2026-09-09 07:59:51 UTC
- **THEN** the ranking last-modified time is 2026-09-09 07:59:51 UTC, and the same 455 anime hold the same positions

#### Scenario: An empty ranking gets no time
- **WHEN** the migration runs against a database holding no ranking positions
- **THEN** no ranking last-modified time is stored

#### Scenario: Every existing record gets its own identifier
- **WHEN** the migration runs against a log of 913 records
- **THEN** there are still 913 records, each carrying an identifier no other record carries, with its timestamp, local number, change type and detail unchanged

#### Scenario: The feed reads the same
- **WHEN** the migration has been applied and the profile page is opened
- **THEN** Latest updates and the full edit history show exactly the rows they showed before, in the same order, including every merged "Completed — Score" row

#### Scenario: Reversing restores the per-position time
- **WHEN** the migration is reversed on a database whose ranking last-modified time is `T`
- **THEN** every stored position carries the time `T` again

## MODIFIED Requirements

### Requirement: ActivityLog entity
The system SHALL persist an ActivityLog of timestamped change records (status changes, episode increments, score changes, additions, completions, removals) sufficient to reconstruct a chronological feed. Episode-change records SHALL also store the previous episodes-watched value so the direction of the change (increase vs decrease) can be determined at read time.

A removal record SHALL survive the deletion of the UserAnimeEntry it describes, so the history of an anime that was removed from my list is not lost along with the entry.

Every record SHALL carry an **identifier** that is unique among all records in the database. The system SHALL assign it when it creates the record, before the record is first stored, and it SHALL never change once stored. The identifier is the only property of a record that SHALL identify that record outside this database. A record that is stored while already carrying an identifier SHALL keep that identifier.

Every record SHALL also carry a **local number**, which the database assigns when the record is stored. The local number is meaningful only within that database: it SHALL NOT leave it, and it SHALL NOT be used to identify a record anywhere else. Surfaces reading the log SHALL order records that share a timestamp by their local number, so those records read in the order they were stored on this database.

#### Scenario: Recording a change
- **WHEN** a tracked field on a UserAnimeEntry changes
- **THEN** a timestamped ActivityLog row describing the change is written

#### Scenario: Recording episode-change direction
- **WHEN** an episodes-watched value changes
- **THEN** the ActivityLog row records the previous episodes-watched value alongside the new one

#### Scenario: Recording a removal
- **WHEN** an anime is removed from my list
- **THEN** a timestamped ActivityLog row recording the removal is written and is retained after the entry is deleted

#### Scenario: A new record gets its own identifier
- **WHEN** any change is recorded
- **THEN** its record carries an identifier that no other record in the database carries

#### Scenario: A record stored with an identifier keeps it
- **WHEN** a record that already carries an identifier is stored
- **THEN** it is stored with that same identifier, and this database gives it a new local number

#### Scenario: Two records cannot share an identifier
- **WHEN** a record is stored with an identifier that another stored record already carries
- **THEN** storing it fails and the log is left unchanged

#### Scenario: Records sharing a timestamp read in the order they were stored
- **WHEN** one save records several changes at the same timestamp
- **THEN** surfaces reading the log treat them in the order they were stored on this database

## REMOVED Requirements

### Requirement: Persisted manual top-anime selections
**Reason**: It describes choosing which tied anime fill the remaining top-ten slots, a choice the app no longer offers. The table it refers to now stores the ranking's full order. It is replaced by "The ranking's stored order carries one last-modified time" and "The stored ranking can be replaced exactly".
**Migration**: None. The stored order is unchanged. Only its per-position time is replaced, by one time for the whole ranking.
