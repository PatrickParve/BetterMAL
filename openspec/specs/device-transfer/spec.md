# device-transfer Specification

## Purpose
The device-transfer capability lets me carry what exists only in this app — my ranking, my chosen anime pictures and series titles/pictures, my activity log, and this database's device identifier — between my devices, as a single downloadable JSON file produced only when I ask for it, and merged into another device only when I give it that file. The file holds nothing derived from MyAnimeList, since MyAnimeList already carries that data to any device I connect it on; it exists purely for the choices and history this app itself keeps.

Importing a file merges it with no review step: the activity log as a union, each chosen picture and title to whichever device changed it last, and the ranking whole to whichever device arranged it last. It touches no list entry, sends nothing to MyAnimeList, and cannot be undone. It then reports what it did.

## Requirements

### Requirement: An export is produced only when I ask for it
The system SHALL produce an export file only when I explicitly ask for one from the Settings page. It SHALL hand the result to the browser as a download of a single JSON file. How the file reaches another device SHALL be left to me.

The system SHALL NOT produce, send, upload or schedule an export by any other means. In particular:
- not at startup
- not on a timer
- not after an edit
- not when the app is opened on a device that has sat idle

Producing an export SHALL write nothing to the database. No stored value SHALL differ afterwards from what it was before, and no record SHALL be kept that an export happened. This covers:
- every choice and its time
- every ranking position and the ranking time
- every log record
- the device identifier

#### Scenario: Pressing export produces one file
- **WHEN** I press the export action on the Settings page
- **THEN** the browser receives one JSON file, named for this device and the moment of export

#### Scenario: Opening an idle device sends nothing
- **WHEN** I open the app on a device that has not been used for weeks
- **THEN** no export is produced, and nothing is sent anywhere

#### Scenario: Exporting changes nothing
- **WHEN** I export
- **THEN** every stored choice, choice time, ranking position, ranking last-modified time, log record and the device identifier is exactly as it was before

### Requirement: The file holds only what exists nowhere but this app
The file SHALL hold exactly these top-level members, and no others:
- `formatVersion`
- `device`
- `exportedAt`
- `ranking`
- `animePictures`
- `series`
- `activity`

The file SHALL NOT contain anything that comes from MyAnimeList or rebuilds itself. That excludes:
- anime metadata: titles, the pictures MyAnimeList publishes, picture sets, episode counts and dates
- season listings
- episode airings
- relations
- series structure and membership
- top-anime rankings

It SHALL NOT contain any of the following either:
- OAuth tokens
- the anime-updates feed or relation discoveries
- pending-sync, held or reconciliation state
- browser preferences

Anime and series SHALL be referred to by id only.

#### Scenario: The top-level members are fixed
- **WHEN** I open an export file
- **THEN** its top-level members are exactly `formatVersion`, `device`, `exportedAt`, `ranking`, `animePictures`, `series` and `activity`

#### Scenario: No token leaves the device
- **WHEN** I export while connected to MyAnimeList
- **THEN** no access token or refresh token appears anywhere in the file

#### Scenario: Anime are named by id only
- **WHEN** an anime appears in the ranking, a picture choice or the log
- **THEN** the file carries its MyAnimeList id, and no title, MyAnimeList picture or other metadata for it

### Requirement: The header names the format, the device and the moment
Nothing in the header SHALL require configuration.

- **`formatVersion`** SHALL be the integer `1` for the file shape this requirement set defines. A later change SHALL raise it if it alters the shape in a way an older import could misread.
- **`device`** SHALL be an object holding two members:
  - `id`: this database's device identifier (see `data-persistence`, "Each database carries one device identifier"). It is the same in every export from that database and different between my two devices. It is what identifies the device.
  - `name`: a readable description of the operating system and browser the export was requested from, such as `"macOS · Safari"`, derived from that request. Where only one of the two can be recognised, `name` SHALL name that one alone. Where neither can, `name` SHALL be null and the export SHALL still succeed. `name` is descriptive only and SHALL NOT be relied on to tell devices apart.
- **`exportedAt`** SHALL be the moment the file was produced. It SHALL be the only time in the file stamped at export. Every other time SHALL be the one already stored.

The download's file name SHALL carry the first eight hex digits of the device identifier and the moment of export. Files from two devices, or two exports from one device, can then be told apart without opening them.

#### Scenario: The header of an export
- **WHEN** I export from Safari on macOS
- **THEN** the header reads `formatVersion` 1, `device.id` this database's device identifier, `device.name` `"macOS · Safari"`, and `exportedAt` the moment of export

