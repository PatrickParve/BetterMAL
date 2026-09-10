## Why

I use the app on two devices and edit on both without syncing them first. Everything that comes from MyAnimeList already reaches both devices and rebuilds itself. What exists only in this app has no route between them at all: the ranking, chosen pictures, chosen series titles and pictures, and the activity log.

This change produces a file holding exactly that data. Change 04 imports it. 01 made each device's log hold only its own edits, and 02 gave the ranking one last-modified time and gave every log row a GUID. Those were the prerequisites. Nothing is exported yet.

**Verified against the code and the live database** (2026-09-10):

- **The ranking:** 455 rows (the brief said 453). None of them is for an anime outside my list. `RankingStates` holds one row, 2026-09-09 07:59:51.127889 UTC.
  - Nothing reads that time yet. 02 left the reader to this change.
  - The order is read by `TopAnimeSelectionRepository.GetOrderedAnimeIdsAsync`, sorted on `(Position, AnimeId)`.
- **Chosen anime pictures:** 197 anime have a non-null `SelectedPictureModifiedAt` (the brief said 190+). One of them is a clear: a null URL with a real time. No anime has a URL without a time.
- **Series choices:** 114 series have at least one choice time (the brief said 105+):
  - 34 have a title time
  - 97 have a picture time
  - 17 have both

  So 80 of the 114 carry only one of the two blocks, and "absent" has to be expressible in the file.
- **The activity log:** 913 rows, not 546. 01 deleted only the 4 rows written by sync paths; the other 913 were all written in the app, so all of them travel. 94 timestamps are shared by more than one row, and 11 anime in the log are no longer in my list.
- **Stored times are microsecond-precise** (e.g. `2026-09-09 21:26:43.973829+00`). A file that truncated them to milliseconds would carry a different time from the one stored.
- **No machine name can be read automatically.**
  - Inside Docker, the backend's hostname is its container id (`886ab0e45e3d`), and that changes on every rebuild.
  - The Mac's own name is not exported to the shell, so `docker compose` could only pass it in if it were set by hand.
  - A browser cannot read it at all.

  The device is therefore identified by an id the app generates for itself. The browser and operating system that asked for the export supply a readable name, with nothing to configure.
- **Correction to the brief:** the time filter on chosen pictures coincides with my list today, with no exceptions. But that is not guaranteed:
  - `ArtworkSelectionService.cs:21-22` refuses a *set* on an anime outside my list.
  - `ResetAnimePictureAsync` (`:34-47`) has no such check.
  - Removing an anime from my list leaves its choice and time in place.

  The export still filters on the time alone, as the brief asks (design D8).
- **Enum storage:** `ActivityLog.ChangeType` is stored with `HasConversion<string>()`. The API's JSON options already add a `JsonStringEnumConverter` with no naming policy, so a C# enum name is written exactly as it is stored.

## What Changes

**A device identity, created automatically**

- A new singleton `DeviceIdentity` row holds a GUID.
  - The first time the upgraded backend starts on a database, it creates the row. Afterwards nothing writes it, so it survives restarts and rebuilds and never changes.
  - Nothing is configured.
- Two separately created databases, meaning my two devices, get different ids.

**An export file, produced on request**

- A new read-only endpoint produces one JSON file and hands it to the browser as a download. It holds the following, and nothing else:
  - **Header:** `formatVersion` (1), `exportedAt`, and `device`. `device` holds the device `id`, plus a `name` such as `"macOS · Safari"`, read from the export request's `User-Agent` and null where it cannot be recognised.
  - **Ranking:** the stored order as a bare list of anime ids, plus the ranking's one last-modified time. The array's order is the position; no positions are written.
  - **Chosen anime pictures:** one entry per anime whose choice time is not null, including clears. Each entry has the URL, which may be null, and the time.
  - **Series choices:** one entry per series with at least one choice time. The title and the picture are separate blocks, each with its own value and time. A block that was never set or cleared is **left out**, never written as null.
  - **Activity log:** every record, oldest first, ordered by timestamp and then by storage order. Each carries its GUID as `id` and never the local number.
