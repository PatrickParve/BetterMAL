## Context

This is the last of four changes (01–04) that move my data between my two devices:
- **01** (`log-only-my-own-edits`, archived) made each device's log hold only what was done on that device.
- **02** (`make-ranking-and-log-portable`, archived) did two things:
  - gave the ranking one last-modified time, on the singleton `RankingState` row
  - gave every log record an `EventId` GUID, with a unique index, and added `ReplaceAllAsync`
- **03** (`export-data-mal-cannot-carry`, archived) produces the file. It added:
  - `GetModifiedAtAsync`, the ranking-time read
  - `GetAllOldestFirstAsync`, the ascending log read
  - a device identifier
  - the Transfer group on the Settings page

This change reads the file. The file is a contract between two builds that may not be deployed at the same time (03 D4).

**What already exists to build on:**

| Need | Existing piece |
| --- | --- |
| Replace the ranking with a given list and time | `ITopAnimeSelectionRepository.ReplaceAllAsync` (02). Rejects the whole list if any id has no row. |
| Read the ranking's time | `ITopAnimeSelectionRepository.GetModifiedAtAsync` (03) |
| Fetch and cache an anime never seen | `IMetadataRefreshService.RefreshOneAsync`. Upserts. Client-id auth, so no MAL connection is needed. Asks for the picture set only for my-list anime. |
| Stop one anime being fetched twice at once | `RefreshGate.LockAsync("anime:{id}")`, the detail page's key |
| Find a series from an anime | `ISeriesService.FindSeriesIdAsync`: the anime's primary membership |
| Build a series the way a visit does | `ISeriesService.GetSeriesAsync` (visit budget, single-flight by series key) |
| The picker's validation | inline in `ArtworkSelectionService`'s `Set*` methods: `:21-25`, `:57-59`, `:82-87` |
| Fetch an anime's picture set | `IPictureRefreshService.RefreshOneAsync`. A no-op once `PicturesSyncedAt` is set. |
| Know whether the list import finished | `IImportProgressTracker.Snapshot.Phase`. In memory only. |
| A background job with progress | the series bulk build: trigger, tracker with `MarkPending` and `Fail`, `BackgroundService`, `202` and a status endpoint |

**Constraints:**
- **The proxy.** nginx proxies `/api/` with the default `proxy_read_timeout` of 60 seconds. MyAnimeList requests are paced at about one per second.
- **The log's tie-break.** Surfaces read the log in `(Timestamp desc, Id desc)` order. `ActivityFeedComposer.FindCompletionScoreMerges` folds a completion into a following score only when the score comes first in that order. The completion must therefore hold the lower local number.
- **Tests.** They use the EF in-memory provider, with `InMemoryEventId.TransactionIgnoredWarning` suppressed wherever a service opens a transaction, as `ExportServiceTests` does. That provider cannot prove isolation or atomicity.
- **Builds.** The backend builds and tests only in the `mcr.microsoft.com/dotnet/sdk:10.0` image, from a copy under `/private/tmp`. The frontend builds with nvm's Node 22.

## Goals / Non-Goals

**Goals:**
- An import that applies without asking, by fixed rules, and reports what it did (specs: `device-transfer`).
- Refusals by name for:
  - a file this build cannot or should not read
  - a device still importing its list
  - an import already running
- Anime and series this device lacks are fetched or built first. Pictures that fail get one retry after their sets are fetched.
- One transaction for everything written from the file.
- Choices stored only through the picker's own checks, with the file's times.
- A report of exactly what the user asked for: ranking changes, first-time fetches, failures.
- Background progress on the Settings page, in the shared job shape.

**Non-Goals:**
- A review step, an undo, or an automatic export before importing.
- Propagating removals. An imported `Removed` record is history only.
- Persisting the report or the job across a restart.
- Any schema change.
- Tolerating a file from a newer format. It is refused, never read partly.

## Decisions

### D1. The request accepts the file; a background job imports it

**The request.** `TransferController` gains `POST api/transfer/import`.
- It reads the raw request body, not a model-bound one. The file is read with its own options (D3), for the same reason 03 D4 kept it off MVC's JSON settings.
- It calls `ITransferImportService.AcceptAsync(body, ct)`, which:
  1. runs the refusals (D2), turning any into a `400` or `409` carrying `{ "error": … }`, the shape `readErrorReason` already reads
  2. offers the parsed file to a single-slot `ITransferImportTrigger`
  3. calls `MarkPending` on the tracker
  4. returns `202` with the status, so the response already reads as running (the bulk-build pattern)

