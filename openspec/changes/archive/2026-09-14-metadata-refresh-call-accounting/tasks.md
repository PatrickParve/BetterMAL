Paths without a prefix are under `backend/AnimeTracker.Api/`. Test paths are under `backend/AnimeTracker.Api.Tests/`.

## 1. Failed-attempt column and the due check (design D4, D5)

- [x] 1.1 In `Models/AnimeMetadata.cs`, add `public DateTimeOffset? LastRefreshFailedAt { get; set; }` next to `LastSyncedAt`. Its comment should say:
  - the scheduled refresh job sets it when MAL answers 404
  - `ApplyTo` clears it
  - only `RefreshTiers.IsDue` and the job's ordering read it
  - it is kept apart from `LastSyncedAt` because that one means "has had a full fetch"
- [x] 1.2 In `Services/Mal/MalMappingExtensions.cs` `ApplyTo(MalAnimeNode, AnimeMetadata, …)`, set `target.LastRefreshFailedAt = null` beside `target.LastSyncedAt = now`.
- [x] 1.3 In `Services/Metadata/RefreshTiers.cs` `IsDue`:
  - change the never-fetched short-circuit to `a.LastSyncedAt == default && a.LastRefreshFailedAt == null`
  - in each tier branch, change `a.LastSyncedAt <= X` to `a.LastSyncedAt <= X && (a.LastRefreshFailedAt == null || a.LastRefreshFailedAt <= X)`
  - update the class and method doc comments: `IsDue` measures from the later of the two timestamps, and `TtlFor` (the detail page) still reads `LastSyncedAt` alone
- [x] 1.4 In the same file, add `public static readonly Expression<Func<AnimeMetadata, DateTimeOffset>> LastAttemptAt = a => a.LastRefreshFailedAt > a.LastSyncedAt ? a.LastRefreshFailedAt.Value : a.LastSyncedAt;`, with a one-line comment on why a null mark falls through to `LastSyncedAt`.
- [x] 1.5 Generate the migration in the SDK 10 container. Copy `backend/` to `/private/tmp/bm-build/` (excluding `bin/` and `obj/`), run `dotnet ef migrations add AddAnimeMetadataLastRefreshFailedAt`, and copy back the migration, its designer file and `AnimeTrackerDbContextModelSnapshot.cs`. Check that `Up` adds only the nullable `timestamp with time zone` column and `Down` drops it.
- [x] 1.6 Tests in `Services/Metadata/RefreshTiersTests.cs`:
  - A finished anime on the 3-day tier, with `LastSyncedAt` 60 days ago and `LastRefreshFailedAt` 2 days ago, isn't due. With the failure at 3 days and 1 hour ago, it is.
  - A never-fetched anime (`LastSyncedAt == default`) with a failure 1 hour ago isn't due. Once the failure is older than its tier, it is.
  - A never-fetched anime with no failure is still due.
  - `LastAttemptAt` orders a row with an old sync and a recent failure after a row with a sync between the two.

## 2. Tally and failure classifier (design D1, D2)

- [x] 2.1 Add `Services/Metadata/MalCallTally.cs`: a `sealed class` with:
  - `Attempts`, `Succeeded` and `int? UnavailableAnimeId` (private setters), plus `bool MalUnavailable => UnavailableAnimeId is not null`
  - `RecordAttempt()`, `RecordSuccess()` and `RecordUnavailable(int animeId)`

  Its doc comment: one tally per stage of one pass; an attempt is recorded before the MAL call, so it counts even if the method throws later; `UnavailableAnimeId` names the anime whose call ended the stage, for the next pass to skip (design D6).
- [x] 2.2 Add `Services/Metadata/MalCallFailure.cs` with `enum MalCallFailureKind { Outage, NotFound, Other }` and `static MalCallFailureKind Classify(Exception ex)`, following design D2:
  - `AnimeMetadataNotFoundException`, or a 404 → `NotFound`
  - no status, a 5xx, 429 or 403 → `Outage`
  - `OperationCanceledException` → `Outage`
  - anything else → `Other`

  Doc-comment that callers must rethrow a cancellation of their own token before classifying, and that a 403 here has already outlasted `MalAuthPacingHandler`'s retries.
- [x] 2.3 Add `Services/Metadata/MalCallFailureTests.cs` covering each row:
  - `HttpRequestException` with no status, and `HttpIOException` → `Outage`
  - statuses 500, 503, 429 and 403 → `Outage`
  - `TaskCanceledException` with an inner `TimeoutException` → `Outage`
  - status 404 and `AnimeMetadataNotFoundException` → `NotFound`
  - statuses 400 and 401, `JsonException` and `InvalidOperationException` → `Other`

## 3. The refresh batch (design D3, D4, D6)

