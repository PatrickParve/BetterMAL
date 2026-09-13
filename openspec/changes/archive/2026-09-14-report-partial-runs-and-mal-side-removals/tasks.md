Paths without a prefix are under `backend/AnimeTracker.Api/`. Test paths are under `backend/AnimeTracker.Api.Tests/`.

## 1. Partial runs of the airing refresh and build all series end as failed (B2; design D1, D2)

- [x] 1.1 In `Services/Airing/IEpisodeScheduleRefreshService.cs`:
  - add `public record RefreshManyResult(int NoData, int Failed);`
  - change `RefreshManyAsync` to return `Task<RefreshManyResult>`
  - update its doc comment: no data isn't a failure, and a thrown refresh is
- [x] 1.2 In `Services/Airing/EpisodeScheduleRefreshService.cs` `RefreshManyAsync`, replace `zeroRows` with two counters: a `0` return counts toward `noData`, the per-item catch counts toward `failed`. Return `new RefreshManyResult(noData, failed)`. Leave `EpisodeScheduleRefreshBackgroundService` (`:88`, `:103`) discarding the result, and confirm it still compiles.
- [x] 1.3 In `Services/Airing/AiringFullRefreshBackgroundService.cs`, capture the result:
  - `Failed > 0`: `progress.Fail($"{result.Failed} of {targets.Count} anime couldn't be refreshed from AniList.")`
  - otherwise: `progress.Complete()`
- [x] 1.4 In `Services/Series/SeriesBulkBuildBackgroundService.cs` `RunAsync`:
  - add a `failed` counter, incremented only in the `catch (Exception ex) when (ex is not OperationCanceledException)` branch, not for `SeriesNotFoundException`
  - after the loop: `failed > 0` → `progress.Fail($"{failed} of {targets.Count} series couldn't be built.")`, otherwise `progress.Complete()`
- [x] 1.5 Tests:
  - `Services/Airing/EpisodeScheduleRefreshServiceTests.cs`: add a `RefreshManyAsync` test where one anime returns no rows and one throws, asserting `NoData == 1`, `Failed == 1` and that progress reports every anime.
  - `Services/Airing/AiringFullRefreshBackgroundServiceTests.cs`:
    - update `ThrowingEpisodeScheduleRefreshService` to the new signature
    - add a fake that returns a result: `Failed = 2` of 5 targets gives `Failed` with the "2 of 5" reason; `NoData = 3, Failed = 0` gives `Complete`
  - `Services/Series/SeriesBulkBuildBackgroundServiceTests.cs`:
    - change `AFailingTargetIsLoggedAndTheRunContinues` to assert `JobPhase.Failed`, `Done == 3`, and an error of `"1 of 3 series couldn't be built."`
    - keep `ASeriesNotFoundTargetIsANormalOutcomeNotAnError` asserting `Complete`

## 2. One list of known MAL statuses (R2; design D3)

- [x] 2.1 In `Services/Mal/MalMappingExtensions.cs`:
  - add `TryToWatchStatus(this string malStatus, out WatchStatus status)`, holding the only switch over the five strings
  - re-express `ToWatchStatus` through it, keeping the `ArgumentOutOfRangeException`
  - add `HasRecognizedStatus(this MalListStatus? status)`: true when `status?.Status` is null or `TryToWatchStatus` succeeds
  - doc-comment that a new MAL status is added in `TryToWatchStatus` alone
- [x] 2.2 In `Services/Jobs/JobFailure.cs`, add `UnrecognizedStatuses(int leftOut, int? total = null)`, returning `"Left out {n} anime whose MyAnimeList list status this app doesn't recognize — the backend log names each one."`, or `"Left out {n} of {total} anime whose …"` when a total is given.
- [x] 2.3 Tests in `Services/Mal/MalMappingExtensionsTests.cs`:
  - `TryToWatchStatus` maps the five known strings and returns false for an unknown one
  - `ToWatchStatus` still throws on an unknown string
  - `HasRecognizedStatus` is true for null status, a null `Status` and each known string, and false for an unknown string

## 3. Reconciliation: unrecognized statuses and removals on MAL (R2, PF3; design D4, D5)

- [x] 3.1 In `Models/PendingReconciliationDiff.cs`:
  - add `RemovedOnMal` to `ReconciliationDiffChangeType`
  - extend `PendingReconciliationDiffEntry`'s doc comment: for `RemovedOnMal`, the values are the local entry's, and applying it deletes that entry
  - confirm that no migration is generated (design D10)
- [x] 3.2 In `Services/Sync/IReconciliationService.cs`:
  - add `RemovedOnMal` and `SkippedUnrecognized` to `ReconciliationResult`
  - update the summary comments, including that `AcceptPendingDiffAsync` deletes entries for removals