#### Scenario: The file name carries the device and the moment
- **WHEN** the device identifier begins `7f3c9a12` and I export at 2026-09-10 18:22:04 UTC
- **THEN** the file is named `bettermal-7f3c9a12-20260910-182204.json`

#### Scenario: The device id is the same in every export
- **WHEN** I export, restart or rebuild the app, and export again
- **THEN** both files carry the same `device.id`

#### Scenario: Two devices carry different ids
- **WHEN** I export on each of my two devices
- **THEN** the two files carry different `device.id` values

#### Scenario: An unrecognised browser
- **WHEN** I export from a browser whose request names no operating system or browser the app recognises
- **THEN** the export succeeds, `device.name` is null, and `device.id` is present as usual

#### Scenario: Stored times are not restamped
- **WHEN** I export a picture chosen three weeks ago
- **THEN** its entry carries the three-week-old time, and only `exportedAt` carries the time of the export

### Requirement: The ranking travels as one ordered list with one time
`ranking` SHALL always be present. It SHALL hold:
- `animeIds`: the stored ranking order, as a list of anime ids
- `modifiedAt`: the ranking last-modified time

The list SHALL be in the order the app reads the ranking: by stored position, with ties broken by anime id. It SHALL carry no positions; an id's place in the list is its position. Each anime SHALL appear at most once.

Two edge cases:
- A ranking **never arranged** on this database SHALL have a null `modifiedAt` and an empty `animeIds`.
- A ranking **arranged and then emptied** SHALL have an empty `animeIds`, and `modifiedAt` SHALL be the time it was emptied.

#### Scenario: The stored order is the list order
- **WHEN** the stored ranking holds C, A and B at positions 0, 1 and 2, last modified at `T`
- **THEN** `ranking.animeIds` is `[C, A, B]` and `ranking.modifiedAt` is `T`

#### Scenario: A ranking never arranged
- **WHEN** no ranking order has ever been written on this database
- **THEN** `ranking.modifiedAt` is null and `ranking.animeIds` is empty

#### Scenario: An emptied ranking keeps its time
- **WHEN** the ranking was arranged and then emptied at `T`
- **THEN** `ranking.animeIds` is empty and `ranking.modifiedAt` is `T`

### Requirement: Chosen anime pictures are selected by their time, not their value
`animePictures` SHALL hold one entry per anime whose chosen-picture time is not null, and none for any other anime. Each entry SHALL carry:
- `animeId`
- `selectedPictureUrl`: the chosen picture, or null where the choice was cleared
- `modifiedAt`: the chosen-picture time

Selection SHALL depend on the time alone:
- A cleared choice has no chosen picture but does have a real time. It SHALL be exported, so that it can outrank an older choice on the other device.
- An anime SHALL NOT be excluded for not being in my list. A stamped choice is exported wherever it is stored.

Entries SHALL be ordered by anime id, ascending.

#### Scenario: A chosen picture is exported
- **WHEN** I chose a picture for an anime at `T`
- **THEN** `animePictures` holds an entry for that anime with that picture and `modifiedAt` `T`

#### Scenario: A cleared choice is exported with a null picture
- **WHEN** I cleared an anime's chosen picture at `T`
- **THEN** `animePictures` holds an entry for that anime with `selectedPictureUrl` null and `modifiedAt` `T`

#### Scenario: Never chosen, never cleared
- **WHEN** no picture has ever been chosen or cleared for an anime
- **THEN** `animePictures` holds no entry for it

#### Scenario: A stamped choice on an anime no longer in my list
- **WHEN** I chose a picture for an anime and later removed that anime from my list
- **THEN** its entry is still exported, with the stored picture and time

### Requirement: Series choices travel as independent blocks
`series` SHALL hold one entry per series whose chosen-title time or chosen-picture time is not null, and none for any other series. Each entry SHALL carry `seriesId`, the series' stored identifier (its root entry's MyAnimeList id), and up to two blocks:
- **`title`**, present exactly when the chosen-title time is not null. It holds `value`, the chosen title or null where the choice was cleared, and `modifiedAt`, that time.
- **`picture`**, present exactly when the chosen-picture time is not null. It holds `value`, the chosen picture URL or null where the choice was cleared, and `modifiedAt`, that time.

