## Why

I want a saved copy of my list that I can keep on my own terms, whatever happens to MyAnimeList or to this database. Today nothing produces one. The device-transfer file (`device-transfer`) leaves my list out on purpose, because MyAnimeList already carries it between devices. A backup is a different job. It exists to be kept and read, not to be imported.

This change adds a list backup: one JSON file, produced when I press a button on the Settings page. **It is never imported.** The app offers no way to read it back, and nothing about it has to be mergeable.

**Verified against the code and the live database** (2026-09-11):

- **My list:** 617 entries:

  | Status | Entries |
  | --- | --- |
  | Completed | 462 |
  | Plan to watch | 114 |
  | Dropped | 27 |
  | Watching | 8 |
  | On hold | 4 |
  | Rewatching | 2 |

  - 142 entries have no score, 82 have a start date, 355 have a finish date, and 44 have been rewatched at least once.
  - None has an unsent edit or is held for review right now, but the backup must not depend on that.
- **The backup holds something MyAnimeList cannot.** MyAnimeList has no Rewatching status, so the app pushes a rewatch as `watching` (`mal-write-sync`, "Rewatching is pushed to MyAnimeList as watching"). A copy of MyAnimeList's list would lose that status. This file keeps it.
- **Status is stored by name.** `UserAnimeEntry.Status` uses `HasConversion<string>()` (`Data/AnimeTrackerDbContext.cs:47`), so the file can write `PlanToWatch` and `Rewatching` exactly as the database holds them, through the same `ExportJson.Options` the device-transfer export uses.
- **Titles.** Every anime has its stored MyAnimeList title. 49 of the 617 have no English title, and 38 have no known episode total.
- **The existing import would misread this file.** If a list backup were dropped onto the device-transfer import, `TransferFileReader` would find an integer `formatVersion`, then report the file as "damaged" because `ranking` is missing. That is true, but it says nothing useful about what the file actually is.

## What Changes

**A list backup, produced on request**

- A new read-only endpoint, `GET /api/my-list/backup`, returns one JSON file as a download. It holds:
  - **Header:**
    - `kind`, always `"listBackup"`, so the file says what it is
    - `formatVersion`, `1`
    - `device`, with the same `id` and `name` as the device-transfer header
    - `exportedAt`
  - **`entries`:** one per anime in my list, ordered by anime id. Each entry holds:
    - `animeId`
    - `title` and `englishTitle`, so the file can be read without looking anything up
    - `totalEpisodes`
    - `status`, written under its stored name, Rewatching included
    - `episodesWatched`
    - `score`, null where unscored
    - `startedAt` and `completedAt`, as dates, null where unset
    - `rewatchCount`
- An entry holds the values this app holds now, including an edit not yet sent to MyAnimeList. An anime I have removed is not in the file, even if its removal has not reached MyAnimeList yet.
- The file name is `bettermal-list-{first 8 hex digits of the device id}-{yyyyMMdd-HHmmss}.json`. It cannot be confused with a transfer file (`bettermal-{id}-…`).
- Producing a backup writes nothing, sends nothing, and happens only when I press the button. The entries are read in one statement, so a save that lands during a backup appears in the file entirely or not at all.

**Never in the file**

- Sync bookkeeping: whether an entry is waiting to be sent, when it last synced, and whether it is held for review.
- My MyAnimeList connection.
- The ranking, chosen pictures, series choices and the activity log. Those belong to the device-transfer file.
- Anime metadata beyond the two titles and the episode total, and browser preferences.

**The device-transfer import names a list backup**

- Given a list backup, the import refuses it by what it is, saying that it is a backup of my list and is never imported. It no longer calls the file damaged. Nothing else about the import changes.

**Settings**

- The Transfer group becomes **Files**. It holds every action that produces or takes a file:
  - the new **Back up my list** action, first
  - the device-transfer export and import, unchanged
- The backup action's explanation says:
  - what the file holds, including Rewatching, which MyAnimeList cannot hold
  - that it is a copy to keep, and the app never imports it
  - that producing it changes nothing and sends nothing anywhere

**Not in scope**

- Any import, restore or merge of a list backup.
- Other formats, such as MyAnimeList's XML or CSV.
- Scheduled or automatic backups.

## Capabilities

### New Capabilities

- `list-backup`: the saved copy of my list. It covers:
  - that a backup is produced only on request, is read-only, and is never imported
  - what the file holds and leaves out
  - its header and file name
  - how entries are selected and ordered
  - how values are written
  - that the file is one consistent snapshot

### Modified Capabilities

- `settings-page`:
  - **"Settings are organised into named groups"**: the fourth group is renamed from Transfer to Files, and holds the list backup as well as the transfer between devices.
  - **New requirement:** the list backup action states what the file holds, that it is never imported, and that it changes and sends nothing.
- `device-transfer`:
  - **"A file this build cannot read is refused by name"**: gains a refusal for a list backup, checked before the format version.

## Impact

**Backend**

- `Services/ListBackup/` (new):
  - `IListBackupService` and `ListBackupService`, which read the list in one projection and build the file
  - `ListBackupFile`, the file's record types
- The file reuses `Services/Transfer/ExportJson.Options` and `DeviceName.Describe`, so both files write values and name devices the same way.
- `Controllers/ListBackupController.cs` (new): `GET api/my-list/backup`, returning `application/json` as an attachment and passing the request's `User-Agent` to the service.
- `Services/Transfer/TransferFileReader.cs`: refuses a file whose `kind` is `"listBackup"`, before it reads `formatVersion`.
- `Program.cs`: registers the service.
- No schema change and no migration.

**Frontend**

- `api/client.ts`:
  - `exportListBackup()`
  - the blob-and-file-name download that `exportData()` performs, moved into one shared helper both calls use
- `pages/SettingsPage.tsx`:
  - the group renamed to Files
  - the Back up my list action
  - the save-by-temporary-link step shared with the export

**Tests**

- `ListBackupService`, asserted on the serialized JSON:
  - its members
  - selection and order
  - stored names and dates
  - nulls
  - unsent and held entries
  - pending removals
  - that nothing is written
- The file name.
- The controller's download headers.
- The reader's new refusal, including that a transfer file is still read as before.

**Operations**

- Nothing to migrate or configure. Rebuild the backend and frontend.