- [x] 3.1 In `Services/Metadata/IMetadataRefreshService.cs`, change the signature to `Task RefreshStaleBatchAsync(int batchSize, int? skipAnimeId, MalCallTally tally, CancellationToken ct = default)`. Update its doc comment to describe attempts, stopping on an outage, the skip id, and the 404 mark.
- [x] 3.2 In `Services/Metadata/MetadataRefreshService.cs` `RefreshStaleBatchAsync`:
  - when `skipAnimeId` is set, add `.Where(a => a.Id != skipAnimeId)` to the candidate query, before `Take`
  - order by `RefreshTiers.LastAttemptAt` instead of `a => a.LastSyncedAt`, and update the comment that explains the ordering
  - in the loop, call `tally.RecordAttempt()` right before `GetAnimeDetailsAsync`, and `tally.RecordSuccess()` after `RecordAsync`
  - put `catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }` ahead of the general catch
  - in the general catch, branch on `MalCallFailure.Classify(ex)`:
    - `NotFound`: set `anime.LastRefreshFailedAt = now` and log a warning that MAL has no such anime and it waits out its tier
    - `Outage`: log a warning, call `tally.RecordUnavailable(anime.Id)` and stop the loop. A `break` inside a `switch` only leaves the `switch`, so use an `if` chain or a flag.
    - `Other`: log a warning as today
  - replace `if (refreshed > 0) await db.SaveChangesAsync(ct);` with a save whenever `due.Count > 0`
  - return `Task`
- [x] 3.3 Update the 12 test fakes of `IMetadataRefreshService` to the new signature. They all keep throwing `NotImplementedException`:
  - `Services/Series/SeriesServiceRelatedEntriesTests.cs`
  - `Services/Series/SeriesGraphBuilderTests.cs`
  - `Services/Series/SeriesGraphBuilderTraverseAsyncTests.cs`
  - `Services/Series/SeriesGraphBuilderVersionSlotOrderingTests.cs`
  - `Services/Series/SeriesServiceClassificationRebuildTests.cs`
  - `Services/Series/SeriesGraphBuilderExtraGroupResolutionTests.cs`
  - `Services/Series/SeriesGraphBuilderReRootTests.cs`
  - `Services/Series/SeriesGraphBuilderVersionPersistenceTests.cs`
  - `Services/Transfer/TransferImportBackgroundServiceTests.cs`
  - `Services/Transfer/TransferImportRunnerTests.cs`
  - `Services/Updates/AnnouncementResolutionServiceTests.cs`
  - `Services/Detail/AnimeDetailServiceTests.cs`
- [x] 3.4 In `Services/Metadata/MetadataRefreshServiceTests.cs`:
  - give `FakeMalClient` an optional `Dictionary<int, Exception>` of failures that `GetAnimeDetailsAsync` throws, after recording the call
  - change existing `RefreshStaleBatchAsync` call sites to pass `skipAnimeId: null` and a `MalCallTally`, and assert `tally.Succeeded` where they asserted `refreshedCount`
- [x] 3.5 Add outage tests to the same file. Use five due anime with ascending `LastSyncedAt`, so the order is fixed.
  - A 503 on the second: calls are `[1, 2]`, `Attempts == 2`, `Succeeded == 1` and `UnavailableAnimeId == 2`. A fresh `AsNoTracking` read shows anime 1's `LastSyncedAt` advanced (the partial work was saved) and anime 2–5 unchanged.
  - `skipAnimeId: 1` with all five succeeding: anime 1 isn't called, and anime 2–5 are.
  - A `[Theory]` over no status, 500, 429 and 403 and a `TaskCanceledException` timeout. The pass stops after the first call, and the failing anime's `LastRefreshFailedAt` stays null and `LastSyncedAt` unchanged.
  - A cancelled token: `OperationCanceledException` propagates and nothing is marked.
  - A 400 on the second: all five are called, `Attempts == 5`, `Succeeded == 4`, `MalUnavailable` is false, and anime 2 has no failure mark.
- [x] 3.6 Add 404 tests to the same file:
  - **A 404 on the third of five.** Calls are all five, `Attempts == 5`, `Succeeded == 4`, `MalUnavailable` is false. Anime 3 has `LastRefreshFailedAt` set and `LastSyncedAt` unchanged. A second `RefreshStaleBatchAsync` on the same database makes no call for anime 3.
  - **A 404 on a never-fetched anime** leaves `LastSyncedAt == default`.
  - **Anime seeded on the 3-day tier** with `LastRefreshFailedAt` 4 days ago and MAL now returning it: it's refreshed, `LastSyncedAt` advances and `LastRefreshFailedAt` is null.
  - **The same anime with a failure 2 days ago** is not called.
  - **`RefreshOneAsync` on an anime with a failure mark** clears it, so any successful full-detail path clears it.

## 4. Announcement resolution (design D3, D6)