**The job.** `TransferImportBackgroundService` waits on the trigger and opens a scope. It runs `TransferImportRunner.RunAsync(file, progress, stoppingToken)`, then:
- on success, completes the tracker with the report
- on an exception, fails it with the reason

It runs on the host's stopping token, never the request's. Closing the tab does not stop an import.

**The status.** `GET api/transfer/import/status` returns:
- `phase`: `NotStarted`, `Running`, `Complete` or `Failed`
- `done` and `total`
- the file's `deviceName` and `exportedAt`
- the `report`
- the failure `error`

*Why a job:*
- An import may fetch unseen anime, build series and fetch picture sets, each paced at about one per second. That can exceed nginx's 60 seconds.
- A request cut off there would cancel the work, because `RequestAborted` fires.
- The Settings page already has a shape for work that takes minutes.

*Alternatives:*
- **Raise nginx's timeout and stay synchronous.** Rejected. A closed tab still cancels the import, the page could show no progress, and browser-side idle timeouts remain.
- **A persisted job table.** Rejected. Nothing but the report would be worth keeping across a restart. After a restart the same file can simply be imported again (D5), which re-reports whatever still fails.

### D2. The refusals, and their order

`AcceptAsync` checks, in this order, and stops at the first that applies:

| # | Condition | Status | Message (in substance) |
| --- | --- | --- | --- |
| 1 | Not JSON, not an object, or `formatVersion` not an integer ≥ 1 | 400 | Not a BetterMAL export file |
| 2 | `formatVersion` > 1 | 400 | Exported by a newer version (format *n*); this build reads format 1; update this device first |
| 3 | A required member missing, or of the wrong kind | 400 | The file is damaged; names the member's path |
| 4 | `device.id` equals this database's `DeviceIdentities.DeviceId` | 400 | Exported from this device; import it on the other one |
| 5 | Initial import phase not `Complete` | 409 | The list is still being imported (*x* / *n*), or hasn't finished since the app started |
| 6 | An import is already pending or running | 409 | An import is already running |

*Why this order:*
- **The version before the shape.** A format-2 file whose shape changed must be refused as *newer*, never as *damaged*.
- **File problems before state problems.** They are the more useful thing to learn first. Either way nothing is written.
- **Steps 5 and 6 at the moment of acceptance.** Checking them at the start of the job would add nothing, because a restart loses the queued file anyway.

**Why step 4:** 03 left it to this change whether to use the device id. Importing this device's own file would change nothing: every record exists, and every time is equal or older. A silent no-op would read as success, most likely after picking the wrong file.
- A database rebuilt from empty gets a new device id.
- So restoring from an old file of this device is still possible.

### D3. The import reads the file through its own types

`Services/Transfer/TransferFileReader.Read(bytes)` returns a `TransferFile`, or throws `TransferFileRefusedException(message)` for D2 steps 1–3.

**How it reads:**
1. Parse a `JsonDocument` and read `formatVersion` alone (steps 1 and 2).
2. Deserialize into import-side records: `TransferFile`, `TransferDevice`, `TransferRanking`, `TransferAnimePicture`, `TransferSeries`, `TransferSeriesChoice` and `TransferActivity`.

**The options** start from `ExportJson.Options` (Web defaults, camelCase) and add:
- `RespectRequiredConstructorParameters = true`. A missing member is refused, and `JsonException.Path` names it.
- `RespectNullableAnnotations = true`. A null where the format never writes one is refused.

Unknown members are ignored. A later build may add a member an older import can safely skip, without raising the version.

**Change types.** `TransferActivity.ChangeType` is a `string`. It is mapped to `ActivityChangeType` by exact name, and numeric strings are rejected. An unknown name stays with its record (D5), because `ActivityChangeType` is append-only and a new value does not change the file's shape.

**Times** are `DateTimeOffset`. A `…Z` string reads back to the exact stored tick, because 03 D5 writes full precision.

**Absent versus cleared series blocks.**
- A block property is nullable. An absent `title` reads as null and means "no block".
- `{ "value": null, "modifiedAt": … }` is a clear.
- A literal `"title": null` reads as absent. The export never writes one.

**The contract test.** A test runs `ExportService`'s real output through the reader. This pins the two halves of the contract to each other.

