## 1. Reading the file

- [x] 1.1 Add the import-side record types in `Services/Transfer/TransferFile.cs` (design D3):
  - `TransferFile`, `TransferDevice`, `TransferRanking`, `TransferAnimePicture`, `TransferSeries`, `TransferSeriesChoice` and `TransferActivity`
  - times as `DateTimeOffset`
  - `TransferActivity.ChangeType` as a `string`
  - series blocks as nullable properties
- [x] 1.2 Add `TransferFileReader.Read(bytes)` and `TransferFileRefusedException` (D2 steps 1–3, D3):
  1. Peek `formatVersion` through a `JsonDocument`:
     - not an integer ≥ 1 → "not an export file"
     - greater than 1 → "newer format", naming both versions
  2. Deserialize with options copied from `ExportJson.Options`, adding `RespectRequiredConstructorParameters` and `RespectNullableAnnotations`.
  3. Report a damaged file by `JsonException.Path`.
- [x] 1.3 Map `ChangeType` to `ActivityChangeType` by exact name, rejecting numeric strings. An unknown name stays with its record as unmapped; it does not refuse the file.
- [x] 1.4 Reader tests:
  - Each refusal:
    - not JSON
    - not an object
    - `formatVersion` missing, non-integer or 0
    - a format-2 file with a broken shape, refused as newer and not as damaged
    - an activity record with no `id`, naming its path
    - a null where the format never writes one
  - An unknown member is ignored.
  - An absent series block is told apart from a cleared one.
  - An unknown change type is kept with its record.
  - A numeric change type is not mapped.
  - `ExportService`'s real output reads back through the reader, field for field.

## 2. Adopting choices through the picker's checks

- [x] 2.1 Extract `ArtworkSelectionService`'s validation into private helpers that `Set*` calls, leaving `Set*` behaving as today (D6):
  - anime picture: in my list, and in `AnimePicture.Options`
  - series title: `SeriesTitleRule.IsAcceptable` against the main line's `OfferedTitles`
  - series picture: `SeriesPicturePool.Build` plus the current selection
- [x] 2.2 Add `AdoptAnimePictureAsync`, `AdoptSeriesTitleAsync` and `AdoptSeriesPictureAsync(id, value?, modifiedAt)`:
  - a non-null value runs the helpers
  - null clears, with no check
  - the given time is stored
  - for an anime, `ResolvePictureUrl()` runs
  - a title is stored normalized
  - the class comment ("the only writers") and the interface docs are updated
- [x] 2.3 Add `CheckAnimePictureAsync` and `CheckSeriesPictureAsync`. They run the same helpers and write nothing.
- [x] 2.4 Add the new members to `SeriesControllerBulkBuildTests`' `IArtworkSelectionService` double.
- [x] 2.5 Tests:
  - `Adopt*` stores the given time, not now.
  - A clear needs no check, even for an anime not in my list.
  - A set is refused, with nothing modified, when:
    - the anime is not in my list
    - the picture is outside the option set
    - the title is not a trim of an offered title
  - The title is stored normalized.
  - `Check*` writes nothing.
  - The existing `Set*` tests still pass.

## 3. Refreshing a picture set that was already fetched

- [x] 3.1 Add `bool evenIfFetched = false` to `IPictureRefreshService.RefreshOneAsync` (D7). When set, eligibility no longer requires `PicturesSyncedAt == null`. It still requires the anime to be in my list, uses the same `RefreshGate` key, and never throws.
- [x] 3.2 Update `SeriesControllerBulkBuildTests`' `IPictureRefreshService` double to the new signature.
- [x] 3.3 Tests:
  - `evenIfFetched` fetches an already-fetched set again.
  - It is still a no-op for an anime not in my list.
  - The default behaviour is unchanged.

## 4. The job: tracker, trigger, background service

- [x] 4.1 Add `ITransferImportProgressTracker` as a singleton, modelled on `ISeriesBulkBuildProgressTracker`:
  - phases `NotStarted`, `Running`, `Complete` and `Failed`
  - `MarkPending`, `Start(total)`, `AddToTotal(n)`, `ReportProgress(done)`, `Complete(report)` and `Fail(reason)`
  - it keeps the file's `device.name` and `exportedAt`, and the last report, until the next `MarkPending`
