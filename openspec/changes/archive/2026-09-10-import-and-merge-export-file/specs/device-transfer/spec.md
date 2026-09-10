## ADDED Requirements

### Requirement: An import merges a file from my other device, and asks nothing
The system SHALL import an export file only when I give it one on the Settings page. It SHALL apply the file by the rules this capability sets out, with no review, confirmation or accept step, and SHALL then report what it did.

An import SHALL NOT be undoable, and the system SHALL keep no copy of what it replaced.

An import SHALL touch nothing but the sections the file carries. It SHALL NOT:
- change any list entry
- send anything to MyAnimeList, or mark anything to be sent
- record any activity of its own; the only records it adds to the activity log SHALL be the ones the file carries
- change the device identifier

Importing a file a second time SHALL change nothing that the first import applied:
- its records are already present
- its times are no newer than the ones it stored
- its ranking is the one already stored

#### Scenario: Importing applies without asking
- **WHEN** I give the Settings page an export file from my other device
- **THEN** its contents are merged by the rules of this capability and a report is shown, with nothing asked of me in between

#### Scenario: Re-importing changes nothing
- **WHEN** I import a file that was imported in full before
- **THEN** no log record, choice, choice time, ranking position or ranking time changes, and the report has nothing to report

#### Scenario: My list is untouched
- **WHEN** I import a file
- **THEN** no list entry changes, and nothing is sent or queued to MyAnimeList

### Requirement: A file this build cannot read is refused by name
Before writing or fetching anything, the system SHALL refuse a file when any of the following holds. It SHALL say which one, in words:
- **Not an export file.** It is not a JSON object, or it has no integer `formatVersion`.
- **A newer format.** Its `formatVersion` is newer than the formats this build reads. The refusal SHALL name the file's format and the format this build reads. This SHALL be checked before the rest of the file's shape, so a newer file is never described as damaged.
- **Damaged.** A member the format requires is missing or of the wrong kind. The refusal SHALL name that member.
- **From this device.** Its `device.id` is this database's own device identifier.

Members the format does not define SHALL be ignored.

A refusal SHALL write nothing and fetch nothing. It SHALL leave the previous import's report in place.

#### Scenario: A newer format
- **WHEN** I import a file whose `formatVersion` is 2 on a build that reads format 1
- **THEN** the import is refused, saying the file is format 2 and this build reads format 1, and nothing changes

#### Scenario: Not an export file
- **WHEN** I import a JSON file that has no `formatVersion`
- **THEN** the import is refused as not an export file

#### Scenario: A damaged file
- **WHEN** an export file's activity record has no `id`
- **THEN** the import is refused, naming that member, and nothing from the file is applied

#### Scenario: A file from this device
- **WHEN** I import a file this device exported
- **THEN** the import is refused, saying the file was exported from this device

#### Scenario: A member the format does not define
- **WHEN** a format-1 file carries a member the format does not define
- **THEN** that member is ignored and the import proceeds

### Requirement: No import while the list is being imported
The system SHALL refuse to start an import, saying why, while the initial MyAnimeList list import has not finished since the application started. This SHALL apply whether that import is running or has not run.

A choice cannot be applied to an anime that is not yet in my list. Importing into a half-populated device would therefore report failures that are not failures.

While the list import is running, the refusal SHALL say how far it has got.

The system SHALL refuse to start an import while another import is running. Two imports SHALL never run at once.

#### Scenario: The list is still being imported
- **WHEN** I import a file while the initial list import stands at 142 of 380
- **THEN** the import is refused, saying the list is still being imported and how far it has got, and nothing changes

#### Scenario: An import is already running
- **WHEN** I import a file while another import is running
- **THEN** the second is refused, and the first carries on

### Requirement: The activity log merges as a union
For every record in the file whose identifier no stored record carries, the system SHALL store that record exactly as the file carries it:
- its identifier
- its timestamp
- its anime
- its change type
- its detail
- its previous episode count

A record whose identifier is already stored SHALL be skipped. Where the file carries an identifier more than once, only its first occurrence SHALL be considered.

