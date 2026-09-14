## Why

The scheduled metadata refresh job can't see its own failures (triage item **R4** in `docs/ISSUE_TRIAGE.md`).

- Its 500-calls-a-day cap counts only successful calls.
- It keeps going when MAL is down.
- It retries an anime MAL has deleted on every pass, forever.

During a long MAL outage none of these calls count, so nothing limits them. How many there are depends on how MAL fails. The figures below assume a full batch of 20 due anime per pass and no pending discoveries, which is what a long outage produces. The next pass starts 10 minutes after the previous one ends.

| How MAL fails | Per day today, none counted |
|---|---|
| refuses fast (connection refused, instant 5xx) | ~2,800 calls (up to ~4,300 if 10 discoveries are also waiting) |
| hangs until the 100 s `HttpClient` timeout | ~670 calls |
| throttles with 403 (each call tried 5 times by the handler) | ~6,500 HTTP requests |

The pacer only spaces requests about a second apart, so page actions wait at most a second or two longer. The more likely harm is the 403 case: continuing to send requests while MAL is throttling may keep that throttling going, and it also hits page actions. Separately, once failures are counted, each deleted anime would use 144 of the 500 daily calls.

**Observed state** (2026-09-14): no refresh, resolution or 403 warnings in the backend log since the container last started (about 9 hours of log). All 617 list anime have been fully fetched within their tier, and there are 0 unprocessed discoveries. The problem is real in the code but isn't causing harm today.

**Verified against the code** (2026-09-14, at `7f57c85`):

- **Only successes are counted.**
  - `MetadataRefreshBackgroundService.cs:47,58` adds what the two methods return to `_callsThisWindow`.
  - `MetadataRefreshService.cs:73` increments only after a successful fetch and write.
  - `AnnouncementResolutionService.cs:69` increments only after `RefreshOneAsync` returns.
  - `metadata-refresh/spec.md:152` already caps the job "by request count".
- **Every failure moves on to the next anime.** `MetadataRefreshService.cs:75` and `AnnouncementResolutionService.cs:71` both `catch (Exception)`, log, and continue. Nothing tells the background service that MAL is down.
- **A deleted anime stays at the front of the queue.**
  - A failed refresh leaves `LastSyncedAt` untouched, and candidates are ordered by it (`MetadataRefreshService.cs:49`, `RefreshTiers.IsDue`).
  - A failed resolution leaves its discovery unprocessed, and discoveries are taken oldest first (`AnnouncementResolutionService.cs:26`). `metadata-refresh/spec.md:123` and `anime-updates/spec.md:442` both require that retry.
- **How failures reach the job.**
  - `MalClient.GetAsync` calls `EnsureSuccessStatusCode`, so a failed status arrives as `HttpRequestException` with `StatusCode` set.
  - With no response, `StatusCode` is null.
  - An `HttpClient` timeout arrives as `TaskCanceledException` while the job's own token is not cancelled.
  - `MalAuthPacingHandler.cs:54-64` retries a 403 up to 5 times and then returns it, so any 403 the job sees has already outlasted those retries.
  - `RefreshOneAsync` (`MetadataRefreshService.cs:103`) turns a 404 into `AnimeMetadataNotFoundException`. The batch loop calls `GetAnimeDetailsAsync` directly and sees the raw 404.
- **`LastSyncedAt` can't carry a failure.** Six files read `LastSyncedAt != default` (or `== default`) as "this anime has had a full fetch": `AnnouncementResolutionService.cs:48`, `AnimeMetadataChangeDetector.cs:118`, `AnimeDetailService.cs:113`, `RelationResolver.cs:81,96`, `RelationAdjudicationService.cs:111,134` and `RefreshTiers.cs:72`.
- **An existing test depends on today's behaviour.** `AnnouncementResolutionServiceTests.AFailedFetchLeavesTheDiscoveryUnprocessed` simulates its failure with `AnimeMetadataNotFoundException` and asserts 0 calls. After this change that exception resolves the discovery, and the attempt counts.

## What Changes

- **Every attempt counts against the daily cap.**
  - Each MAL call the announcement resolution or the refresh batch starts counts once, whether it succeeds or fails.
  - It counts once per anime, not once per retry inside `MalAuthPacingHandler`.
  - A discovery skipped without a call, because it's already fully fetched or already in my list, doesn't count.
  - An attempt still counts if the method throws later in the pass, for example when its final save fails.
- **A pass ends at the first sign MAL is down or pushing back.** These failures stop the rest of the pass:
  - no response
  - a request timeout (not the host shutting down)
  - a 5xx or a 429
  - a 403, which by then has outlasted the handler's retries

  When this happens during announcement resolution, that pass's refresh batch is skipped as well. There's no delay, backoff or shared "MAL is down" state: the next 10-minute pass is the retry. The failed call still counts, and nothing is marked failed or resolved. Work already done in the pass is still saved.
