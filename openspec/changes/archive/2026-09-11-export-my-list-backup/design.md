## Context

The device-transfer export (`export-data-mal-cannot-carry`, archived) and its import (`import-and-merge-export-file`, archived) move what exists only in this app between my two devices. Both leave my list out on purpose, since MyAnimeList carries it. This change adds the missing piece: a saved copy of my list. The copy is produced on request and is never read back.

**Where the list lives today:**
- `UserAnimeEntries` has one row per anime in my list, keyed by `AnimeId`.
- `Status` is stored as a string (`HasConversion<string>()`, `Data/AnimeTrackerDbContext.cs:47`).
- The other stored values are `EpisodesWatched`, `MyScore`, `StartedAt`/`CompletedAt` (`DateOnly`) and `RewatchCount`, beside the sync bookkeeping (`PendingSync`, `LastSyncedAt`, `HeldForReviewAt`).
- Each entry shares its key with an `AnimeMetadata` row. That row holds `Title`, `EnglishTitle` and `TotalEpisodes`, the total the app displays (MyAnimeList's, else AniList's).
- Removing an anime deletes its entry in the same transaction that queues the removal for MyAnimeList (`data-persistence`, "Pending entry removal record").

**Current data** (verified 2026-09-11):
- 617 entries: 462 Completed, 114 PlanToWatch, 27 Dropped, 8 Watching, 4 OnHold, 2 Rewatching.
- 142 have no score, and 44 have been rewatched.
- 49 have no English title, and 38 have no known total.
- None has an edit waiting to be sent, and none is held.

**Pieces this change reuses, unchanged:**
- `ExportJson.Options`: web defaults, string enums with no naming policy, indented output.
- `DeviceName.Describe`.
- The `DeviceIdentities` singleton.
- The Settings page's `SettingsGroup` and `SettingsAction`.
- The blob-download flow from `export-data-mal-cannot-carry` D7.

**Constraints:**
- Tests use the EF in-memory provider.
- The backend builds and tests only in the `mcr.microsoft.com/dotnet/sdk:10.0` image, from a copy under `/private/tmp`.
- The frontend builds with nvm's Node 22.

## Goals / Non-Goals

**Goals:**
- One JSON file holding every entry in my list, readable on its own, with Rewatching kept.
- A header that says what the file is, and a file name that can't be confused with a transfer file.
- A consistent, read-only snapshot, produced only on request.
- A clear refusal when a backup is dropped onto the transfer import.
- A Back up my list action on the Settings page.

**Non-Goals:**
- Importing, restoring or merging a backup, in any form.
- MyAnimeList's XML format, CSV, or any second format.
- Scheduled or automatic backups, or keeping backups on the server.
- Refusing to back up during the initial list import (see Risks).

## Decisions

### D1. A separate file, service and endpoint, not a section of the transfer file

The backup is its own file, built by `Services/ListBackup/ListBackupService` behind `IListBackupService`, and served by a new `Controllers/ListBackupController` as `GET /api/my-list/backup`. The controller passes `Request.Headers.UserAgent` to the service and returns `File(bytes, "application/json", fileName)`, the same shape as `TransferController.Export`.

- **Not a section of the transfer file.** That file's contract is "nothing that comes from MyAnimeList" (`device-transfer`), and its import reads every section it carries. Adding the list would break the first and make the second promise to leave my list alone conditional. It would also make every backup carry the ranking and log, and every transfer carry the list.
- **Not on `TransferController`.** That controller moves data between devices, and a backup moves nothing.
- **Not on `MyListController`.** That controller serves the My List page's reads. The backup sits under the same `api/my-list` prefix because it's the same data, but in its own controller, as the transfer export does.
- **`GET`**, because the request has no side effect and can be repeated freely.

### D2. JSON, not MyAnimeList's XML or CSV

- **Nothing reads the file but me.** MyAnimeList's XML helps when a file is going to be imported somewhere, and this one never is.
- **XML would lose what makes this backup worth having.** It has no Rewatching status and no null score (unscored is `0`), and nothing here could check its output against MyAnimeList's importer.
- **CSV would flatten nulls and types,** and would need quoting rules for titles.
- **JSON reuses what already exists:** `ExportJson.Options` writes stored enum names, ISO times and indented output, and the device-transfer export already proves that path.