The system SHALL NOT delete, overwrite or re-time any stored record.

Records SHALL be stored in the order the file lists them, so records sharing a timestamp read in the file's order on this database too. A completion and a score saved together on my other device SHALL therefore read as one completion carrying that score here as well.

A record whose change type this build does not know SHALL NOT be stored, and SHALL be reported. It SHALL NOT prevent the rest of the file from being imported.

Every surface that reads the log SHALL read an imported record as it reads any other. In particular, imported progress SHALL count toward the recap of the period it falls in, so a period's episode count can rise after an import.

A removal record SHALL be imported as history only. It SHALL NOT remove the anime from my list.

#### Scenario: New records are added
- **WHEN** the file carries 400 records, 300 of which are already stored here
- **THEN** the other 100 are stored, and the 300 are left exactly as they were

#### Scenario: A save's internal order survives
- **WHEN** the file carries a completion and then a score for one anime, both at timestamp `T`, and neither is stored here
- **THEN** the latest-updates feed shows one "Completed — Score" row for that anime

#### Scenario: Nothing is re-timed
- **WHEN** a stored record and a file record carry the same identifier with different timestamps
- **THEN** the stored record is unchanged

#### Scenario: An unknown change type
- **WHEN** one file record carries a change type this build does not know
- **THEN** that record is not stored and is reported, and every other new record is stored

#### Scenario: A past recap reads higher
- **WHEN** the file carries episode progress within a period whose recap I have already looked at
- **THEN** that period's recap now counts it

#### Scenario: A removal is history only
- **WHEN** the file carries a removal record for an anime that is still in my list here
- **THEN** the record is stored, and the anime stays in my list

### Requirement: Chosen pictures and titles go to the newer time, choice by choice
The system SHALL decide each choice the file carries on its own:
- each anime's chosen picture
- each series' chosen title
- each series' chosen picture

A series' title and its picture SHALL be decided independently of each other.

A choice from the file SHALL be applied when this device holds no time for that choice, or holds a time earlier than the file's. Where this device's time is equal or later, the choice SHALL be left exactly as it is. Times SHALL be compared at the full precision they are stored with.

An applied choice SHALL store the file's value together with the file's time, never the time of the import. A null value SHALL clear the choice as a clear made here would, and SHALL store the file's time.

Where the file's entry for a series has no block for a choice, that choice SHALL be left untouched here.

A series SHALL be found through the anime the file's series id names: the series that anime primarily belongs to on this device. A choice therefore lands on the right series even when the series' root has moved on one side and its id differs between the two devices.

#### Scenario: A newer choice wins
- **WHEN** this device chose picture P1 for an anime at `T1`, and the file carries picture P2 for it at `T2`, later than `T1`
- **THEN** the anime's chosen picture is P2, with time `T2`

#### Scenario: An older choice loses
- **WHEN** the file carries a choice made earlier than the one stored here
- **THEN** the stored choice and its time are unchanged

#### Scenario: An equal time changes nothing
- **WHEN** the file carries a choice with exactly the time stored here
- **THEN** nothing changes

#### Scenario: A newer clear wins
- **WHEN** this device chose a picture for an anime at `T1`, and the file carries a cleared choice for it at `T2`, later than `T1`
- **THEN** no picture is chosen for that anime, and its choice time is `T2`

#### Scenario: A choice with no time here
- **WHEN** this device has never chosen or cleared a picture for an anime, and the file carries a choice for it
- **THEN** the file's choice and time are stored

#### Scenario: Title and picture are decided apart
- **WHEN** a series' title was changed more recently here, and its picture more recently on my other device
- **THEN** after the import the series keeps this device's title and takes the file's picture

#### Scenario: An absent block is left alone
- **WHEN** the file's entry for a series carries a picture block and no title block
- **THEN** the series' title and title time here are unchanged

#### Scenario: A moved root
- **WHEN** the file names a series by id 100, and on this device anime 100 primarily belongs to series 90 because the root moved
- **THEN** the file's choices for that series are compared with, and applied to, series 90