- [x] 4.1 In `Services/Updates/IAnnouncementResolutionService.cs`, change the signature to `Task ResolveAsync(int maxAnime, int? skipAnimeId, MalCallTally tally, CancellationToken ct = default)`. Update the doc comment: a 404 resolves the discovery without an announcement, other failures leave it unprocessed, an outage stops the loop and names its anime in the tally, and discoveries naming `skipAnimeId` wait for a later pass.
- [x] 4.2 In `Services/Updates/AnnouncementResolutionService.cs` `ResolveAsync`:
  - when `skipAnimeId` is set, add `.Where(d => d.RelatedAnimeId != skipAnimeId)` to the discovery query, before `Take`
  - call `tally.RecordAttempt()` right before `RefreshOneAsync`, and `tally.RecordSuccess()` after it returns
  - rethrow a cancellation of `ct`
  - branch the general catch on `MalCallFailure.Classify(ex)`:
    - `NotFound`: stamp `ProcessedAt = now` on the group, log a warning that MAL has no such anime and the discovery is resolved without an announcement, and continue
    - `Outage`: log a warning, call `tally.RecordUnavailable(animeId)` and stop the loop, leaving the group unprocessed (mind the `switch`/`break` trap again)
    - `Other`: log a warning and continue, leaving the group unprocessed
  - keep the final `SaveChangesAsync`, and return `Task`
  - update the D5 comment block where it says the failure retries next pass
- [x] 4.3 In `Services/Updates/AnnouncementResolutionServiceTests.cs`:
  - give `FakeMetadataRefreshService` a `Calls` list and an optional `Dictionary<int, Exception>` of failures. A missing id still throws `AnimeMetadataNotFoundException`.
  - pass `skipAnimeId: null` and a `MalCallTally` everywhere, and change `malCalls` assertions to `tally.Attempts`
  - in the tests that note "empty: any call would throw" (`AnAlreadyAnnouncedAnimeIsNotAnnouncedTwice`, `ADiscoveryNamingAnAlreadyFullyFetchedAnimeSpendsNoMalCall`, `ADiscoveryNamingAnAnimeWithAListEntryOfAnyStatusRecordsNothing`), also assert `Calls` is empty. After this change a call there would resolve the discovery and slip past the `ProcessedAt` assertion.
- [x] 4.4 Rework `AFailedFetchLeavesTheDiscoveryUnprocessed` to fail with `HttpRequestException` status 400. It should assert `Attempts == 1`, `MalUnavailable` is false, no update, and `ProcessedAt` null.
- [x] 4.5 Add tests to the same file:
  - **A discovery whose anime 404s** is processed, with no update and `Attempts == 1`. A second `ResolveAsync` makes no call.
  - **Two groups where the first resolves and the second gets a 503.** `Attempts == 2` and `UnavailableAnimeId` names the second group's anime. A fresh read shows the first group processed and the second unprocessed.
  - **Three groups where the first gets a 503.** Only one call is made, and the other two groups are untouched.
  - **`skipAnimeId` set to the oldest discovery's anime.** That discovery isn't called and stays unprocessed, and the next discovery is resolved.

## 5. The background pass (design D1, D3, D6)

- [x] 5.1 In `Services/Metadata/MetadataRefreshBackgroundService.cs`, add `private int? _skipAnimeIdNextPass;`, then move the body inside the loop's `try` into `internal async Task RunPassAsync(DateOnly today, CancellationToken ct)`:
  1. roll the window over
  2. return when no quota remains
  3. copy `_skipAnimeIdNextPass` into a local `skip` and set the field to `null`
  4. create the scope
  5. run resolution with `skip` and its own `MalCallTally`, adding `tally.Attempts` to `_callsThisWindow` in a `finally`
  6. log the resolution attempts and successes against the total when `Attempts > 0`
  7. when `MalUnavailable`, set `_skipAnimeIdNextPass = tally.UnavailableAnimeId`, log one information line that the pass ended because MAL was unavailable and that anime will be skipped next pass, and return
  8. recompute the quota and return when none remains
  9. run the batch with `skip` and a second tally the same way, log it the same way, and on `MalUnavailable` store its `UnavailableAnimeId` and log the ended-early line

  `ExecuteAsync` calls `RunPassAsync(DateOnly.FromDateTime(DateTime.UtcNow), stoppingToken)` and keeps its catches and delay. Add `internal int CallsThisWindow => _callsThisWindow;`, and update the class doc comment: attempts are counted, the pass ends on an outage, and the next pass skips the anime that ended it.