- The file name carries the first 8 hex digits of the device id and the moment of export, e.g. `bettermal-7f3c9a12-20260910-182204.json`.
- Every time in the file is the one stored, written at full precision. `exportedAt` is the only time stamped at export.
- The reads share one snapshot. A save landing during an export appears in the file wholly or not at all. In particular, the ranking's list and its time always come from the same moment.
- Producing an export writes nothing and sends nothing anywhere. No export happens unless I press the button.

**Never exported**

- Anything that comes from MyAnimeList or rebuilds itself: anime metadata, season listings, airings, relations, series structure and membership, and top-anime rankings.
- `OAuthTokens`.
- The anime-updates feed (`AnimeUpdates`, `RelationDiscoveries`).
- Sync bookkeeping: pending, held and reconciliation state.
- Browser preferences in `localStorage`.

**A fifth Settings group**

- A new group, **Transfer**, sits after Data tools and before Account. It holds the export action now and 04's import action later, and nothing else.
- The export action's explanation says:
  - what the file holds and what it leaves out
  - that it changes nothing here and sends nothing anywhere
  - that getting the file to the other device is up to me

**Not in scope**

- Importing. That is 04, which decides whether to use the device id (for example, to recognise a file this device exported itself).
- Browser preferences, the anime-updates feed, and a my-list export.
- Removals propagating between devices. That is a separate live bug (see the export-import README).

## Capabilities

### New Capabilities

- `device-transfer`: the file that carries app-only data between my devices. It covers:
  - what the file contains and excludes
  - its header, the device id and name, and the format version
  - how each section is selected and ordered
  - how values are written
  - that an export is a consistent, read-only snapshot, produced only on request

  04 will add the import to this capability.

### Modified Capabilities

- `data-persistence`:
  - **New requirement:** each database carries one device identifier. The application generates it on first start, never configures or rewrites it, and it lasts as long as the database does.
- `settings-page`:
  - **"Settings are organised into named groups"** gains a fifth group, Transfer, between Data tools and Account, and the ordering rule is restated to place it.
  - **New requirement:** the export action states what the file holds, what it leaves out, and that nothing leaves the device by itself.

## Impact

**Backend**

- `Models/DeviceIdentity.cs` (new) and `Data/AnimeTrackerDbContext.cs`: a `DeviceIdentities` set.
- `Services/Transfer/` (new):
  - `DeviceIdentityInitializer`, which creates the id at startup when there is none
  - `DeviceName`, which turns a `User-Agent` into `"OS · Browser"`
  - `ExportService`, which reads the snapshot and builds the file
  - the file's record types and its serializer options
- `Controllers/TransferController.cs` (new): `GET /api/transfer/export`, returning `application/json` as an attachment. It passes the request's `User-Agent` to the service.
- `Program.cs`: registers the services, and calls the initializer in the startup scope right after `Migrate()`.
- `Data/Repositories/ITopAnimeSelectionRepository.cs` and its implementation: `GetModifiedAtAsync`, the ranking-time reader 02 deferred. 04 needs the same read.
- `Data/Repositories/IActivityLogRepository.cs` and its implementation: `GetAllOldestFirstAsync`. It orders ascending on `(Timestamp, Id)` and does not `Include` the anime.
- One EF migration, `AddDeviceIdentity`, which only creates the table, plus the updated model snapshot.

**Frontend**

- `api/client.ts`: `exportData()`, which fetches the file as a blob and reads its name from `Content-Disposition`.
- `pages/SettingsPage.tsx`: the Transfer group and its export action.

**Tests**

- 12 test doubles of `ITopAnimeSelectionRepository` and 13 of `IActivityLogRepository` each gain a throwing stub.
- New repository tests for the two reads.
- Tests for the initializer, for `DeviceName` and for `ExportService`, covering every spec scenario and run against the serialized JSON.
- A controller test for the download headers.

**Unchanged**

- No change to any writer of the ranking, the choices or the log.

**Operations**

- The migration applies when the rebuilt backend starts, and the device id is created in the same startup. Nothing has to be set on either device. A dump is taken first, as for every migration.
- After 03 ships, 02's migration must not be rolled back on a device that has exported: its `Down` discards the GUIDs the files carry.
- One device's database must not be restored onto the other device. The two would then share a device id. The file is the route between them.
