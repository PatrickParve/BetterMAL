## Context

This is the third of four changes (01–04) that move my data between my two devices.
- 01 (`log-only-my-own-edits`, archived) made each device's log hold only what was done on that device.
- 02 (`make-ranking-and-log-portable`, archived) gave the ranking one last-modified time on a singleton `RankingState` row, and every log row an `EventId` GUID.

This change produces the file. 04 reads it. The file is therefore a contract between two builds that may not be deployed at the same time.

**Where each section lives today:**
- **Ranking:**
  - `TopAnimeSelections` holds `(AnimeId, Position)`. `TopAnimeSelectionRepository.GetOrderedAnimeIdsAsync` reads it ordered on `(Position, AnimeId)`, which is the order the app reads.
  - The time sits on `RankingStates`. Nothing reads it yet.
  - The one writer, `WriteOrderAsync`, rewrites the order and the time in one `SaveChanges`.
- **Anime pictures:** `AnimeMetadata.SelectedPictureUrl` and `SelectedPictureModifiedAt`. Only `ArtworkSelectionService` writes them.
- **Series choices:** `Series.SelectedTitle`/`SelectedTitleModifiedAt` and `SelectedPictureUrl`/`SelectedPictureModifiedAt`. `Series.Id` is the root anime's MAL id.
- **Activity log:**
  - `ActivityLogs` has the local `long Id` and the travelling `Guid EventId`. `ChangeType` is stored as a string.
  - `GetRecentAsync` and `GetAllAsync` order on `(Timestamp desc, Id desc)`, and both `Include` the anime.
- **The device:** nothing identifies it today.
  - Inside Docker, the backend's hostname is its container id (`886ab0e45e3d` at proposal time), and that changes on every rebuild.
  - The Mac's own name ("Patrick's MacBook Air") is not exported to the shell, so `docker compose` could not interpolate it.

**Current data on this device** (verified 2026-09-10): 455 ranking rows, 197 anime choices (1 of them a clear), 114 series with a choice (80 of them with only one block), and 913 log rows. Stored times are microsecond-precise.

**Constraints:**
- The tests use the EF in-memory provider. It has no SQL, no isolation levels and no real transactions. Starting a transaction throws unless `InMemoryEventId.TransactionIgnoredWarning` is suppressed, as `SeriesGraphBuilderReRootTests` already does.
- The backend builds, tests and runs `dotnet ef` only in the `mcr.microsoft.com/dotnet/sdk:10.0` image, from a copy under `/private/tmp`. The frontend builds with nvm's Node 22.
- The backend runs `db.Database.Migrate()` at startup, followed by `IStartupPendingSyncHold.ApplyAsync()` in the same startup scope.
- Each device runs its own `docker compose` stack against its own Postgres volume.

## Goals / Non-Goals

**Goals:**
- One file holding exactly the four app-only sections and a header, with the shape in the brief.
- A device identity that needs nothing configured: an id the app generates for itself, and a readable name taken from the browser that asked.
- Time-based selection for choices, with "absent" and "cleared" kept distinct in the file.
- Activity rows in `(Timestamp, Id)` ascending order, carrying the GUID and never the local number.
- A consistent snapshot, and an export that writes nothing.
- A Transfer group on the Settings page, holding the export action.

**Non-Goals:**
- Import, merge rules, or validating a file. That is 04.
- Any setting, `.env` value or UI for naming the device.
- Deciding what 04 does with the device id.
- Compression, chunking, or streaming the response. The file is a few hundred KB at most.
- Refusing to export during the initial list import. An export at that point is simply smaller, and 04 is the side that refuses.

## Decisions

### D1. One read-only service behind one `GET`

The pieces, all new:
- `Services/Transfer/ExportService`, behind `IExportService`, builds the file.
- `Controllers/TransferController` exposes it as `GET /api/transfer/export`.
- The controller passes `Request.Headers.UserAgent` to the service (D6), and returns `File(bytes, "application/json", fileName)`. ASP.NET writes `Content-Disposition: attachment; filename=…` from that.

`GET` fits because the request has no side effect and can be repeated freely.

The route sits under `api/transfer/`, not `api/export`, so that 04's import can land beside it (`POST /api/transfer/import`) under the same name as the Settings group.

*Alternatives:*
- `POST`. Rejected: nothing is created or changed.
- A route per section. Rejected: the snapshot (D2) and the single file both need one request.

### D2. The reads share one snapshot: a `RepeatableRead` transaction

The export makes six reads:
1. the device id
2. the ranking order
3. the ranking time
4. the picture choices
5. the series choices
6. the log

Read separately, a score save could land between reads 2 and 3. The file would then carry the old order under the new time, and 04's "newer wins" would install a stale order stamped as current, on the other device.

