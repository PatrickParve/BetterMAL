# list-backup Specification

## Purpose
The list-backup capability lets me produce, on request, a single JSON file holding a snapshot of my anime list as this app stores it — status, progress, score, dates and rewatch count for every anime I track — so I have a copy of my list to keep, separate from the device-transfer file that carries my ranking, pictures and edit history. A list backup is a copy to keep and read; the app never imports one back.

## Requirements

### Requirement: A backup is produced only when I ask for it
The system SHALL produce a list backup only when I explicitly ask for one from the Settings page. It SHALL hand the result to the browser as a download of a single JSON file. Where I keep the file SHALL be left to me.

The system SHALL NOT produce, send, upload or schedule a backup by any other means. In particular:
- not at startup
- not on a timer
- not after an edit

Producing a backup SHALL write nothing to the database and SHALL send nothing to MyAnimeList. No stored value SHALL differ afterwards from what it was before, and no record SHALL be kept that a backup happened.

#### Scenario: Pressing back up produces one file
- **WHEN** I press the list backup action on the Settings page
- **THEN** the browser receives one JSON file, named for this device and the moment of the backup

#### Scenario: Nothing happens by itself
- **WHEN** the app starts, runs for a day, and saves edits, without my pressing the backup action
- **THEN** no backup is produced

#### Scenario: A backup changes nothing
- **WHEN** I produce a backup
- **THEN** every list entry, every sync state and every other stored value is exactly as it was before, and nothing is queued or sent to MyAnimeList

### Requirement: A backup is never imported
A list backup is a copy to keep and read. The system SHALL offer no way to import, restore or merge a list backup: no action, endpoint or background job SHALL read one back into the database.

Given to the device-transfer import, a list backup SHALL be refused as a list backup (see `device-transfer`, "A file this build cannot read is refused by name").

#### Scenario: A backup given to the transfer import
- **WHEN** I give the Settings page's import action a list backup
- **THEN** the import is refused, saying that the file is a backup of my list and is never imported, and nothing changes

### Requirement: The file holds my list and nothing else
The file SHALL hold exactly these top-level members, and no others:
- `kind`
- `formatVersion`
- `device`
- `exportedAt`
- `entries`

It SHALL NOT contain any of the following:
- sync bookkeeping: whether an entry is waiting to be sent to MyAnimeList, when it last synced, whether it is held for review, and any pending removal
- OAuth tokens
- the ranking, chosen anime pictures, series choices or the activity log
- anime metadata beyond each entry's two titles and episode total
- browser preferences

#### Scenario: The top-level members are fixed
- **WHEN** I open a list backup
- **THEN** its top-level members are exactly `kind`, `formatVersion`, `device`, `exportedAt` and `entries`

#### Scenario: No token leaves the device
- **WHEN** I produce a backup while connected to MyAnimeList
- **THEN** no access token or refresh token appears anywhere in the file

#### Scenario: No sync state is written
- **WHEN** I produce a backup while one entry has an edit not yet sent to MyAnimeList
- **THEN** no entry in the file carries a member saying whether it is waiting to be sent, when it last synced, or whether it is held

### Requirement: The header names the file, the device and the moment
Nothing in the header SHALL require configuration.

- **`kind`** SHALL be the string `"listBackup"`, in every list backup. It says what the file is, without relying on its name.
- **`formatVersion`** SHALL be the integer `1` for the file shape this capability defines. A later change SHALL raise it if it alters the shape in a way a reader of the older shape could misread.
- **`device`** SHALL be an object holding `id` and `name`, by exactly the rules of the device-transfer header (see `device-transfer`, "The header names the format, the device and the moment"):
  - `id` is this database's device identifier
  - `name` describes the operating system and browser the backup was requested from, and is null where neither can be recognised
- **`exportedAt`** SHALL be the moment the file was produced.

The download's file name SHALL be `bettermal-list-`, then the first eight hex digits of the device identifier, then the moment of the backup as `yyyyMMdd-HHmmss` in UTC, then `.json`. It SHALL therefore never read as a device-transfer file's name.

#### Scenario: The header of a backup
- **WHEN** I produce a backup from Safari on macOS
- **THEN** the header reads `kind` `"listBackup"`, `formatVersion` 1, `device.id` this database's device identifier, `device.name` `"macOS · Safari"`, and `exportedAt` the moment of the backup

#### Scenario: The file name
- **WHEN** the device identifier begins `7f3c9a12` and I produce a backup at 2026-09-11 09:15:30 UTC
- **THEN** the file is named `bettermal-list-7f3c9a12-20260911-091530.json`

#### Scenario: The same device as the transfer export
- **WHEN** I produce a list backup and a device-transfer export from the same browser
- **THEN** both files carry the same `device.id` and the same `device.name`

