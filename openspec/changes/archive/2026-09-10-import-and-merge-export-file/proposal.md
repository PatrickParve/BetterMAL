## Why

03 (`export-data-mal-cannot-carry`, archived) produces a file holding what exists only in this app: the ranking, chosen anime pictures, chosen series titles and pictures, and the activity log. Nothing reads that file yet, so edits made on one of my two devices still never reach the other.

This change is the other half. I drop a file exported on my other device into the Settings page, and it merges into this one by rules decided in advance:
- the activity log is combined as a union
- each chosen picture and title goes to whichever device changed it last
- the ranking goes, whole, to whichever device arranged it last

There is nothing to approve. The import applies, then tells me what it did.

**Verified against the code** (2026-09-10):

- **Fetching a missing anime needs no MyAnimeList connection.** `MalClient.GetAnimeDetailsAsync` uses client-id authentication, and `IMetadataRefreshService.RefreshOneAsync` already upserts a row for an anime this device has never cached. It needs MyAnimeList to be reachable, nothing more.
- **An import can take minutes, which one request cannot hold.**
  - nginx proxies `/api/` with no `proxy_read_timeout`, so the default of 60 seconds applies.
  - Fetches are paced at about one per second. An import may need a fetch for each unseen anime, a series build (up to 8 fetches each) for each series this device has not built, and a picture-set fetch for each choice that fails validation.
  - A request cut off at 60 seconds would also cancel the work underneath it.

  So the import runs as a background job, in the shape the three Data-tools jobs already use.
- **"The initial import has finished" is known only in memory.**
  - `ImportProgressTracker` is not persisted. The initial import re-runs on every start where a token exists, passing through `Running` for the seconds it takes to re-page the list, then reaches `Complete`.
  - If MyAnimeList is unreachable at start, the tracker stays `NotStarted` until the next authorization or restart.
  - Nothing in the frontend reads `/api/import/status` today.
- **The picker's writes cannot be reused as they are.**
  - Every write in `ArtworkSelectionService` stamps `DateTimeOffset.UtcNow`, and an import must store the time the file carries.
  - The validation those writes run (`:21-25`, `:57-59`, `:82-87`) is exactly what an import must pass, so the adopting writes belong in the same service, sharing that validation.
- **The retry-after-refresh needs a new option.** `PictureRefreshService.RefreshOneAsync` is a no-op once `PicturesSyncedAt` is set. A set fetched before MyAnimeList added the chosen picture fails validation just as a never-fetched one does, and would never be retried.
- **Ranking replacement is already exact.** `ReplaceAllAsync` (02) stores the given list and the given time, and rejects the whole list if any id has no row. `GetModifiedAtAsync` (03) reads the time to compare against.
- **The log's identifier is already enforced.** `ActivityLog.EventId` carries a unique index (02).
- **Insertion order decides how a save reads.** `ActivityFeedComposer.FindCompletionScoreMerges` folds a `Completed` record and a `ScoreChanged` record into one "Completed — Score 9" row only when, reading `(Timestamp desc, Id desc)`, the score comes first. The completion must therefore receive the lower local number, which means inserting in file order.
- **A change type unknown to this build must not break the file.** `ActivityChangeType` is append-only. A later build can add a value without changing the file's shape, and so without raising `formatVersion`. Read into the enum, that one record would fail the whole file.
- **The activity-recording spec contradicts the import.** It says the log "SHALL hold nothing else" and that "each device's log records only what was done on that device". After an import it also holds the other device's records.

## What Changes

**Importing a file**