A block for a choice never set or cleared SHALL be **absent** from the entry: its key SHALL NOT appear. It SHALL NOT be written as null, or as a block with a null time, because a null `value` inside a block already means "cleared". Within a block that is present, `value` SHALL always be written, as null where the choice was cleared.

Entries SHALL be ordered by series id, ascending.

#### Scenario: A series with only a chosen title
- **WHEN** a series has a chosen title set at `T` and no picture has ever been chosen or cleared for it
- **THEN** its entry holds a `title` block with that title and `modifiedAt` `T`, and no `picture` key at all

#### Scenario: A series with both choices
- **WHEN** a series has a chosen title set at `T1` and a chosen picture set at `T2`
- **THEN** its entry holds a `title` block with `T1` and a `picture` block with `T2`, each with its own value

#### Scenario: A cleared series picture
- **WHEN** a series' chosen picture was cleared at `T` and no title has ever been chosen or cleared for it
- **THEN** its entry holds a `picture` block whose `value` is null and whose `modifiedAt` is `T`, and no `title` key

#### Scenario: A series with no choices
- **WHEN** neither a title nor a picture has ever been chosen or cleared for a series
- **THEN** `series` holds no entry for it

### Requirement: The activity log is exported whole, in the order it was stored
`activity` SHALL hold every activity-log record, ordered by timestamp ascending. Records sharing a timestamp SHALL follow the order they were stored in on this database.

The order carries meaning. An import relies on it to reproduce each save's internal order, and that order is what lets a completion and a score saved together read as one row.

Each record SHALL carry:
- `id`: the record's identifier, the one that travels between devices
- `timestamp`
- `animeId`
- `changeType`
- `changeDetail`, written as null where the record holds none
- `previousEpisodesWatched`, written as null where the record holds none

A record's local number SHALL NOT appear anywhere in the file. No record SHALL carry a field naming where the change came from.

Records for anime no longer in my list, removals included, SHALL be exported like any other.

#### Scenario: Oldest first
- **WHEN** the log holds records at `T1` and `T2`, with `T1` earlier than `T2`
- **THEN** the `T1` record precedes the `T2` record in `activity`

#### Scenario: A save's internal order survives
- **WHEN** one save stored a completion and then a score for the same anime, both at timestamp `T`
- **THEN** the completion precedes the score in `activity`

#### Scenario: The local number stays home
- **WHEN** I export
- **THEN** each record's `id` is its identifier, and no record carries its local number

#### Scenario: The history of a removed anime travels
- **WHEN** I removed an anime from my list after editing it
- **THEN** its edit records and its removal record are all exported

### Requirement: Values are written exactly as they are stored
- A change type SHALL be written under the same name the database stores for it (for example `EpisodeIncremented`), never as a number and never in another casing.
- Every time SHALL be written as a UTC ISO 8601 instant with a `Z` suffix, carrying all the precision the database stores. No time SHALL be rounded or truncated, so a time read back from the file equals the stored time exactly.
- Anime ids and series ids SHALL be written as JSON numbers.
- Record identifiers and the device identifier SHALL be written as GUID strings in canonical form.

#### Scenario: Change types use their stored names
- **WHEN** the log holds an episode-progress record
- **THEN** its `changeType` in the file is the string `EpisodeIncremented`

#### Scenario: A microsecond time survives
- **WHEN** a choice's stored time is 2026-09-09 21:26:43.973829 UTC
- **THEN** the file carries `"2026-09-09T21:26:43.973829Z"` for it

### Requirement: An export is one consistent snapshot
Every section of the file SHALL be read as of one moment:
- A change saved while an export is being produced SHALL appear in the file entirely or not at all.
- The ranking's list and its time SHALL come from the same stored state.

Two exports requested from the same browser, with no change stored between them, SHALL differ only in `exportedAt` and the file name.

#### Scenario: A save lands during an export
- **WHEN** a score save rewrites the ranking and its time while an export is being produced
- **THEN** the file carries either the old order with the old time, or the new order with the new time, never one with the other

#### Scenario: Unchanged data exports identically
- **WHEN** I export twice from the same browser with no change in between
- **THEN** the two files differ only in `exportedAt` and their file names

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
- **A list backup.** Its `kind` is `"listBackup"` (see `list-backup`). The refusal SHALL say that the file is a backup of my list, and that a list backup is never imported. This SHALL be checked as soon as the file is known to be a JSON object, before its `formatVersion` is read, so a list backup is never described as newer, damaged or not an export file.
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