*Alternatives:*
- **Reuse 03's `Export*` records.** Rejected. One unknown change type would fail the whole file through the enum, and the records carry no notion of a required member.
- **Walk a `JsonNode` by hand.** Rejected. It is more code for the same checks, with worse error paths.

### D4. Prepare outside the transaction, then apply inside one

`TransferImportRunner` has two stages.

**Prepare** runs in the job's scope. Its MyAnimeList fetches commit their own cache rows, exactly as a page visit's would. Nothing from the file is written.

1. **Plan.** Read the present `EventId`s, the stored choice and ranking times, and which anime have rows. Then pick the candidates:
   - log records whose id is absent (first occurrence only)
   - anime-picture entries newer than what is stored
   - the ranking, if newer
   - every series entry, which must be resolved before it can be compared
2. **Fetch unseen anime** (D8).
3. **Resolve series**, building where needed (D8). The series blocks can now be compared.
4. **Pre-check picture candidates** with the picker's checks. Refresh the picture sets behind any refused one (D7).

**Apply** runs in a new scope, in one transaction (D11). It re-reads and re-decides every comparison, writes, then commits.

*Why re-decide in apply:* prepare can take minutes. A choice I make on this device meanwhile must beat an older file value, and the only way to know is to compare inside the transaction. The plan only decides what to fetch.

*Why not one transaction around everything:*
- The fetch and build services save through the scoped context, so they would join the transaction.
- Minutes of paced network calls would sit inside one open transaction.
- Their cache fills are legitimate on their own, and are what a visit would store anyway.

*Why the fetch has to come first:* `ActivityLog` and `TopAnimeSelection` both carry a cascading foreign key to `AnimeMetadata`. The rows have to exist before anything referencing them is written.

### D5. The merge rules

| Section | A candidate when | Written by | Time stored |
| --- | --- | --- | --- |
| Log record | its `id` is not stored | an insert, in file order (D10) | the record's own `timestamp` |
| Anime picture | file time > stored time, or none stored | `AdoptAnimePictureAsync` (D6) | the file's |
| Series title | per block: file time > stored time, or none stored | `AdoptSeriesTitleAsync` (D6) | the file's |
| Series picture | per block: file time > stored time, or none stored | `AdoptSeriesPictureAsync` (D6) | the file's |
| Ranking | file time is not null, and > stored time or none stored | `ReplaceAllAsync` (D9) | the file's |

**Ties and precision.**
- Newer means strictly newer. An equal time is the same choice seen again, which is why re-importing a file changes nothing.
- Times are compared as `DateTimeOffset` at full tick precision. 03 D5 keeps microseconds, so a round-tripped time compares equal rather than newer.

**Two file series resolving to one local series.** This happens when two series were merged here. They are applied in file order, each compared with whatever is stored by then, so the newest wins.

**The ranking report.** Added and removed are computed by membership: the difference between the order read inside the transaction and the list applied. A pure reorder lists nothing.

### D6. Adopting a choice is a write of `ArtworkSelectionService`

`IArtworkSelectionService` gains adopting writes and check-only methods:

| Method | Does |
| --- | --- |
| `AdoptAnimePictureAsync(int animeId, string? pictureUrl, DateTimeOffset modifiedAt)` | set or clear, storing `modifiedAt` |
| `AdoptSeriesTitleAsync(int seriesId, string? title, DateTimeOffset modifiedAt)` | set or clear, storing `modifiedAt` |
| `AdoptSeriesPictureAsync(int seriesId, string? pictureUrl, DateTimeOffset modifiedAt)` | set or clear, storing `modifiedAt` |
| `CheckAnimePictureAsync(int animeId, string pictureUrl)` | validates only, writes nothing |
| `CheckSeriesPictureAsync(int seriesId, string pictureUrl)` | validates only, writes nothing |

**Validation.** A non-null value runs exactly the checks the matching `Set*` runs. They live in private helpers extracted from today's `Set*` bodies, and both `Set*` and `Adopt*` call them:
- **Anime picture:** the anime is in my list, and the picture is in `AnimePicture.Options`.
- **Series title:** `SeriesTitleRule.IsAcceptable` against the main line's `OfferedTitles`. The title is stored as `SeriesTitleRule.Normalize` leaves it.
- **Series picture:** the picture is in `SeriesPicturePool.Build`, plus the current selection.

A failed check throws `ArtworkSelectionRejectedException` before anything is modified.