`ExportService` therefore opens `db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead)` before the first read and disposes it after the last. In Postgres, every statement in a `REPEATABLE READ` transaction sees the snapshot taken at its first statement. That is exactly the "wholly or not at all" rule, for every section at once. The transaction writes nothing, so it is never committed; disposing it rolls back nothing.

The repositories share the scoped `AnimeTrackerDbContext`, so their reads run inside the same transaction without being passed anything.

The in-memory tests suppress `TransactionIgnoredWarning`, following `SeriesGraphBuilderReRootTests`. They cannot prove isolation; that rests on Postgres semantics.

*Alternatives:*
- Read the order and the time in one query. Rejected: it closes only the ranking race. A choice written between the picture and series reads would still be half in the file.
- `Serializable`. Rejected: its extra guarantees protect writers, and the export writes nothing.

### D3. Where each read lives

**Ranking order:** the existing `GetOrderedAnimeIdsAsync`. The `(Position, AnimeId)` rule is pinned by `AnimeRankingKeyOrderingParityTests`, and a second copy of it would be a second thing to keep in step.

**Ranking time:** a new `ITopAnimeSelectionRepository.GetModifiedAtAsync()`. It returns `RankingState.ModifiedAt`, or null when there is no row.
- The repository owns the only writer of that value, and 02 left the reader to this change.
- 04 needs the same read to decide which ranking is newer.
- The 12 hand-written test doubles each gain a stub that throws `NotImplementedException`, as in 02 D4.

**Activity log:** a new `IActivityLogRepository.GetAllOldestFirstAsync()`.
- It orders `AsNoTracking()` on `Timestamp` ascending, then `Id` ascending, with no `Include`.
- It sits next to the two descending reads, so the `(Timestamp, Id)` tie-break rule, and the comment explaining it, live in one file.
- The 13 test doubles each gain a throwing stub.

**Device id, picture choices and series choices:** `AsNoTracking()` reads on the `DbContext` inside `ExportService`.
- `DeviceIdentities` is read with `SingleAsync`, since there is exactly one row.
- `AnimeMetadata` rows are filtered on `SelectedPictureModifiedAt != null` and ordered by `Id`.
- `Series` rows are filtered on either time being non-null and ordered by `Id`.
- No repository exposes these columns. The choice reads are projections, so they read only the columns the file needs, not whole metadata rows with their picture sets.

*Alternative:* reverse `GetAllAsync` in memory. Rejected: it `Include`s an `AnimeMetadata` row for each of 913 log rows, only to throw them away.

### D4. The file's shape is defined by its own record types and its own serializer options

`Services/Transfer/ExportFile.cs` holds records that mirror the brief's JSON:
- `ExportFile`
- `ExportDevice`, holding `Guid Id` and `string? Name`
- `ExportRanking`
- `ExportAnimePicture`
- `ExportSeries`
- `ExportSeriesChoice`
- `ExportActivity`

A static `ExportJson.Options` defines how they are written:
- `JsonSerializerDefaults.Web`, which gives camelCase names.
- `JsonStringEnumConverter` with **no naming policy**. `ChangeType` is then written as `EpisodeIncremented`, the same name `HasConversion<string>()` stores.
- `WriteIndented = true`. The file is meant to be opened and checked by eye, and at these row counts the size is irrelevant.

The service serializes to UTF-8 bytes with these options. It does not hand an object to MVC. The file is a contract with 04 and with future builds, and should not change shape when someone adjusts the API's JSON settings.

**Absent versus null** (series blocks):
- `ExportSeries.Title` and `.Picture` are nullable `ExportSeriesChoice` properties marked `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]`. A block that was never set or cleared is `null` in C#, so its key is omitted from the file.
- `ExportSeriesChoice.Value` carries no such attribute, so a cleared choice is always written as `"value": null`.
- Those two properties are the only ones that omit a null. Everything else writes its nulls: `device.name`, `ranking.modifiedAt`, `selectedPictureUrl`, `changeDetail` and `previousEpisodesWatched`.

**Activity identity:** `ExportActivity.Id` is a `Guid` filled from `EventId`. `ActivityLog.Id` is never read into the file's types at all, so the local number cannot leak.

*Alternative:* return the records through `Ok(...)` with MVC's options. Rejected: an attachment still needs a file result, and the file format would be coupled to the API's settings.

### D5. Times are written from `DateTimeOffset.UtcDateTime`

The record types hold `DateTime` values set from `.UtcDateTime`.

System.Text.Json writes a `DateTime` of kind `Utc` as ISO 8601 with a `Z`. It keeps up to seven fractional digits, dropping trailing zeros. A stored `2026-09-09 21:26:43.973829+00` therefore comes out as `"2026-09-09T21:26:43.973829Z"`: the stored value exactly, in the brief's form.

