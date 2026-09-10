## ADDED Requirements

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