- [x] 4.2 Add `ITransferImportTrigger` as a singleton, with a single slot:
  - `TryOffer(file)` returns false while a file is pending or running
  - `WaitAsync` hands the file over
  - the slot is released when the run ends
- [x] 4.3 Add `TransferImportBackgroundService`:
  - it waits on the trigger, opens a scope, and runs `TransferImportRunner` on the host's stopping token
  - on success, `Complete`
  - on an exception, `Fail`, saying nothing from the file was applied
- [x] 4.4 Tests:
  - the tracker's transitions, and the report kept until the next `MarkPending`
  - the trigger refuses a second offer while one is pending or running, and accepts once it ends
  - the background service fails the tracker when the runner throws

## 5. Accepting a file

- [x] 5.1 Add `ITransferImportService.AcceptAsync(bytes)`. It checks D2's refusals in order (D2, D12):
  1. the reader
  2. `device.id` against `DeviceIdentities`
  3. `IImportProgressTracker.Snapshot.Phase == Complete`, with the counts while `Running` and the since-start wording while `NotStarted`
  4. `TryOffer`

  It then calls `MarkPending` and returns the status.
- [x] 5.2 The refusal carries its HTTP status (400 for a file problem, 409 for a state problem) and its message.
- [x] 5.3 Tests:
  - every refusal, in order, with nothing written and no file offered
  - an accepted file reaches the trigger, and the tracker reads `Running`

## 6. The runner: prepare

- [x] 6.1 `TransferImportRunner`'s planning read (D4, D5):
  - it reads the stored `EventId`s, the choice times, the ranking time, and which anime have rows
  - it picks the candidates: strictly newer, compared at full precision; a duplicated id considered once; the ranking only when the file's time is not null
- [x] 6.2 Fetch the unseen anime that candidates and series entries name (D8):
  - under `RefreshGate.LockAsync("anime:{id}")`, re-checking the row, via `IMetadataRefreshService.RefreshOneAsync`
  - success → listed as fetched for the first time
  - `AnimeMetadataNotFoundException` → "MyAnimeList has no anime with this id"
  - anything else → "could not be fetched from MyAnimeList"
- [x] 6.3 Resolve series (D8):
  - `FindSeriesIdAsync`; when that returns null, `GetSeriesAsync` and then `FindSeriesIdAsync` again
  - `SeriesNotFoundException` → "not part of a series on this device"
  - anything else → "the series could not be built"
- [x] 6.4 Pre-check each candidate that sets a picture with `Check*Async` (D7). When it is refused, refresh with `evenIfFetched: true`:
  - an anime choice: the anime itself
  - a series choice: each my-list main-line member, in main-line order
- [x] 6.5 Report progress:
  - the total is the anime to fetch plus the series to build
  - picture refreshes are added to the total as they are found
  - each fetch, build or refresh advances `done`

## 7. The runner: apply

- [x] 7.1 Open a fresh DI scope and `BeginTransactionAsync(IsolationLevel.RepeatableRead)`. Re-read and re-decide every comparison inside it (D11).
- [x] 7.2 Insert the log records (D10):
  - read the stored `EventId`s into a set
  - in file order, for each new record whose anime has a row and whose change type is known: add it, `SaveChanges`, then add its id to the set
  - records skipped for an unfetchable anime or an unknown change type get failure lines carrying their count
- [x] 7.3 Adopt anime pictures through `AdoptAnimePictureAsync`. A rejection gets a failure line, and the apply carries on.
- [x] 7.4 Adopt series choices, per resolved series and block, in file order, through `AdoptSeriesTitleAsync` and `AdoptSeriesPictureAsync`. A rejection gets a failure line.
- [x] 7.5 Apply the ranking (D9):
  - when it is a candidate and every id has a row: read the stored order, call `ReplaceAllAsync(list, fileTime)`, and compute added and removed by membership
  - otherwise, or on `UnknownAnimeIdsException`, add a ranking failure line naming the anime that blocked it