- `POST /api/transfer/import` takes the file as its body. It checks the file and this device's state, then hands the file to a background job and answers at once.
- The request is refused **by name**, with nothing written, when:
  - the body is not an export file, or is damaged, naming the first member that is wrong
  - its `formatVersion` is newer than this build reads, naming both versions
  - it was exported from this device (`device.id` is this database's own identifier)
  - the initial MyAnimeList list import has not finished since the app started
  - another import is already running
- `GET /api/transfer/import/status` reports the job's phase and progress, and, once it has run, its report.

**The merge**

- **Activity log: a union.**
  - Every record whose identifier is not already present is inserted, in file order. Nothing is deleted, overwritten or re-timed, and a record already present is skipped.
  - A record whose change type this build does not know is not inserted, and is reported.
- **Chosen anime pictures, series titles and series pictures: the newer time wins, choice by choice.**
  - A series' title and picture are decided independently.
  - A cleared choice with a newer time clears.
  - The stored time becomes the file's time, never the time of the import.
  - A series is found through the file's series id's primary membership, so a choice lands correctly even if the series' root has moved on one side.
- **Ranking: the whole list goes to the newer side.**
  - If the file's ranking time is newer, the stored order becomes exactly the imported list, carrying the file's time. Anime missing from that list lose their places.
  - Otherwise the ranking is left alone.
  - If any anime in a newer list cannot be fetched, the ranking is left alone and the anime that blocked it is reported. Importing again once it can be fetched applies the list.
- **Choices pass the picker's own checks.**
  - A picture must be in the anime's or series' option set, and an anime picture needs the anime on my list.
  - A title must pass the contiguous-trim rule.
  - A picture outside its option set gets **one retry**: the picture sets behind that option set are fetched, even if fetched before, and the choice is checked again before anything is reported.
- **Anime this device has never seen are fetched, not reported.**
  - Every anime that something about to be applied names, and that has no row here, is fetched from MyAnimeList first. Both `ActivityLog` and `TopAnimeSelection` carry a foreign key to `AnimeMetadata`.
  - A series this device has not built is built the way a visit builds it.
  - These fetches are the same cache fills a page visit makes, and they stay even if the import later fails.
- **One transaction.** Everything the merge writes is applied in one transaction, after the fetching is done. The import either applies wholly or leaves nothing of the file behind.
  - Nothing is dropped or recreated, and the database keeps serving throughout.
  - A change saved on this device while the merge is being written makes the import fail, rather than letting an older value from the file overwrite it.
- **What the import never touches.** It touches no list entry and sends nothing to MyAnimeList. It records no activity of its own, beyond the records it imports. It leaves the device identifier alone.
- **Re-importing the same file changes nothing.** The identifiers exist, the times are not newer, and the ranking is identical.

**The report**

- Shown on the Settings page when the job finishes. It holds:
  - the anime added to the ranking and the anime removed from it
  - the anime fetched for the first time
  - everything that could not be applied, with its anime or series id, its title where known by then, and what failed
- It holds nothing else. A choice that applied cleanly gets no line, and there are no counts.
- An import with nothing to report says so in one line.
- It stays until the next import or a backend restart.

**Settings**

- The Transfer group gains an **Import from a file** action, beside the export. It accepts a file chosen from a picker or dropped onto it.
- Its explanation, readable before pressing:
  - what the import merges, and by which rules
  - that it asks nothing and cannot be undone, and that the ranking is replaced whole when the file's is newer, so exporting this device first is the habit
  - that it can take minutes when anime have to be fetched
- It reports progress in the page's shared job presentation, and its report beneath.

**Not in scope**

- A review or accept step, or a held-for-review table. Every rule is unconditional, and a review that can only say yes is not a review.
- Undoing an import, or exporting automatically before one.
- Propagating removals between devices. A `Removed` record imported into the log is history only and removes nothing (see the export-import README).
- Browser preferences, the anime-updates feed, and a my-list import.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `device-transfer`: gains the import. It adds requirements for:
  - refusing a file by name
  - refusing while the list is being imported
  - the union of the log
  - newer-wins choices through the picker's checks, with one retry after a refresh for pictures
  - whole-list ranking replacement
  - fetching unseen anime first
  - one transaction
  - the report
- `settings-page`:
  - **"Settings are organised into named groups"**: Transfer now holds the export and import actions.
  - **"Background jobs report progress the same way"**: covers the import as a fourth job, with its report as its finished state.
  - **New requirement:** the import action states what it merges, that it cannot be undone, and that exporting first is the habit.
- `activity-recording`:
  - **"Every change made in the app records what it changed"**: the log may also hold records imported from my other device's log, which that device recorded under the same rule. Importing them records nothing further.

## Impact

**Backend**

- `Services/Transfer/`:
  - the import-side file types and their reader. Change types are read as strings, and missing members are refused.
  - `TransferImportService`: accepts the file and applies the refusals.
  - a progress tracker and a single-slot trigger
  - `TransferImportBackgroundService`
  - `TransferImportRunner`: prepares, then applies in one transaction
  - the report types
- `Services/Artwork/ArtworkSelectionService` and its interface:
  - validate-only checks and adopting writes, taking a value and a time, for the anime picture, the series title and the series picture
  - the existing `Set*` methods share the same validation helpers
- `Services/Artwork/PictureRefreshService` and its interface: an option to fetch a picture set even when one was fetched before. It stays my-list-only.
- `Controllers/TransferController`: `POST api/transfer/import` and `GET api/transfer/import/status`.
- `Program.cs`: registrations.
- Read, not changed:
  - `ISeriesService.FindSeriesIdAsync` and `GetSeriesAsync`
  - `IMetadataRefreshService.RefreshOneAsync`
  - `ITopAnimeSelectionRepository.ReplaceAllAsync` and `GetModifiedAtAsync`
  - `IImportProgressTracker`
  - `RefreshGate`
- No schema change and no migration.

**Frontend**

- `api/client.ts` and `api/types.ts`: `importData(file)`, `getImportStatus()`, and the status and report types.
- `pages/SettingsPage.tsx` and `SettingsPage.css`:
  - the import action: file picker, drop target, progress and report
  - the report list and the drop highlight

**Tests**

- The reader:
  - every refusal
  - unknown members ignored
  - an unknown change type kept to its record
  - an export's own output read back
- The runner, on the in-memory provider with `TransactionIgnoredWarning` suppressed, as `ExportServiceTests` does, covering every merge scenario.
- `ArtworkSelectionService`'s adopting writes and checks.
- The picture refresh option.
- The tracker and trigger.
- The controller's status codes.
- `SeriesControllerBulkBuildTests`' test doubles of `IArtworkSelectionService` and `IPictureRefreshService` gain the new members.

**Operations**

- Nothing to migrate or configure.
- An import cannot be undone. Before the first import on each device, take a database dump and export that device.

**Consequences, by design**

- **Recaps change.** Past recaps read higher once the other device's progress arrives, because the recap's episode counts are built from `EpisodeIncremented` records (`Services/Recap/RecapService.cs:35`).
- **The ranking loses work.** Anime ordered only on this device lose their places when a newer ranking arrives from the other device.