#### Scenario: A list backup
- **WHEN** I import a list backup
- **THEN** the import is refused, saying that the file is a backup of my list and is never imported, and nothing changes

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
The system SHALL refuse to start an import, saying why, until a MyAnimeList list import has **gone through my whole list** since the application started.

A list import run has gone through the list when it has read the list and dealt with every anime it found missing. That includes a run that found nothing missing, and a run that ended with some anime it could not fetch. A run that failed before it could read the list has not gone through it.

A choice cannot be applied to an anime that is not yet in my list. Importing into a half-populated device would therefore report failures that are not failures.

The refusal SHALL say why, in terms of where the list import stands:
- **while a run is fetching anime:** how far it has got
- **while a run is still reading the list:** that the list is being checked against MyAnimeList
- **when the last run could not read the list:** that it could not, and when it will try again, or that it will try when the app next starts
- **while the connection to MyAnimeList is lost:** that the list cannot be checked until I re-authorize
- **when no run has started since the application started:** that the list has not finished importing since the app started

The system SHALL refuse to start an import while another import is running. Two imports SHALL never run at once.

#### Scenario: The list is still being imported
- **WHEN** I import a file while the list import stands at 142 of 380
- **THEN** the import is refused, saying the list is still being imported and how far it has got, and nothing changes

#### Scenario: The list is being checked
- **WHEN** I import a file while the list import is still reading my MyAnimeList list
- **THEN** the import is refused, saying the list is being checked against MyAnimeList

#### Scenario: The list could not be read
- **WHEN** I import a file after the list import failed to read my MyAnimeList list and a retry is planned
- **THEN** the import is refused, saying the list couldn't be read and when it will try again

#### Scenario: The connection is lost
- **WHEN** I import a file while the connection to MyAnimeList is lost
- **THEN** the import is refused, saying the list can't be checked until I re-authorize

#### Scenario: A list import with nothing missing opens the way
- **WHEN** the list import has read my list and found nothing missing
- **THEN** a file I import is accepted

#### Scenario: Anime the list import could not fetch do not block
- **WHEN** the list import went through my list but could not fetch two anime
- **THEN** a file I import is accepted, and choices for those two anime are reported as failures in its report

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
- **An anime picture:** the anime is in my list, and the picture is one of that anime's pictures as the picker offers them, from MyAnimeList or from TMDB.
- **A series picture:** the picture is one the series' picker offers.
- **A series title:** the title passes the rule for a custom series title, checked against the titles offered for that series here. It SHALL be stored in the form that rule stores.

A choice that clears a value SHALL need no check, as clearing in the picker needs none.

A picture choice refused because the picture is not among those offered SHALL be checked **once more** before it is reported:
1. The picture sets the offer is built from SHALL first be fetched again, including sets fetched before. What is fetched depends on the refused picture:
   - **Not a TMDB image:** fetch from MyAnimeList, as follows.
     - For an anime picture, fetch that anime's own set.
     - For a series picture, fetch the set of each main-line member in my list.
   - **A TMDB image:** fetch from TMDB every set the anime or series draws from (`tmdb-artwork`), whatever their age. The series page's per-visit budget does not apply.
2. The choice SHALL then be checked again.

MyAnimeList picture sets SHALL still be fetched only for anime in my list. When TMDB access is not configured on this device, no TMDB set SHALL be fetched, and a refused TMDB choice SHALL go straight to being reported.

A choice that is still refused after that, or that is refused for any other reason, SHALL NOT be stored. It SHALL be reported, and every other choice SHALL still be applied.

#### Scenario: A picture accepted once its set is fetched
- **WHEN** the file chooses a picture for an anime in my list whose picture set this device has never fetched
- **THEN** that anime's set is fetched, the choice is checked again and stored, and nothing is reported for it

#### Scenario: A set fetched long ago is fetched again
- **WHEN** the file chooses a picture that MyAnimeList added after this device last fetched that anime's set
- **THEN** the set is fetched again, and the choice is stored

#### Scenario: A TMDB choice accepted once its sets are fetched
- **WHEN** the file chooses a TMDB image for a mapped anime in my list whose TMDB sets this device has never fetched, and TMDB access is configured
- **THEN** that anime's TMDB sets are fetched, the choice is checked again and stored, and no MyAnimeList request is made for it

#### Scenario: A TMDB choice without TMDB access
- **WHEN** the file chooses a TMDB image this device has not cached, and no TMDB API key is configured here
- **THEN** the choice is not stored, and it is reported

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