**Null clears** with no check, as the picker's `Reset*` does. For an anime, `ResolvePictureUrl()` runs either way.

**The comment about writers.** It currently names "these six methods" as the only writers of the choice times; it becomes nine.
- The service stays the only writer of those columns.
- An adoption is the only write that stores a time other than now. It is the choice-side counterpart of `ReplaceAllAsync`: adopting another device's choice is not making one.

*Alternatives:*
- **Write the columns from the runner.** Rejected. That is exactly how an import could store what the picker refuses.
- **Call `Set*`, then overwrite the time.** Rejected. It is two writes, the first stamping a time that should never have existed, and a clear would go through `Reset*` with the same problem.

### D7. The retry fetches the picture sets behind the option set, once

**The refresh option.** `IPictureRefreshService.RefreshOneAsync` gains `bool evenIfFetched = false`.
- With it set, eligibility drops `PicturesSyncedAt == null` and keeps "in my list". The spec says picture sets are fetched only for anime on my list.
- It keeps the same single-flight key, and still never throws.

**During prepare**, per candidate that sets a picture:
- The candidate goes through `Check*Async`.
- If refused, the runner refreshes with `evenIfFetched: true`:
  - an anime choice: that anime
  - a series choice: each my-list main-line member of the resolved series, in main-line order, with no budget, since the job is not a page visit

**During apply**, `Adopt*` checks again. That is the retry. A refusal there is reported. No choice is refreshed twice.

**Refused for another reason.** An anime not in my list, say, triggers a refresh that is a no-op (my-list-only). The second check refuses it again and it is reported.

**Why refetch a set already fetched:** a set fetched before MyAnimeList added the chosen picture fails exactly as a never-fetched one does. Retrying only never-fetched sets would leave it failing on every import.

**Why no retry for titles:** the retry was asked for pictures. A picture set is the one input that is missing by design until fetched. The titles `SeriesTitleRule` reads come with every anime's metadata.

*Alternative:* refresh every chosen anime up front. Rejected. It is hundreds of fetches for choices that would pass as they are.

### D8. Unseen anime are fetched; unbuilt series are built

**Unseen anime.** Every anime with no `AnimeMetadata` row that a candidate names is fetched. That covers:
- new log records
- anime-picture candidates
- the ranking's list, when it is a candidate
- every series entry's id

**How each is fetched:**
- `IMetadataRefreshService.RefreshOneAsync`, under `RefreshGate.LockAsync("anime:{id}")`.
- The row is re-checked after the gate is acquired, which is the detail page's own pattern. A visit racing the import cannot insert the row twice.
- The anime has no list entry here, so it is fetched without its picture set. That is consistent with the my-list-only rule.

**Outcomes:**
- **Success:** the anime is listed as fetched for the first time.
- **`AnimeMetadataNotFoundException`** (MyAnimeList answered 404): reported as "MyAnimeList has no anime with this id".
- **Any other exception:** reported as "could not be fetched from MyAnimeList".

On either failure, everything naming that anime is skipped and reported, and the rest carries on.

**Series.** For each file series id:
1. Call `ISeriesService.FindSeriesIdAsync(id)`.
2. If that returns null, call `ISeriesService.GetSeriesAsync(id)`, the visit's build, then `FindSeriesIdAsync` again.
3. Outcomes:
   - `SeriesNotFoundException`: reported as "not part of a series on this device"
   - any other exception: reported as "the series could not be built"
   - a partial build still resolves

*Why build rather than report:* a series not yet built here is the same kind of gap as an unseen anime. It is missing from this device's cache, not a disagreement between devices, and building it is what visiting its page would do.

*Why resolve through membership even when a series with that id exists:*
- The id is the root's MyAnimeList id.
- If the root moved on this device, the old root is still a member.
- Its primary membership names the series now holding the choices.

### D9. The ranking is applied whole or not at all

**The rule.** If every id in a newer list has a row once prepare has finished, the runner calls `ReplaceAllAsync(list, fileTime)`. If any id could not be fetched, the ranking is left alone, and the report names the anime that blocked it.

**The fallback.** `ReplaceAllAsync`'s own `UnknownAnimeIdsException` is thrown before any row is touched. It is caught and reported the same way, which covers the (unexpected) case of a row vanishing between the stages.