### D3. One projection is the snapshot

The service reads the list in one query:

```csharp
db.UserAnimeEntries.AsNoTracking()
    .OrderBy(e => e.AnimeId)
    .Select(e => new { e.AnimeId, e.Anime.Title, e.Anime.EnglishTitle, e.Anime.TotalEpisodes,
                       e.Status, e.EpisodesWatched, e.MyScore, e.StartedAt, e.CompletedAt, e.RewatchCount })
```

That query is one SQL statement, a join, and Postgres runs every statement against one snapshot. So an edit that lands during a backup appears in every column or in none, and an added or removed anime is present or absent as a whole. That is the whole "one consistent snapshot" requirement. Unlike the transfer export (its D2), no `RepeatableRead` transaction is needed: there is only one data read.

The device id is read separately, with `SingleAsync` on `DeviceIdentities`. It never changes once created, so reading it apart from the list can't mix two moments.

**The selection follows from the query:**
- **Unsent edits and held entries:** the row holds the local values, so those are what the file carries.
- **Pending removals:** the entry row is deleted together with queuing the removal, so the anime is absent without a separate filter.
- **Anime known only from browsing:** they have no entry row, so they are absent.

The projection reads only the ten columns the file needs, not whole metadata rows with their picture sets and synopses.

*Alternative:* reuse the My List page's repository read. Rejected: it carries page-shaped data, and the file's shape should not follow the page.

### D4. The file's own record types, on the transfer export's serializer options

`Services/ListBackup/ListBackupFile.cs` holds:
- `ListBackupFile(string Kind, int FormatVersion, ExportDevice Device, DateTime ExportedAt, List<ListBackupEntry> Entries)`
- `ListBackupEntry(int AnimeId, string Title, string? EnglishTitle, int? TotalEpisodes, WatchStatus Status, int EpisodesWatched, int? Score, DateOnly? StartedAt, DateOnly? CompletedAt, int RewatchCount)`

They are serialized to UTF-8 bytes with `ExportJson.Options`:
- **camelCase member names.** Records serialize in declaration order, so `kind` comes first.
- **Status as its stored name.** `WatchStatus` goes through `JsonStringEnumConverter` with no naming policy, so it is written `PlanToWatch` and `Rewatching`, the names `HasConversion<string>()` stores.
- **Dates:** `DateOnly` is written natively as `"yyyy-MM-dd"`.
- **Times:** `ExportedAt` is a `DateTime` of kind `Utc`, so it is written with a `Z`, as in the transfer export's D5.
- **Nulls:** no property carries `[JsonIgnore]`, so every null is written and every entry has all ten members.

`ExportDevice` is reused rather than copied, since the spec defines the backup's `device` as the transfer header's.

**Naming:** the member is `score`, not `myScore`. The file holds no MyAnimeList community score, so nothing can be confused with it.

*Alternative:* a separate options object for the backup. Rejected: it would be a copy, and the two files are meant to write values the same way. `ExportJson.Options` is already a stable contract, because changing it would break the transfer file.

### D5. Both stored titles, not one display title