Precision matters because 04 compares file times with stored times. Truncating would amount to restamping at export, which the artwork-selection spec forbids ("Nothing that later reads, copies, exports, or rebuilds a choice SHALL restamp it"). A truncated time would also make a device's own choice, re-imported, read as slightly older than itself.

*Alternative:* write the `DateTimeOffset` directly. Rejected: it comes out as `+00:00`. Both forms parse, but the file should have one canonical form, and the brief shows `Z`.

### D6. The device is identified by a generated id, and described by the browser that asked

Nothing about the device is configured. `device` in the header holds:
- `id`: a GUID the app generated for this database.
- `name`: `"OS · Browser"`, derived from the export request's `User-Agent`, or null.

**The id is stored on a new singleton row.**
- `Models/DeviceIdentity.cs` has an `int Id` and a `Guid DeviceId`, the same singleton shape as `RankingState`, and gets a `DeviceIdentities` `DbSet`.
- The migration `AddDeviceIdentity` only creates the table. It seeds nothing.

**It is created at startup, by application code.**
- `Services/Transfer/DeviceIdentityInitializer.EnsureAsync()`, behind `IDeviceIdentityInitializer`, runs in `Program.cs`'s startup scope right after `Migrate()`, beside `IStartupPendingSyncHold.ApplyAsync()`.
- If no row exists, it adds one with `Guid.NewGuid()`. If one exists, it does nothing.
- Nothing else writes the row.

It follows that:
- the id exists without anything being set, from the first start of the upgraded backend on each database
- it lives in the Postgres volume, so rebuilds and restarts keep it
- the export only reads it, so "producing an export writes nothing" still holds
- there is one generator, in application code, the same reasoning as 02 D5

*Why not seed it in the migration?* The startup check also covers a database that lacks the row for any other reason, such as a dump taken before this change and restored, and it keeps the migration pure scaffold, with no hand-written SQL to rehearse.

*Other sources, rejected:*
- **The container's hostname.** It is the container id, and it changes on every rebuild.
- **The host machine's name.** A container cannot read it, and passing it through `docker compose` needs a value set by hand, which is what this decision removes.
- **Postgres's `pg_control_system().system_identifier`.** It is automatic and needs no migration, but it identifies the Postgres cluster rather than the data. Restoring a dump into a fresh volume would change it, and the in-memory tests cannot see it.

**The name: `DeviceName.Describe(string? userAgent)`,** a pure function returning `"OS · Browser"`, one part alone, or null.
- **Operating system**, first match wins:
  - `iPhone` → iOS
  - `iPad` → iPadOS
  - `Android` → Android
  - `CrOS` → ChromeOS
  - `Windows` → Windows
  - `Macintosh` or `Mac OS X` → macOS
  - `Linux` → Linux
- **Browser**, checked in this order, because each later token also appears in the user agents of the earlier browsers:
  - `Edg` → Edge
  - `OPR/` → Opera
  - `Firefox/` or `FxiOS/` → Firefox
  - `Chrome/` or `CriOS/` → Chrome
  - `Safari/` → Safari
- No versions are included. Safari freezes the macOS version in its user agent anyway, and a version would make two exports from the same browser differ after an update.

The name describes the browser and never identifies the device; the id does that. Two known inaccuracies are accepted:
- iPad Safari requests desktop sites by default and so is described as `"macOS · Safari"`.
- Two Macs using the same browser get the same name.

**File name:** `bettermal-{first 8 hex digits of the id}-{yyyyMMdd-HHmmss}.json`, using `exportedAt` in UTC.

*Alternative for the name:* have the frontend send `navigator.userAgentData` as a query parameter. Rejected: Safari and Firefox do not implement `userAgentData`, so the frontend would parse the same user-agent string, only somewhere it cannot be unit tested.

### D7. Frontend: fetch the file as a blob, save it, report the result

**`api/client.ts`:** gains `exportData(): Promise<{ blob: Blob; fileName: string }>`.
- It goes through `fetchRaw`, so reachability reporting behaves as for every other call.
- A non-ok response throws `ApiError`.
- The file name is read from `Content-Disposition`, falling back to `bettermal-export.json`.
- The browser sends its own `User-Agent`, so the frontend passes nothing for the device name.

`fetchRaw` merges identical GETs that are in flight at the same time. That is harmless here, because the button is disabled while an export is running.

**`SettingsPage.tsx`:** gains `<SettingsGroup title="Transfer" …>` between Data tools and Account.
- The group's hint is "Move what only this app holds between your devices, as a file."
- It holds one `SettingsAction`, titled **Export to a file**. Its hint is the explanation the spec requires: what the file holds, what it leaves out, that nothing here changes and nothing is sent, and that moving the file is up to me.
- The button reads **Export**, and **Exporting…** while it runs.

To save the file, the page:
1. calls `URL.createObjectURL(blob)`
2. clicks a temporary `<a download={fileName}>`
3. revokes the URL