- [x] 3.3 In `Services/Sync/ReconciliationService.cs` `RunLockedAsync`, change the forward walk:
  - capture `readStartedAt` immediately before `GetFullUserAnimeListAsync`
  - add a `seenAnimeIds` set, filled as the first statement of each loop iteration
  - after the pending-removal skip and before the lean-metadata add, add the `HasRecognizedStatus` check. On failure, log a warning with the anime id and raw status, increment `skippedUnrecognized` and `continue`
- [x] 3.4 In the same method, after the forward walk, add a `RemovedOnMal` diff entry for each local entry that meets all of these:
  - not in `seenAnimeIds`
  - not `PendingSync`
  - not in `pendingDeletionAnimeIds`
  - not `LastSyncedAt >= readStartedAt`

  Each entry carries the local entry's values (reuse `ToDiffEntry` with the local entry and `local.Status`). Count them, add both new counts to the completion log line and to the returned result, and add a short comment on why the timestamp guard exists.
- [x] 3.5 In `AcceptPendingDiffAsync`, branch on `entry.ChangeType == RemovedOnMal`:
  - no local entry: skip
  - `PendingSync`: skip
  - `LastSyncedAt > diff.ComputedAt`: skip
  - otherwise: `db.UserAnimeEntries.Remove(local)`, with no `PendingEntryDeletion`, push or `ActivityLog`

  Leave the `Added`/`Updated` body unchanged.