*Why:*
- `ReplaceAllAsync` already refuses such a list, leaving the order and its time untouched (data-persistence, "The stored ranking can be replaced exactly").
- Storing the list minus the missing anime, under the file's time, would record this device as holding that ranking.
- A later import of the same file would then find its time not newer, and never place the missing anime.
- Left alone instead, importing again once MyAnimeList answers applies the list exactly.

### D10. One log record, one save, in file order

**The rule.** Log records are inserted inside the transaction with one `SaveChanges` each, in file order.

*Why:*
- Local numbers must follow file order, so that a completion and its score keep folding into one row.
- EF Core does not promise that rows added together receive identity values in the order they were added.
- One insert per record makes the order structural.
- Around 1,000 inserts on a local Postgres, inside one transaction, take on the order of a second.

**Duplicates.**
- Stored `EventId`s are read once, inside the apply, into a set.
- Each inserted id joins it, so a duplicate later in the file is skipped too.
- The unique index is the backstop.

*Alternatives:*
- **Rely on EF's batch order.** Rejected. It is not promised, and the in-memory provider would never catch a regression.
- **Raw SQL.** Rejected. It bypasses the model for no gain.

### D11. The apply is a fresh scope and a `RepeatableRead` transaction

**A fresh scope.** Apply opens a new DI scope, so its `AnimeTrackerDbContext` tracks nothing from prepare. A tracking query returns an already-tracked instance *without* refreshing its values, so reusing prepare's context would read stale rows.

**The transaction.** It opens `BeginTransactionAsync(IsolationLevel.RepeatableRead)`, then:
- re-reads everything it compares
- writes through `ArtworkSelectionService`, `ReplaceAllAsync` and the log inserts, each of which calls `SaveChanges` inside the same transaction
- commits at the end

**Handling failures:**
- **A rejection thrown by `Adopt*`, or `UnknownAnimeIdsException`,** is thrown before anything is modified. It is caught, reported, and the apply carries on.
- **Any other exception** rolls the transaction back, and the job fails with "nothing from the file was applied".

**A concurrent save.** In Postgres, updating a row another transaction changed after this one's snapshot raises a serialization failure. A choice I save during the apply therefore makes the import fail whole, rather than letting an older file value overwrite it. The same holds for a ranking write, since a score save deletes and re-inserts `TopAnimeSelections`.

**Tests.** The in-memory tests suppress `TransactionIgnoredWarning`. Isolation and atomicity rest on Postgres semantics, and are checked once after deployment (Migration Plan).

*Alternative:* `Serializable`. Rejected. `RepeatableRead` already catches the one collision that matters, a write to a row this import writes, and nothing here needs predicate locking.

### D12. The list-import gate reads the in-memory tracker

**The rule.** An import is refused unless `IImportProgressTracker.Snapshot.Phase == Complete`.
- **`Running`:** the refusal gives its counts.
- **`NotStarted`:** the refusal says the list hasn't finished importing since the app started. That wording is true both for a device never connected and for one whose start-up import could not reach MyAnimeList.

*Why the in-memory state is enough:*
- On every start where a token exists, the initial import re-runs.
- With the list already stored, it skips every anime and reaches `Complete` within seconds.
- A persisted marker would add a migration and a new writer for a question the tracker already answers.

### D13. The report

`TransferImportReport` holds:

| Member | Contents |
| --- | --- |
| `rankingAdded` | anime `{ animeId, title, englishTitle }` |
| `rankingRemoved` | anime `{ animeId, title, englishTitle }` |
| `fetched` | anime `{ animeId, title, englishTitle }` |
| `failures` | `{ subject: "anime" \| "series", id, title, englishTitle, what, reason }` |

**Failures.**
- `what` is one of: chosen picture, series title, series picture, edit history (with its number of records), or ranking.
- There is one line per subject and `what`. An anime that could not be fetched gets one line for each thing that named it.
- A series line carries the file's series id, which is the id the file names.
- An unknown change type is reported under its anime, as edit history with the number of records affected.

**Titles** are resolved when the run ends, so a title known "by then" is used:
- **An anime:** its stored title and English title.
- **A series:** its displayed title through `SeriesIdentity.Resolve` when it resolved, otherwise its root anime's title.
- **Neither stored:** no title.

The frontend picks the English title first (`pickDisplayTitle`), as everywhere else.

**Lifetime.** The tracker keeps the report, together with the file's `device.name` and `exportedAt`, until the next import starts or the backend restarts.

### D14. Frontend