### Requirement: One entry per anime in my list, as this app holds it now
`entries` SHALL hold exactly one entry for every anime in my list, and none for any anime not in it. Each entry SHALL carry the values this app holds for that anime at the moment of the backup:
- An entry with an edit not yet sent to MyAnimeList SHALL carry the edited values.
- An entry held for review SHALL carry the values stored here, not MyAnimeList's.
- An anime I have removed from my list SHALL have no entry, even while its removal is still waiting to be sent to MyAnimeList.
- An anime the app only holds details for, from browsing or search, SHALL have no entry.

Entries SHALL be ordered by anime id, ascending.

An empty list SHALL still produce a file, with an empty `entries`.

#### Scenario: Every anime in my list appears once
- **WHEN** my list holds 617 anime and I produce a backup
- **THEN** `entries` holds 617 entries, one per anime, ordered by anime id

#### Scenario: An unsent edit is backed up as edited
- **WHEN** I raised an anime's episodes watched from 3 to 4 and that edit has not yet reached MyAnimeList
- **THEN** its entry carries 4 episodes watched

#### Scenario: A pending removal is left out
- **WHEN** I removed an anime from my list and the removal has not yet reached MyAnimeList
- **THEN** the file holds no entry for that anime

#### Scenario: An anime I only browsed is left out
- **WHEN** the app holds details for an anime I have looked at but never added
- **THEN** the file holds no entry for it

### Requirement: Each entry carries my values, and enough to read it by
Each entry SHALL carry exactly these members, every one of them always present:
- `animeId`: the anime's MyAnimeList id
- `title`: the anime's stored MyAnimeList title
- `englishTitle`: its English title, or null where none is known
- `totalEpisodes`: the episode total the app shows for the anime, or null where it is unknown
- `status`
- `episodesWatched`
- `score`: my score, or null where I have not scored it
- `startedAt`: my start date, or null where none is set
- `completedAt`: my finish date, or null where none is set
- `rewatchCount`

A member with no value SHALL be written as null, never left out.

`title`, `englishTitle` and `totalEpisodes` are there so the file can be read on its own. `animeId` is what identifies the anime.

#### Scenario: A finished, scored, rewatched anime
- **WHEN** my list holds an anime completed with score 9, started 2025-01-04, finished 2025-02-10 and rewatched once
- **THEN** its entry carries status `Completed`, score 9, `startedAt` `"2025-01-04"`, `completedAt` `"2025-02-10"` and `rewatchCount` 1, along with its id, both titles and its episode total

#### Scenario: An unscored anime with no dates
- **WHEN** an anime in my list has no score and no start or finish date
- **THEN** its entry carries `score`, `startedAt` and `completedAt` as null

#### Scenario: An anime with no English title and no known total
- **WHEN** an anime in my list has no English title and no known episode total
- **THEN** its entry carries `englishTitle` and `totalEpisodes` as null, and its `title` is the stored MyAnimeList title

### Requirement: Values are written as they are stored
- A status SHALL be written under the name the database stores it under: `Watching`, `Completed`, `OnHold`, `Dropped`, `PlanToWatch` or `Rewatching`. It SHALL never be written as a number, in another casing, or under MyAnimeList's name for it.
- A Rewatching entry SHALL be written as `Rewatching`, never as `Watching`, though MyAnimeList holds it as `watching`.
- A date SHALL be written as `YYYY-MM-DD`, with no time and no zone.
- `exportedAt` SHALL be written as a UTC ISO 8601 instant with a `Z` suffix.
- Anime ids, episode counts, scores and rewatch counts SHALL be written as JSON numbers, and the device identifier as a GUID string in canonical form.

#### Scenario: A rewatch keeps its status
- **WHEN** an anime in my list is Rewatching
- **THEN** its entry's `status` is the string `Rewatching`

#### Scenario: A multi-word status keeps its stored name
- **WHEN** an anime in my list is planned
- **THEN** its entry's `status` is the string `PlanToWatch`

#### Scenario: A date carries no time
- **WHEN** an entry's start date is 11 September 2026
- **THEN** its `startedAt` is `"2026-09-11"`

### Requirement: A backup is one consistent snapshot
Every entry SHALL be read as of one moment. An edit saved while a backup is being produced SHALL appear in the file entirely or not at all: an entry SHALL never carry some fields from before the edit and some from after, and an anime added or removed at that moment SHALL be either wholly present or wholly absent.

Two backups requested from the same browser, with no change stored between them, SHALL differ only in `exportedAt` and the file name.

#### Scenario: An edit lands during a backup
- **WHEN** a save changes an entry's status and episodes watched while a backup is being produced
- **THEN** the file carries either both old values or both new values for that entry

#### Scenario: Unchanged data backs up identically
- **WHEN** I produce two backups from the same browser with no change in between
- **THEN** the two files differ only in `exportedAt` and their file names