- **The next pass skips the anime whose call ended the pass.** The job keeps that one anime id in memory for one pass. Without this, one anime that always gets a 5xx from MAL while everything else works would stay first in the queue, end every pass, and stop the job for good.
  - During a real outage this changes nothing: each pass still makes one attempt, just on a different anime.
  - With one broken anime, the job refreshes normally every other pass, and that anime is retried on the passes in between.
  - The id is forgotten on restart, which does no harm.
- **Any other failure** (for example a 400, a 401 on the client-id request, or an unreadable body) counts, skips that anime for this pass and moves on, as today.
- **A 404 means that anime is gone, not that MAL is down.** The pass continues with the next anime.
  - **Refresh candidate:**
    - The job records when the attempt failed in a new nullable column, `AnimeMetadata.LastRefreshFailedAt`.
    - Whether the anime is due, and where it sits in the queue, are measured from the later of its last full fetch and that failed attempt, so it waits out its tier's normal interval.
    - `LastSyncedAt` is not touched.
    - Any later successful full-detail fetch clears the mark.
    - Only a 404 sets it.
  - **Discovery:** the discovery is marked processed, nothing is announced, and a log line names the anime. Every other failure still leaves the discovery for a later pass.
- **Internal API shapes change. No HTTP or client-visible change.**
  - `IMetadataRefreshService.RefreshStaleBatchAsync` and `IAnnouncementResolutionService.ResolveAsync` take an optional anime id to skip and a per-pass `MalCallTally` that they fill in, instead of returning a success count.
  - The background service gets a pass method it runs once per tick, so tests can drive single passes.
- **Unchanged:** the 500/day cap, the 10-minute tick, the batch sizes (10 and 20), announcements running first, the staleness tiers themselves, and the detail page's own tier check (`RefreshTiers.TtlFor`).

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `metadata-refresh`:
  - **Rewritten:** "Scheduled tiered staleness refresh for my-list anime". Staleness is measured from the last full-detail fetch or a later not-found attempt, and a never-fetched anime with a not-found attempt no longer sorts first.
  - **Rewritten:** "Announcement resolution shares the refresh job's pacing and cap". A not-found anime resolves its discovery without an announcement. Every other failure is still retried.
  - **Rewritten:** "Per-day batch cap ordered by staleness, spread across 10-minute passes". Failed calls count against the cap, candidates are ordered by time since their last attempt, and a cap reached by failures holds until the next UTC day.
  - **Added:** "A pass ends when MyAnimeList is down or pushing back". Covers each failure kind, a failed announcement fetch skipping the refresh batch, and the next pass skipping the anime that ended it.
  - **Added:** "A refresh candidate MyAnimeList does not have waits out its tier". Covers the 404 handling for refresh candidates.
- `anime-updates`: **Rewritten:** "Newly-discovered relations are resolved before they become news". Its retry rule gets the same not-found exception, since it states the same retry `metadata-refresh` does.

## Impact

- **Backend** (under `backend/AnimeTracker.Api/`):
  - `Services/Metadata/`:
    - `MetadataRefreshBackgroundService.cs`
    - `MetadataRefreshService.cs` and `IMetadataRefreshService.cs`
    - `RefreshTiers.cs`: `IsDue` and a last-attempt ordering expression
    - new `MalCallTally.cs` and `MalCallFailure.cs`
  - `Services/Updates/AnnouncementResolutionService.cs` and `IAnnouncementResolutionService.cs`
  - `Models/AnimeMetadata.cs`: `LastRefreshFailedAt`
  - `Services/Mal/MalMappingExtensions.cs`: `ApplyTo` clears the mark
- **Migration:** one nullable `timestamp with time zone` column on `AnimeMetadata`, plus the model snapshot. Existing rows start with no failure recorded.
- **Backend tests** (under `backend/AnimeTracker.Api.Tests/`):
  - **New:** `Services/Metadata/MetadataRefreshBackgroundServiceTests.cs`
  - **Extended:** `MetadataRefreshServiceTests`, `AnnouncementResolutionServiceTests` and `RefreshTiersTests`
  - **Updated:** the 12 test fakes that implement `IMetadataRefreshService`, for the new `RefreshStaleBatchAsync` signature
- **Docs:**
  - `CODE_GUIDE.md`: the `Services/Metadata/` and `Services/Updates/` notes
  - `docs/ISSUE_TRIAGE.md` (local, gitignored): R4 moves to Fixed
- **Out of scope:**
  - an app-wide "MAL is down" state or circuit breaker
  - the pending-edit retry (`Services/Sync/PendingSyncRetryBackgroundService.cs`)
  - telling a throttling 403 apart from a real refusal (R1, Phase 3)
  - 404 handling anywhere outside this job: the detail page, series builds, on-demand refresh
  - series builds triggered by metadata the job fetches, which stay uncounted