### Requirement: An imported choice passes the picker's own checks
A choice from the file that sets a value SHALL be stored only if the picker would accept the same value here:
- **An anime picture:** the anime is in my list, and the picture is one of that anime's pictures as the picker offers them.
- **A series picture:** the picture is one the series' picker offers.
- **A series title:** the title passes the rule for a custom series title, against the titles offered for that series here. It SHALL be stored in the form that rule stores.

A choice that clears a value SHALL need no check, as clearing in the picker needs none.

A picture choice refused because the picture is not among those offered SHALL be checked **once more** before it is reported:
1. The picture sets the offer is built from SHALL first be fetched from MyAnimeList, including sets fetched before:
   - for an anime picture, that anime's own set
   - for a series picture, the set of each main-line member in my list
2. The choice SHALL then be checked again.

Picture sets SHALL still be fetched only for anime in my list.

A choice that is still refused after that, or that is refused for any other reason, SHALL NOT be stored. It SHALL be reported, and every other choice SHALL still be applied.

#### Scenario: A picture accepted once its set is fetched
- **WHEN** the file chooses a picture for an anime in my list whose picture set this device has never fetched
- **THEN** that anime's set is fetched, the choice is checked again and stored, and nothing is reported for it

#### Scenario: A set fetched long ago is fetched again
- **WHEN** the file chooses a picture that MyAnimeList added after this device last fetched that anime's set
- **THEN** the set is fetched again, and the choice is stored

#### Scenario: Still refused after the fetch
- **WHEN** the file chooses a picture that is not among the anime's pictures, even after its set is fetched
- **THEN** the choice is not stored, and it is reported

#### Scenario: An anime not in my list
- **WHEN** the file chooses a picture for an anime that is not in my list here
- **THEN** the choice is not stored, and it is reported

#### Scenario: A title that is not a trim of an offered title
- **WHEN** the file chooses a series title that is not a contiguous trim of any title offered for that series here
- **THEN** the title is not stored, and it is reported

#### Scenario: A clear needs no check
- **WHEN** the file carries a newer cleared picture choice for an anime that is not in my list here
- **THEN** the clear and its time are stored

### Requirement: The ranking goes whole to the newer side
The ranking SHALL be decided as one: one list and one time.

When the file's ranking time is later than this device's, or this device has none and the file has one, the stored ranking SHALL become exactly the file's list, carrying the file's time. An anime in this device's ranking that the file's list does not hold SHALL lose its stored place.

When the file's ranking time is equal or earlier, or is null, the ranking SHALL be left exactly as it is.

Where the file's list is to be applied but names an anime that this device holds no record of and cannot fetch, the ranking SHALL be left exactly as it is, and the report SHALL name that anime. The ranking SHALL NOT be stored without it.

#### Scenario: A newer ranking replaces this one
- **WHEN** this device's ranking is `[A, B, C]` from `T1`, and the file's is `[C, D, A]` from `T2`, later than `T1`
- **THEN** the stored ranking is `[C, D, A]` with time `T2`, and B holds no place

#### Scenario: An older ranking is ignored
- **WHEN** the file's ranking time is earlier than this device's
- **THEN** the stored order and its time are unchanged

#### Scenario: A ranking never arranged there
- **WHEN** the file's ranking time is null
- **THEN** this device's ranking is unchanged

#### Scenario: A newer emptied ranking
- **WHEN** the file's ranking is empty, with a time later than this device's
- **THEN** this device's ranking is emptied, and carries the file's time

#### Scenario: An anime that cannot be fetched holds the ranking back
- **WHEN** the file's newer ranking names an anime this device has never seen, and MyAnimeList cannot supply it
- **THEN** this device's ranking and its time are unchanged, and the report names that anime

### Requirement: Anime this device has never seen are fetched first
Before writing anything from the file, the system SHALL fetch from MyAnimeList every anime it holds no record of that one of these names:
- a record about to be stored
- a choice about to be decided
- a ranking about to be applied
- a series entry