Each entry carries `title` (MyAnimeList's stored title, never null) and `englishTitle` (null where there is none). It does not carry the one label the app displays, English falling back to MyAnimeList's title:
- A derived label would drop MyAnimeList's title for the 568 anime that have an English one. A backup should hold what is stored.
- Both titles help someone find an anime by eye, or match it elsewhere.

`totalEpisodes` is the effective `TotalEpisodes`, the figure shown next to my progress in the app, so an entry reads as "12 of 24" the same way the app shows it.

### D6. A `kind` marker, and the transfer import refuses it by name

The backup's header starts with `"kind": "listBackup"`.

`TransferFileReader.PeekFormatVersion`, which already parses the document and checks that the root is an object, checks `kind` before reading `formatVersion`. When `kind` is the string `"listBackup"`, it throws `TransferFileRefusedException("This is a backup of your list. A list backup is never imported.")`. The Settings page already shows a refusal's text in the import action.

- **A marker, not detection by members.** "Has `entries` and no `ranking`" would still read as damaged under a later shape. A marker says what the file is, and it can't be mistaken.
- **Before `formatVersion`.** A backup carries `formatVersion` 1 too, and in its own sense. Checking the marker first means the version never gets compared across the two formats.
- **Transfer files carry no `kind`,** and the reader already ignores members it does not define. So a transfer file is read exactly as before, and the transfer export does not change. Its top-level members stay the fixed seven.

### D7. File name: `bettermal-list-{id8}-{yyyyMMdd-HHmmss}.json`

`ListBackupService.BuildFileName(Guid deviceId, DateTime exportedAtUtc)` is `internal static`, like `ExportService.BuildFileName`, so it can be tested directly. The `list` segment keeps a backup from ever reading as a transfer file in a downloads folder. The device prefix and the timestamp keep backups from my two devices, and several from one, apart.

### D8. Frontend: one shared download helper, separate state per action

**`api/client.ts`:**
- The body of `exportData()` becomes a private `fetchDownload(url, fallbackName)`, which goes through `fetchRaw`, throws `ApiError` on a non-ok response, and reads the file name from `Content-Disposition`.
- `exportData()` and the new `exportListBackup()` both call it. The fallback names are `bettermal-export.json` and `bettermal-list.json`.

**`SettingsPage.tsx`:**
- The object-URL-and-anchor step inside `handleExport` becomes a local `saveFile(blob, fileName)`, which `handleExport` and the new `handleListBackup` share.
- The backup has its own state: `backingUp`, `backupFileName` and `backupError`. So its outcome never shows on the export action, and the other way round.
- The button reads **Back up**, and **Backing up…** while it runs.
- The existing `settings-box__hint` and `settings-box__error` classes cover everything, so no CSS changes.

### D9. The group is renamed Files

The fourth group now holds a backup, which moves nothing between devices, so "Transfer" no longer names it.

- **Files** names what its three actions share: each one produces or takes a file. It is still one word, like its neighbours.
- **The hint** becomes "Save a copy of your list, or move what only this app holds between your devices, as a file."
- **"Import" is still avoided** as a group name, for the reason in `export-data-mal-cannot-carry` D10: it already names the initial MyAnimeList list import.
- **Placement and ordering stay:** the group stays after Data tools because it holds the one action that cannot be undone. Within the group, the backup comes first and the import last, running from harmless to irreversible.

*Alternatives:*
- A separate Backup group. Rejected: one button would get its own group, and by the ordering rule it would sit before Data tools, splitting apart the three actions that belong together.
- Keep "Transfer". Rejected: the name would be wrong for one of its three actions.
- Data tools. Rejected: that group is defined as the long-running corrective and backfill jobs.

Code comments that tie `api/transfer/` to "the Transfer group" (`TransferController`) are updated to say Files. The route itself does not change.

## Risks / Trade-offs

- **[A backup taken during the very first list import is partial]** → The initial import fills the list progressively, so a backup taken then holds only what has arrived. This is accepted: on every later start the import re-pages a list that is already complete, and the file's entry count is visible. Refusing a backup whenever the import reads `Running` would block it for those seconds on every start, for a case that happens once per device.
- **[Titles and totals are a snapshot of metadata]** → They change on MyAnimeList over time. `animeId` is what identifies the anime, and the spec says so. The titles are there to read by.
- **[Reusing `ExportJson.Options` ties the two files together]** → That is intended: both files should write values one way. A change to those options already needs care because of the transfer contract.
- **[The file holds my personal list]** → It holds no credentials, since `OAuthTokens` is excluded by the spec. Where I keep it is up to me.
- **[The group rename breaks muscle memory]** → The actions keep their titles, hints, buttons and position, and only the group's heading and hint change.

## Migration Plan

1. Rebuild with `docker compose up -d --build backend frontend`. There is no migration.
2. On the Settings page, press **Back up** and check the file against the database:
   - 617 entries, matching `count(*)` on `UserAnimeEntries`
   - 2 entries with `Rewatching`
   - one unscored entry written with `score: null`
   - one entry with both dates
   - one entry with no English title
   - the ascending order of `animeId`
   - `device.id` equal to the transfer export's
3. Drop the backup onto **Import from a file** and confirm it is refused as a list backup.
4. Drop a transfer file this device exported, and confirm it is still refused as "from this device", which shows transfer files are read as before.

**Rollback:** check out the previous commit and rebuild. The backup stores nothing.

## Open Questions

None.