**`api/client.ts`:**
- `importData(file: File)` posts `await file.text()` as `application/json` through `fetchRaw`. A non-ok response throws `ApiError`, whose `reason` is the refusal's message. It returns the status.
- `getImportStatus()`.

**`api/types.ts`:** the status and report types.

**`SettingsPage.tsx`:** a second `SettingsAction` in Transfer, **Import from a file**, after the export.
- **Its hint** is the explanation the spec requires.
- **Choosing a file.** The button, **Choose file…**, opens a hidden `<input type="file" accept=".json,application/json">`.
- **Dropping a file.** A file dropped anywhere on the action is taken the same way. A modifier class highlights the action while a file is held over it.
- **While running:**
  - the button is disabled and reads **Importing…**
  - the status is polled once a second, like the job actions
  - `JobProgress` shows `done` and `total`, with the noun "fetches"
- **A refusal** appears as `settings-box__error`, carrying the reason.
- **When complete:**
  - a line naming the file's device and export time
  - the report's four lists: added to the ranking, removed from the ranking, fetched for the first time, could not be applied
  - or "Nothing to report."
- **When failed:** `JobProgress`'s failed state, plus the error.
- **On mount,** `getImportStatus()` joins `load()`, so a report from earlier in this backend's life still shows.

**`SettingsPage.css`:** the report list and the drop highlight.

## Risks / Trade-offs

- **[An import cannot be undone, and the ranking loses work]** → This is stated before pressing, with the habit that avoids it: export this device first. Before the first import on each device, the Migration Plan also takes a dump.
- **[A save during the apply fails the import]** → Nothing is applied, and importing again is safe. The window is the apply stage only, seconds long, not the fetching before it.
- **[A long first import on a device with few series built]** → It runs as a background job with progress. Each build uses the visit budget, and the first visit to that series would have built it anyway.
- **[An anime MyAnimeList has deleted blocks that device's ranking]** → The report names the anime. It cannot be fetched, so D9 leaves the ranking alone on every import of that device's files. MyAnimeList rarely deletes an anime. If it happens, dropping ids MyAnimeList answers 404 for is its own change.
- **[MyAnimeList unreachable at start keeps the gate shut]** → The refusal says the list hasn't finished importing since the app started. A restart, or re-authorizing, re-runs the list import.
- **[The report is lost on restart]** → Importing the same file again re-applies nothing that already applied, and re-reports whatever still fails.
- **[Imported removals remove nothing]** → By design, since removals do not propagate between devices (export-import README). The record reads as history.
- **[Past recaps move]** → By design. The recap reads every progress record (`Services/Recap/RecapService.cs:35`), and the other device's progress is real progress.
- **[The import-side and export-side types drift apart]** → The round-trip test (D3) fails the build if they do.
- **[`evenIfFetched` spends MyAnimeList requests]** → Only for choices that failed a check, and only once each. The shared pacer paces them.
- **[Test doubles]** → `SeriesControllerBulkBuildTests`' doubles of `IArtworkSelectionService` and `IPictureRefreshService` gain the new members.

## Migration Plan

No schema change and no migration. Only one device runs this app today; the
cross-device steps below apply once a second one exists and are not a
condition of shipping this change.

1. **Before deploying**, on this device:
   - take a dump and confirm `pg_restore --list` reads it
   - export, and keep the file
2. **Deploy:** `docker compose up -d --build backend frontend`.
3. **Once a second device is set up**, verify the cross-device merge:
   - import device A's file on device B. Check the report, that the
     `ActivityLogs` count is B's own plus A's records that B lacked, that one
     choice only A had is now B's carrying A's time, that the ranking went to
     the newer side, and that Latest updates still shows merged
     "Completed — Score" rows for imported completions
   - import the same file again; confirm "Nothing to report", with row
     counts and times unchanged
   - export from B and import on A; both devices then hold the same log, the
     same newest choices and the same ranking
   - save a picture choice on B while an import of an older choice for that
     anime is applying, and confirm the choice survives and the import
     either loses cleanly or fails whole — this checks the Postgres
     transaction behaviour the in-memory tests cannot

**Rollback:** check out the previous commit and rebuild. There is no schema to reverse. Undoing an import's data means restoring the dump from step 1.

## Open Questions

None blocking. For a later change, if it ever occurs: whether an anime MyAnimeList answers 404 for should be dropped from an imported ranking, instead of blocking it (see Risks).