It SHALL NOT stop for this, and SHALL NOT ask. Each anime fetched SHALL be named in the report.

Where the file names a series this device has not stored, the system SHALL build that series as visiting it would, before deciding its choices. Where the series cannot be built, or the anime's relations here form no series, its choices SHALL be reported.

For an anime that cannot be fetched, everything naming it SHALL be left out and reported: its records, its choice and, per "The ranking goes whole to the newer side", the ranking. Everything else SHALL still be applied.

These fetches and builds SHALL store what a page visit would store, and it SHALL remain even if the import then fails.

#### Scenario: An unseen anime is fetched and its records stored
- **WHEN** the file carries records for an anime this device has never seen
- **THEN** that anime is fetched from MyAnimeList, its records are stored, and the report names it as fetched for the first time

#### Scenario: An anime MyAnimeList cannot supply
- **WHEN** a file record names an anime MyAnimeList cannot return
- **THEN** that anime's records are not stored and are reported, and every other record is stored

#### Scenario: A series not built here
- **WHEN** the file carries a chosen title for a series this device has never built
- **THEN** the series is built, and the title is decided against it

### Requirement: An import is written in one transaction
Everything an import writes from the file SHALL be written in one transaction, after every fetch it needs has finished:
- log records
- choices and their times
- the ranking and its time

Either all of it SHALL be stored, or none of it.

The transaction SHALL NOT drop and recreate any table or row, and the app SHALL keep serving while it runs.

A change I save on this device while the import is being written SHALL NOT be overwritten by an older value from the file. Where the two collide, the import SHALL fail as a whole, store nothing from the file, and say so.

An import that fails SHALL say that nothing from the file was applied, and importing the file again SHALL be safe.

#### Scenario: A failure stores nothing
- **WHEN** writing an import fails partway
- **THEN** no record, choice or ranking change from the file is stored, and the report says nothing from the file was applied

#### Scenario: A save made meanwhile is not overwritten
- **WHEN** I choose a picture for an anime while an import carrying an older choice for it is being written
- **THEN** afterwards my choice and its time are the ones stored, whether the import completed or failed

#### Scenario: The app keeps serving
- **WHEN** an import is running
- **THEN** pages keep loading and edits keep saving

### Requirement: The import reports what it did
When an import finishes, the system SHALL report:
- **the ranking's changes:** the anime added to the ranking and the anime removed from it, where the ranking was replaced
- **first fetches:** the anime fetched from MyAnimeList for the first time
- **failures:** everything that could not be applied, each with:
  - the anime or series id the file names
  - its title, where this device knows it by then
  - what could not be applied: a chosen picture, a series title, a series picture, the edit-history records for that anime (with their number), or the ranking
  - why it could not

The report SHALL hold nothing else:
- a choice or record that applied cleanly SHALL have no line
- a ranking that was only reordered SHALL list no anime
- there SHALL be no counts of what was applied

An import with nothing to report SHALL say so.

The report SHALL say which file it is for, by the file's device name and export time. It SHALL remain available until the next import starts or the app restarts.

An import that failed SHALL report that nothing from the file was applied, and why.

#### Scenario: The ranking's changes are listed
- **WHEN** a newer ranking replaces `[A, B, C]` with `[C, D, A]`
- **THEN** the report lists D as added to the ranking and B as removed from it

#### Scenario: A reordered ranking lists nothing
- **WHEN** a newer ranking holds the same anime in a different order
- **THEN** the report lists no anime as added to or removed from the ranking

#### Scenario: A failure names its subject
- **WHEN** a series title from the file is refused
- **THEN** the report names the series' id and title, says that its title could not be applied, and says why

#### Scenario: A clean import has nothing to report
- **WHEN** every record and choice in the file applies, and nothing had to be fetched
- **THEN** the report says there is nothing to report

#### Scenario: A failed import says so
- **WHEN** an import fails while it is being written
- **THEN** the report says that nothing from the file was applied, and why