On success, a hint line names the file. On failure, the existing `settings-box__error` style says the export could not be produced. The page's existing classes cover all of this, so no new CSS is expected.

*Alternative:* a plain `<a href="/api/transfer/export">` link, like the re-authorize link. Rejected: on failure the browser would show a bare error page or save an error body, instead of the in-place message the spec requires.

### D8. Picture choices are filtered on the time alone, with no list join

The brief says the time filter "is also the my-list filter by itself" because `ArtworkSelectionService.cs:22` refuses a choice for an anime not in my list. That holds for **setting** a choice. It does not hold for clearing one, or for what happens afterwards:
- `ResetAnimePictureAsync` has no list check.
- Removing an anime from my list leaves its choice and time in place.

Today no stamped choice sits on an anime outside my list, but nothing guarantees it.

The export still filters on the time alone:
- Removals do not propagate between devices (see the export-import README), so the anime may well still be in my list on the other device. There, a newer choice is exactly what should arrive.
- 04 sends every choice through the picker's own validation, list check included. A choice that doesn't belong will be refused there, by name.

### D9. Every section has a fixed order

| Section | Order |
| --- | --- |
| Ranking | stored order |
| Pictures | anime id |
| Series | series id |
| Activity | `(Timestamp, Id)` ascending |

Nothing iterates a dictionary or a hash set on the way to the file. Two exports of unchanged data from the same browser are therefore identical apart from `exportedAt`, which makes two files easy to compare, and makes the snapshot property testable.

### D10. The group is named Transfer, and sits after Data tools

- **Name:** "Transfer" names what the group is for, moving data between devices, in one word like its neighbours. It avoids "Import", which the Settings page and the specs already use for the initial MyAnimeList list import (`initial-import`).
- **Placement:** it goes after Data tools because the ordering rule runs from cheapest and most reversible to most expensive, and 04's import cannot be undone.
- **Not in Data tools:** that group is defined as the long-running corrective and backfill jobs, and the export is neither.

## Risks / Trade-offs

- **[An export from a stale device carries stale choices]** → The export decides nothing. Every choice and the ranking travel with the time they were made, and 04 lets the newer one win, so a stale file loses on every point where the other device has moved on. Nothing is exported unless I press the button.
- **[Copying one device's database to the other gives both the same device id]** → The file is the route between devices, not a dump. If a dump is ever restored across devices, deleting the `DeviceIdentities` row and restarting creates a fresh id. Nothing but the export header reads it.
- **[`device.name` can mislead]** → An iPad reads as macOS, and two Macs on the same browser read alike. It is labelled descriptive in the spec. The id is what identifies the device, and the file name carries the id.
- **[Reversing `AddDeviceIdentity` loses the id]** → Once the table is re-created, the next start generates a new id, and files exported earlier name an id this device no longer holds. Until 04 gives the id a use, that costs nothing.
- **[The snapshot guarantee cannot be tested in memory]** → It rests on Postgres `REPEATABLE READ` semantics, which are well defined. After deployment, one export against the live database confirms the transaction opens and completes (task 6.5).
- **[Rolling back 02 after exporting orphans GUIDs]** → 02's `Down` discards every `EventId`, and once a file holding them exists, re-importing it would duplicate the log. After 03 ships, roll 02 back only by restoring a dump.
- **[25 test doubles gain throwing stubs]** → This is mechanical and mirrors 02 D4. None of those tests exercise the new reads.
- **[The file holds personal history]** → It holds no credentials, since `OAuthTokens` is excluded by the spec. Where I keep it is my own choice.

## Migration Plan

1. Take a dump before rebuilding, as for every migration, and confirm `pg_restore --list` reads it.
2. Run `docker compose up -d --build backend frontend`. On start:
   1. `AddDeviceIdentity` creates the empty table.
   2. The initializer adds the one row.
3. Confirm:
   - `DeviceIdentities` holds exactly one row
   - the row counts of `TopAnimeSelections`, `AnimeMetadata`, `Series` and `ActivityLogs` are unchanged
4. Export once, and check the file against the database:
   - the counts of ranking ids, picture entries, series entries and activity records
   - one cleared picture
   - one series with only a title block
   - the order of one shared timestamp
   - the device id
5. Restart the backend, export again, and confirm the device id is unchanged.
6. Repeat on the second device, and confirm its id differs.

**Rollback:** check out the previous commit and rebuild. The export stores nothing. `AddDeviceIdentity`'s `Down` drops the table, and with it the id (see Risks).

## Open Questions

- **For 04:**
  - The second device's counts are unknown. The brief's "546+" log rows may be that device's figure.
  - Whether the import uses `device.id`, for example to recognise and skip a file this same device exported, is 04's decision. This change only makes the id available.