- [x] 3.6 In `Controllers/SyncController.cs` `Reconcile`, capture the run's result. When `result.SkippedUnrecognized > 0`, call `reconcileProgress.Fail(JobFailure.UnrecognizedStatuses(result.SkippedUnrecognized))`, following the sync-now pattern.
- [x] 3.7 In `Services/Sync/ReconciliationBackgroundService.cs`, capture the result and record `failed: result.SkippedUnrecognized > 0`, with the `JobFailure.UnrecognizedStatuses(...)` reason when it did.
- [x] 3.8 Update the fakes that build `ReconciliationResult` to the new arity: `Controllers/SyncControllerTests.cs` and `Services/Sync/ReconciliationBackgroundServiceTests.cs`.
- [x] 3.9 Add `Services/Sync/ReconciliationServiceRemovalTests.cs`, with an in-memory DB and a fake MAL client returning fixed edges, like `ReconciliationServiceRewatchingTests`. Cover computing:
  - a local entry MAL doesn't list gets a `RemovedOnMal` entry carrying its local values, and the result counts it
  - no removal for a `PendingSync` entry
  - no removal for an anime in `PendingEntryDeletions` (seed a local entry too, so the guard itself is exercised)
  - no removal for an anime MAL lists with an unrecognized status
  - no removal for an entry whose `LastSyncedAt` is later than the read start (have the fake client stamp the entry's `LastSyncedAt` while "reading", or seed a future timestamp)
- [x] 3.10 In the same file, cover accepting:
  - a removal deletes the entry, and leaves no `PendingEntryDeletions` row and no `ActivityLogs` row
  - a removal is skipped when the entry has since become `PendingSync`
  - a removal is skipped when the entry's `LastSyncedAt` is after `ComputedAt`
  - a removal for an entry already gone is a no-op
  - a mixed diff still applies its `Added`/`Updated` entries
  - cancelling a diff with a removal leaves the entry
- [x] 3.11 Add `Services/Sync/ReconciliationServiceUnrecognizedStatusTests.cs`. One unknown-status edge among others:
  - the run completes and stores the other anime's diff
  - no `AnimeMetadata` row is added for a new anime with the unknown status
  - no diff entry mentions it
  - `SkippedUnrecognized == 1`
- [x] 3.12 Add runner and controller tests:
  - `Services/Sync/ReconciliationBackgroundServiceTests.cs`: a stub result with `SkippedUnrecognized = 2` records `LastRunFailed == true` and the `UnrecognizedStatuses(2)` reason
  - `Controllers/SyncControllerTests.cs`: a reconciliation stub returning `SkippedUnrecognized = 1` leaves `ReconcileProgress` `Failed` with that reason once the run ends, and one returning 0 leaves it `Complete`

## 4. Import: skip unrecognized statuses (R2; design D8)

- [x] 4.1 In `Services/Import/InitialImportService.cs` `RunAsync`, at the top of the work loop and before the `existingAnimeIds` branch, check `edge.ListStatus.HasRecognizedStatus()`. On failure: log a warning naming the anime id, title and raw status, `skipped++`, `continue`.
- [x] 4.2 End the run:
  - `failed == 0 && skipped == 0`: `Complete()`, with the log line as today
  - otherwise: `Fail(...)` with the sentences that apply, joined by a space: `"{failed} of {total} anime couldn't be fetched."` and `JobFailure.UnrecognizedStatuses(skipped, total)`

  Extend the ended-with-failures log line with the skipped count. Update the class doc comment.
- [x] 4.3 Tests in `Services/Import/InitialImportServiceWorkTests.cs`:
  - an unknown-status anime with no cached metadata is not fetched (the fake client records no details call for it) and gets no entry
  - an unknown-status anime with cached metadata gets no entry
  - either case ends `Failed` with the "Left out 1 of N" reason, `Done` excludes it, and the gate went through
  - one fetch failure plus one skip gives both sentences
  - add a note, or a test in `InitialImportBackgroundServiceTests.cs` if cheap, that a skip-only failure sets `LastReadFailure`, so the existing retry sequence applies

## 5. Held changes: an unrecognized status reads as unavailable (R2; design D7)

- [x] 5.1 In `Services/Sync/HeldChangeService.cs`, add the private `ReadRemoteStatusAsync(int animeId, CancellationToken ct)`. It calls `malClient.GetMyListStatusAsync` and throws `InvalidDataException` naming the raw status and anime id when `status is not null && !status.HasRecognizedStatus()`. Use it in place of the client call inside the existing `try` blocks of `TryGetRemoteStatusAsync`, `DeclineEntryAsync` and `DeclineRemovalAsync`, and update the D8 comment on `TryGetRemoteStatusAsync` to say an unrecognized status counts as unreadable.
- [x] 5.2 Add `Services/Sync/HeldChangeServiceUnrecognizedStatusTests.cs`, using `FakeHeldChangeMalClient.SetStatus` with a `MalListStatus { Status = "rewatching_v2" }`:
  - `GetHeldAsync` returns that held entry with `RemoteUnavailable == true` and `RemoteValues == null`, does not clear it, and still returns the other held items
  - the same holds for a held removal
  - `DeclineAsync` returns `Failed` with "Could not read MyAnimeList's current value." and leaves the entry, or removal, exactly as it was
  - `DeclineAllAsync` counts it in `StillHeld` and declines the rest
  - `AcceptAsync` on it still returns `Applied`

## 6. Frontend: the removal row (design D9)

- [x] 6.1 In `frontend/src/api/types.ts`, change `ReconciliationDiffChangeType` to `'Added' | 'Updated' | 'RemovedOnMal'`.
- [x] 6.2 In `frontend/src/pages/SettingsPage.tsx`:
  - replace the `entry.changeType === 'Added' ? 'New entry' : 'Updated'` ternary with a typed label lookup (`Record<ReconciliationDiffChangeType, string>`)
  - render a removal row as `Removed on MyAnimeList — here: {status}, {n} ep[, score s]`
  - keep `Added`/`Updated` rows reading exactly as today
- [x] 6.3 Build with `PATH="$HOME/.nvm/versions/node/v22.23.1/bin:$PATH" npm run build` from `frontend/`. `tsc` must pass.

## 7. Build, test, and check by hand

- [x] 7.1 Copy `backend/` to `/private/tmp/bm-build/` (excluding `bin/` and `obj/`), then run `dotnet build` and `dotnet test` in `mcr.microsoft.com/dotnet/sdk:10.0`. Everything passes, including the existing reconciliation, held-change, import, airing and bulk-build suites.
- [x] 7.2 Confirm `dotnet ef migrations has-pending-model-changes`, or an equivalent check, reports no model change (design D10).

## 8. Docs

- [x] 8.1 In `SETTINGS.md`, "Run full reconciliation":
  - the diff lists entries that would be added, updated **or removed**
  - add a note that an anime deleted on MAL's site is offered for removal here, and accepting removes it locally without sending anything to MAL
  - add a note that an anime whose MAL status the app doesn't recognize is left out, and the run (or the weekly check) reports it as failed
- [x] 8.2 In `CODE_GUIDE.md`:
  - reconciliation bullet: `RemovedOnMal` detection with its guards, and accept deleting without a push
  - `ReconciliationBackgroundService` bullet: a run that skipped statuses records failed
  - `InitialImportService` bullet: the skipped-status sentence and that it retries like a fetch failure
  - add a one-line note that `MalMappingExtensions.TryToWatchStatus` is the single place to add a new MAL status
- [x] 8.3 In `docs/ISSUE_TRIAGE.md` (local, gitignored):
  - move B2 and PF3 to "Fixed", naming this change
  - resolve R2: remove it from Open items, noting it as fixed (skipped and reported, never mapped), and drop its "or map it to Plan to watch" alternative
  - update the Summary counts, Phase 1 of the Fix order, and the appendix rows for ISSUES #8, #13 and #16