- [x] 5.2 Add `Services/Metadata/MetadataRefreshBackgroundServiceTests.cs`. Its fake `IServiceScopeFactory` builds a fresh `AnimeTrackerDbContext` per scope over one named in-memory database, like `ReconciliationBackgroundServiceTests`. It resolves real `AnnouncementResolutionService` and `MetadataRefreshService` instances over a shared scriptable fake `IMalClient` that records calls and throws per anime id.
- [x] 5.3 Tests in that file:
  - **MAL unreachable.** Seed one discovery needing a fetch and three due anime, with the client throwing no-status for every id. Three `RunPassAsync` calls each make exactly one call, and `CallsThisWindow` goes 1, 2, 3.
    - Pass 1 calls the discovery's anime.
    - Pass 2 skips it, has no other discovery, and calls the stalest due anime.
    - Pass 3 calls the discovery's anime again.
  - **One anime that always fails.** Seed five due anime, where only the stalest gets a 500 and the rest succeed with detail nodes that have no relations.
    - Pass 1 calls only that anime.
    - Pass 2 skips it and refreshes the other four.
    - Pass 3 calls it again.
  - **One-off failure.** Same seeding as above, but the stalest anime's client response switches to success after pass 1. Pass 3 refreshes it normally (its `LastSyncedAt` advances), and pass 4 makes no calls, so no skip was left behind.
  - **A 503 on the second of five due anime, no discoveries.** `CallsThisWindow == 2`, and the client saw only anime 1 and 2.
  - **A 503 on the announcement fetch with anime due.** One call, no refresh calls, and `CallsThisWindow == 1`.
  - **A 404 among five due anime.** `CallsThisWindow == 5`. A second pass makes no calls, since the other four are fresh and the 404 anime waits out its tier. Return detail nodes with no relations, so the refreshes write no discoveries for the second pass to resolve.
  - **The cap reached by failures.** Seed more due anime than the batch, all failing with a 400 (`Other`, so each pass spends its whole batch), and run passes on one `today` until `CallsThisWindow` reaches the cap. The next pass on that date makes no call. A pass with `today.AddDays(1)` makes calls again and resets the counter. Make `NightlyCap` `internal` so the test loops up to it instead of hard-coding 500.

## 6. Build, test and check

- [x] 6.1 Copy `backend/` to `/private/tmp/bm-build/` (excluding `bin/` and `obj/`), then run `dotnet build` and `dotnet test` in `mcr.microsoft.com/dotnet/sdk:10.0`. Everything must pass, including the series, transfer and detail suites whose fakes changed.
- [x] 6.2 In the same container, confirm `dotnet ef migrations has-pending-model-changes` reports none after the new migration.
- [x] 6.3 Grep for any remaining caller that treated either method's return value as a count (`RefreshStaleBatchAsync(`, `ResolveAsync(`), and for any `OrderBy(a => a.LastSyncedAt)` left in the job.

## 7. Docs

- [x] 7.1 In `CODE_GUIDE.md` `### Services/Metadata/`:
  - **`RefreshStaleBatchAsync` bullet:** candidates are ordered by `RefreshTiers.LastAttemptAt`. A 404 sets `LastRefreshFailedAt`, which `IsDue` measures like a refresh and `ApplyTo` clears. An outage-kind failure stops the loop.
  - **`RefreshTiers` note:** `IsDue` honours the failure mark and `TtlFor` doesn't.
  - **`MetadataRefreshBackgroundService` paragraph:** the cap counts attempts through `MalCallTally`, a failed announcement fetch skips the batch, the next pass skips the anime whose call ended the pass (kept in memory only), and `MalCallFailure` defines the outage kinds.
- [x] 7.2 In `CODE_GUIDE.md` `### Services/Updates/`, fix the `AnnouncementResolutionService` bullet. It currently says a failed fetch is marked processed and retried, which contradicts itself. It should say: a 404 resolves the discovery without an announcement, other failures leave it for a later pass, and an outage ends the loop.
- [x] 7.3 In `CODE_GUIDE.md` `### Models/`, mention `LastRefreshFailedAt` beside `LastSyncedAt` in the `AnimeMetadata` description.
- [x] 7.4 In `docs/ISSUE_TRIAGE.md` (local, gitignored), following the pattern B2, R2 and PF3 used:
  - replace the R4 entry with a short blockquote pointing to the resolved file
  - update the Summary table (Still present count, and the "Resolved 2026-09-14 (code fixes)" row, or a new dated row)
  - mark Phase 2 of the Fix order done
  - update the appendix rows that name R4 (ISSUES #25, SECURITY_REVIEW action 3, SPEC_CONFORMANCE cap note)
- [x] 7.5 In `docs/ISSUE_TRIAGE_RESOLVED_2026-09-13.md`, add an `## R4.` section under the code-fixes heading. It should cover what was fixed (attempts counted, the pass ends on an outage, 404 handling for candidates and discoveries), name this change, and record the skip-next-pass guard with its remaining limit: two anime that always fail at the same time would still stall the job.