- [x] 7.6 Commit. Any other exception rolls back and reaches the job's `Fail`.
- [x] 7.7 Resolve the report's titles when the run ends (D13):
  - an anime: its stored title and English title
  - a series: `SeriesIdentity.Resolve`, falling back to the root anime's title

## 8. Runner tests

These run on the in-memory provider with `TransactionIgnoredWarning` suppressed, with fakes for the metadata, series and picture-refresh services.

- [x] 8.1 Log:
  - new records are stored, and present ones skipped
  - a duplicate within the file is skipped
  - nothing is re-timed
  - a completion then a score at `T` compose into one "Completed — Score" row through `ActivityFeedComposer`
  - an unknown change type is reported, and the rest is stored
  - a removal record leaves the list entry in place
- [x] 8.2 Choices:
  - a newer choice wins, carrying the file's time
  - an older or equal choice is left alone
  - a newer clear wins
  - with no time stored, the file's choice is stored
  - a series' title and picture are decided independently
  - an absent block is left untouched
  - a moved root resolves through membership
  - two file entries on one series: the newest wins
- [x] 8.3 Checks and the retry:
  - a never-fetched set is accepted after its refresh
  - a set fetched before is fetched again
  - a choice still refused is reported
  - an anime not in my list is reported
  - a title that is not a trim is reported
  - a clear passes with no check
- [x] 8.4 Ranking:
  - a newer ranking replaces this one and drops the missing anime
  - an older, equal or null one is left alone
  - an emptied newer ranking empties this one and carries its time
  - an unfetchable anime leaves the ranking alone and is reported
  - the added and removed lists are correct, and a pure reorder lists nothing
- [x] 8.5 Fetching:
  - an unseen anime is fetched and listed
  - a 404 is reported, and that anime's records are skipped
  - an unbuilt series is built
  - an unbuildable series is reported
- [x] 8.6 Re-importing and side effects:
  - re-importing the same file changes nothing and reports nothing
  - no list entry is written and no `PendingSync` is set
  - the import records no activity of its own
  - the device id is unchanged

## 9. Controller and registration

- [x] 9.1 `TransferController` (D1):
  - `POST api/transfer/import` reads the raw body into `AcceptAsync`, answering `202` with the status, or `400`/`409` with `{ "error": … }`
  - `GET api/transfer/import/status`
- [x] 9.2 `Program.cs`:
  - the tracker and trigger as singletons
  - the import service and runner as scoped
  - the hosted service
- [x] 9.3 Controller tests: `202` reading `Running`, the `400` and `409` bodies, and the status shape.

## 10. Frontend

- [x] 10.1 `api/types.ts`: the import status, report, anime-line and failure-line types.
- [x] 10.2 `api/client.ts`: `importData(file)`, posting `file.text()` as `application/json` through `fetchRaw` and throwing `ApiError` with the backend's reason; and `getImportStatus()`.
- [x] 10.3 `SettingsPage.tsx` gains an **Import from a file** `SettingsAction` in Transfer, after the export (D14):
  - the hint text the settings-page spec requires
  - **Choose file…** opening a hidden file input
  - a drop target on the whole action, highlighted while a file is held over it
  - while running: the button disabled and reading **Importing…**, the status polled every second, and `JobProgress` with the noun "fetches"
  - a refusal shown as `settings-box__error`
  - when complete: a line naming the file's device and export time, then the report lists, titled with `pickDisplayTitle`, or "Nothing to report."
  - when failed: the failed state and its reason
  - `getImportStatus()` added to `load()`
- [x] 10.4 `SettingsPage.css`: the report list and the drop highlight. Check the page at a narrow width.

## 11. Verification

- [x] 11.1 Build and run the backend tests in the `mcr.microsoft.com/dotnet/sdk:10.0` image, from a copy under `/private/tmp`.
- [x] 11.2 Build and lint the frontend with nvm's Node 22.
- [x] 11.3 Deploy, following the design's Migration Plan:
  1. On this device, take a dump and confirm `pg_restore --list` reads it, and export.
  2. Deploy: `docker compose up -d --build backend frontend`.
  3. Once a second device exists, verify the cross-device merge — import, re-import, cross-export, and the concurrent-save check — per the design's Migration Plan. Not a condition of this change, since only one device runs the app today.
