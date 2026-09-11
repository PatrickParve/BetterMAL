## 1. Backend: the backup file

- [x] 1.1 Create `Services/ListBackup/ListBackupFile.cs` with the `ListBackupFile` and `ListBackupEntry` records (design D4). Reuse `ExportDevice`, and put no `[JsonIgnore]` on any property.
- [x] 1.2 Create `IListBackupService` and `ListBackupService.ExportAsync(string? userAgent, CancellationToken ct)`, returning `(byte[] Bytes, string FileName)`:
  - read the device id with `SingleAsync`
  - read the list in the one ordered projection (D3)
  - set `exportedAt` to `DateTime.UtcNow`
  - write `kind` `"listBackup"` and `formatVersion` 1
  - serialize with `ExportJson.Options`
- [x] 1.3 Add `internal static BuildFileName(Guid deviceId, DateTime exportedAtUtc)`, which returns `bettermal-list-{first 8 hex digits}-{yyyyMMdd-HHmmss}.json` (D7).
- [x] 1.4 Create `Controllers/ListBackupController` with `GET api/my-list/backup`. It passes `Request.Headers.UserAgent` to the service and returns `File(bytes, "application/json", fileName)`.
- [x] 1.5 Register `IListBackupService` in `Program.cs`, beside `IExportService`.

## 2. Backend: the transfer import names a list backup

- [x] 2.1 In `TransferFileReader.PeekFormatVersion`, once the root is known to be an object and before `formatVersion` is read, refuse a file whose `kind` is the string `"listBackup"` with "This is a backup of your list. A list backup is never imported." (D6).
- [x] 2.2 Update `TransferController`'s class comment, which ties the route to "the Transfer group", to say the Files group (D9). The route does not change.

## 3. Backend tests

- [x] 3.1 Add `ListBackupServiceTests`. Use the in-memory provider and assert on the parsed JSON, as `ExportServiceTests` does. Cover:
  - **the file:**
    - the five top-level members, in order
    - `kind`, `formatVersion`, and `device.id`/`device.name`
    - no member anywhere for sync bookkeeping (`pendingSync`, `lastSyncedAt`, `heldForReviewAt`)
  - **selection:**
    - one entry per anime in my list, ordered by anime id
    - an anime with metadata but no entry is absent
    - an entry with `PendingSync` and an entry with `HeldForReviewAt` carry their local values
    - an empty list gives an empty `entries`
  - **values:**
    - all ten entry members are present on every entry
    - nulls are written for `score`, `startedAt`, `completedAt`, `englishTitle` and `totalEpisodes`
    - `Rewatching` and `PlanToWatch` are written as those strings
    - dates are written as `yyyy-MM-dd`
    - `exportedAt` ends in `Z`
  - **read-only:**
    - no stored entry differs after a backup, and the change tracker holds nothing
    - two backups with no change in between differ only in `exportedAt`
- [x] 3.2 Add `ListBackupServiceFileNameTests` for the spec's file-name scenario (`bettermal-list-7f3c9a12-20260911-091530.json`).
- [x] 3.3 Add `ListBackupControllerTests`: the file result carries the service's content type and file name, and the request's `User-Agent` reaches the service.
- [x] 3.4 Extend `TransferFileReaderTests`:
  - a list backup is refused with its message
  - a list backup carrying `formatVersion` 2 is still refused as a list backup, not as a newer format
  - a transfer file carrying a `kind` with any other value reads as before
- [x] 3.5 Run the full backend test suite in the `mcr.microsoft.com/dotnet/sdk:10.0` image, from a copy under `/private/tmp`. Every test passes.

## 4. Frontend

- [x] 4.1 In `api/client.ts`:
  - move the body of `exportData()` into a private `fetchDownload(url, fallbackName)`
  - have `exportData()` call it, with the fallback `bettermal-export.json`
  - add `exportListBackup()` on `/api/my-list/backup`, with the fallback `bettermal-list.json`
- [x] 4.2 In `SettingsPage.tsx`:
  - move the object-URL-and-anchor step out of `handleExport` into a local `saveFile(blob, fileName)`
  - add the `backingUp`, `backupFileName` and `backupError` state, and `handleListBackup`, which uses `saveFile`
- [x] 4.3 Rename the Transfer group to **Files**, with the hint "Save a copy of your list, or move what only this app holds between your devices, as a file." Add the **Back up my list** action first in the group:
  - **Hint:** "Saves every anime in your list — its status, progress, score, dates and rewatch count — as a file. Rewatching stays Rewatching, which MyAnimeList can't hold. Holds nothing else: not your ranking, pictures or edit history, and not your MyAnimeList connection. It's a copy to keep: the app never imports it, and it isn't the file for your other device. Producing it changes nothing here and sends nothing anywhere; the browser saves the file."
  - **Button:** **Back up**, reading **Backing up…** while it runs
  - **On success:** "Saved {fileName}."
  - **On failure:** "The backup could not be produced. Please try again."
- [x] 4.4 Type-check and build the frontend with nvm's Node 22. Both complete with no errors.

## 5. Verify on the live stack

- [x] 5.1 Rebuild with `docker compose up -d --build backend frontend`.
- [x] 5.2 Press **Back up** and check the file against the database (design, Migration Plan step 2):
  - the entry count
  - the two Rewatching entries
  - an unscored entry with `score: null`
  - an entry with both dates
  - an entry with no English title
  - ascending `animeId`
  - `device.id` equal to the transfer export's
- [x] 5.3 Drop the backup onto **Import from a file**. It is refused as a list backup, and nothing changes.
- [x] 5.4 Drop a transfer file this device exported. It is still refused as exported from this device.
- [x] 5.5 Check the Settings page:
  - the Files group holds Back up my list, Export to a file and Import from a file, in that order
  - a backup's outcome does not show on the export action
