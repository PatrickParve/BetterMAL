## Context

This change fixes triage item R4. The proposal has the evidence. This section covers only what shapes the design.

**The pass today.** `MetadataRefreshBackgroundService.ExecuteAsync` owns the cap window: `_capWindowDate`, `_callsThisWindow` and the `NightlyCap` of 500. Every 10 minutes it:
1. rolls the window over when the UTC date changes;
2. creates one DI scope;
3. calls `ResolveAsync(min(10, remaining))` and adds its return value;
4. calls `RefreshStaleBatchAsync(min(20, remaining))` and adds its return value.

A throw from either method is caught by the loop's `catch (Exception)`, which logs "Metadata refresh pass failed." The loop then waits for the next tick.

**The two loops.**
- `MetadataRefreshService.RefreshStaleBatchAsync` loads the due candidates tracked (`Include(RelatedAnime)`, `Where(IsDue)`, `OrderBy(LastSyncedAt)`, `Take`). It calls `IMalClient.GetAnimeDetailsAsync` directly for each one, applies the result, and calls `SaveChangesAsync` once at the end, but only when `refreshed > 0`.
- `AnnouncementResolutionService.ResolveAsync` takes unprocessed discoveries oldest first and groups them by `RelatedAnimeId`. A group whose anime is already fully fetched or in my list is marked processed with no call. Otherwise it calls `IMetadataRefreshService.RefreshOneAsync`, which saves its own write and turns a 404 into `AnimeMetadataNotFoundException`. `ProcessedAt` stamps are saved once at the end.

**How MAL failures arrive.**

| MAL outcome | What the loop catches |
|---|---|
| no response (DNS, refused, reset) | `HttpRequestException`, `StatusCode == null` |
| `HttpClient` timeout (default 100 s) | `TaskCanceledException`, caller's token not cancelled |
| 5xx, 429 | `HttpRequestException` with that `StatusCode` |
| 403 | `HttpRequestException(403)`. `MalAuthPacingHandler` has already retried it up to 5 times. |
| 404, batch path | `HttpRequestException(404)` |
| 404, resolution path | `AnimeMetadataNotFoundException` |
| 400, a 401 on the client-id request, unreadable body | `HttpRequestException(4xx)`, `JsonException`, `InvalidOperationException` |
| host shutting down | `OperationCanceledException`, caller's token cancelled |

**`LastSyncedAt` means "has had a full fetch".**
- `MalAnimeNode.ApplyTo` (`MalMappingExtensions.cs:155`) is its only writer.
- `RefreshTiers.TtlFor` (the detail page) and `RefreshTiers.IsDue` (this job) both measure staleness from it.

**Constraints.**
- The backend builds, tests and generates migrations only in `mcr.microsoft.com/dotnet/sdk:10.0`, from a copy under `/private/tmp`. The local SDK is 9.0, and `~/Documents` can't be bind-mounted.
- The database is PostgreSQL through Npgsql. Tests use the EF in-memory provider.

## Goals / Non-Goals

**Goals:**
- Every MAL call the job starts counts once against the daily cap, including a call made before the method later throws.
- While MAL is down, each pass makes at most one attempt, and a failed announcement fetch also skips the refresh batch.
- An anime MAL has deleted costs at most one call per tier interval as a refresh candidate, and one call in total as a discovery.
- One anime that MAL always fails with an outage-type error can't stop the job for good.
- No surface other than this job changes behaviour. `LastSyncedAt`, `TtlFor` and `IMalClient` stay as they are.

**Non-Goals:**
- An app-wide "MAL is down" state, a circuit breaker, or a backoff.
- Telling a throttling 403 apart from a real refusal (R1).
- 404 handling on the detail page, in series builds or in the on-demand refresh.
- Counting series builds triggered by metadata this job fetches.
- Changing the cap, the tick, the batch sizes, the order of the two stages, or the tier ladder.

## Decisions

### D1. A per-pass `MalCallTally` the methods fill in, instead of a returned count

Add `Services/Metadata/MalCallTally.cs`: a small mutable class with `Attempts`, `Succeeded` and `UnavailableAnimeId` (`int?`), plus `MalUnavailable => UnavailableAnimeId is not null`. They are set through `RecordAttempt()`, `RecordSuccess()` and `RecordUnavailable(int animeId)`. The signatures become (the skip parameter is D6):

- `Task RefreshStaleBatchAsync(int batchSize, int? skipAnimeId, MalCallTally tally, CancellationToken ct = default)`
- `Task ResolveAsync(int maxAnime, int? skipAnimeId, MalCallTally tally, CancellationToken ct = default)`

Each loop calls `tally.RecordAttempt()` immediately before its MAL call (`GetAnimeDetailsAsync` in the batch, `RefreshOneAsync` in resolution), and never on a branch that makes no call. The background service creates one tally per stage and adds `tally.Attempts` to `_callsThisWindow` in a `finally`. Attempts therefore count even when the method throws afterwards, for example when the batch's final `SaveChangesAsync` fails.

**Alternatives considered:**
- **Return a `record (int Attempts, bool MalUnavailable)`.** Rejected. A throw after N calls loses all N. A batch whose final save keeps failing would re-fetch the same 20 anime every pass, uncounted, which is the same shape of bug as R4.
- **Throw a `MalUnavailableException` carrying the attempt count.** Rejected. It uses an exception for an expected outcome, the success path still needs a second channel for its count, and a throw from elsewhere still loses the count.

### D2. One classifier, local to this job

Add `Services/Metadata/MalCallFailure.cs` with `enum MalCallFailureKind { Outage, NotFound, Other }` and `static MalCallFailureKind Classify(Exception ex)`:

- `AnimeMetadataNotFoundException`, or `HttpRequestException` with a 404 → `NotFound`
- `HttpRequestException` with no status (this includes `HttpIOException`, for a connection dropped mid-body) → `Outage`
- `HttpRequestException` with a 5xx, 429 or 403 → `Outage`
- `OperationCanceledException` → `Outage`. The call site has already rethrown a host shutdown, so any cancellation that gets here is a timeout.
- anything else → `Other`

Both loops put `catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }` ahead of their general catch, so a shutdown is never classified.

Every 403 counts as `Outage` because `MalAuthPacingHandler` only returns one after its fifth attempt, so any 403 that reaches the job has already outlasted the retries.

**Alternatives considered:**
- **Typed exceptions thrown by `MalClient`.** Rejected. That would change what every `IMalClient` caller sees (detail page, series builds, sync), and all of those are out of scope. R1 in Phase 3 may later move this classifier into `Services/Mal/`.
- **Classify in the background service.** Rejected. The loops have to stop themselves and still save what they finished.

### D3. The loops stop, skip or record, and always save what they did

**`RefreshStaleBatchAsync`**, for each candidate: record the attempt, then fetch, apply and `RecordAsync`, then record the success. On a failure:
- `NotFound`: set `anime.LastRefreshFailedAt = now`, log a warning naming the anime, and continue.
- `Outage`: log a warning naming the anime and the failure, call `tally.RecordUnavailable(anime.Id)`, and leave the loop.
- `Other`: log a warning and continue, as today.

After the loop, call `SaveChangesAsync` whenever any candidate was loaded, not only when one succeeded. A 404 mark has to be saved even in a pass with no successes, and EF makes no database call when nothing changed.

**`ResolveAsync`**, for each group that needs a fetch: record the attempt, call `RefreshOneAsync`, and record the success. On a failure:
- `NotFound`: stamp `ProcessedAt` on the group, log a warning that the anime was resolved without an announcement, and continue.
- `Outage`: log, call `tally.RecordUnavailable(animeId)`, and leave the loop with the group unprocessed.
- `Other`: log and continue with the group unprocessed.

The final `SaveChangesAsync` still runs, so groups resolved earlier in the pass are kept.

**The background service** moves its per-tick body into `internal Task RunPassAsync(DateOnly today, CancellationToken ct)`, and `ExecuteAsync` calls it with `DateOnly.FromDateTime(DateTime.UtcNow)`. The pass:
1. rolls the window over;
2. returns if no quota remains;
3. takes the skip id left by the previous pass and clears the field (D6);
4. runs resolution with that skip id and its own tally;
5. when `resolution.MalUnavailable` is set, stores `resolution.UnavailableAnimeId` for the next pass and returns without running the batch;
6. recomputes the remaining quota and runs the batch with the same skip id and a second tally, storing its `UnavailableAnimeId` the same way.

It logs the attempts for each stage (and the successes, for context) against the running total. When a stage ends early it logs one information line saying the pass ended because MAL was unavailable. `internal int CallsThisWindow` exposes the counter to tests. `InternalsVisibleTo` is already set.

**Alternative considered:** inject a `TimeProvider` so tests can drive `ExecuteAsync` itself. Rejected. Nothing else in the backend uses one, and passing `today` into the pass method gives tests the day rollover without a clock abstraction.

### D6. The next pass skips the anime that ended the previous one

An outage-type failure marks nothing (D3), so the anime it happened on stays first in the queue: stalest among refresh candidates, or oldest among discoveries. MAL could keep failing one anime with a 5xx while every other request works, for example because of broken data behind that entry. Then every pass would try that anime first, fail and end. The refresh batch would never run again, and if the anime were a discovery, neither would later resolutions. Today's code has no such risk, because it just moves past the failure.

To prevent this, the background service keeps one field, `private int? _skipAnimeIdNextPass`:
- A pass takes its value and clears it before running either stage.
- It passes the value to both methods as `skipAnimeId`.
- When a stage ends on an outage, the pass stores that stage's `UnavailableAnimeId` in the field.

The methods apply the skip in their queries, before `Take`, so the pass still fills its allowance with other work:
- the batch adds `Where(a => a.Id != skipAnimeId)`;
- resolution adds `Where(d => d.RelatedAnimeId != skipAnimeId)` to the discovery query.

The skip applies to both stages, because an id is harmless to skip where it doesn't appear.

**What it does in each case:**

| Situation | Passes |
|---|---|
| MAL really down | fail on X → skip X, fail on Y → skip Y, fail on X → … Still one attempt per pass. |
| Only X is broken | fail on X → skip X, everything else refreshes → X tried again, fails → … The job does its normal work every other pass, and X costs 72 counted calls a day. |
| X was a one-off failure | fail on X → skip X, normal pass → X refreshes normally. |

The field lives only in memory. A restart forgets it, which at worst costs one extra failed pass.

**Alternatives considered:**
- **Accept the stall.** Rejected by the user. It's the simplest, but the job would fail silently and the only trace would be a repeated warning in the backend console log.
- **After an outage-type failure, try one more, different anime before ending the pass.** Rejected. It needs no memory and the job would run every pass, but a real outage would cost up to two attempts per pass (288 a day) instead of one, and the rules for carrying a pending failure from resolution into the batch get tangled.
- **Record outage failures on the row (a column on `AnimeMetadata` and `RelationDiscovery`) with a short cool-off.** Rejected. It survives restarts and handles several broken anime at once, but it needs a second column on each of two tables, and it goes against the brief's "an outage marks nothing" for a case that has never been seen.

### D4. `LastRefreshFailedAt`, measured as a refresh by the job alone

- **Model.** `AnimeMetadata` gets `DateTimeOffset? LastRefreshFailedAt`, a nullable column with no index. `LastSyncedAt` has no index either, and the due query only runs over my-list and adjacent anime.
- **Writer.** Only the batch's `NotFound` branch sets it.
- **Clearing.** `MalAnimeNode.ApplyTo` sets it to `null` next to `LastSyncedAt = now`, so any successful full-detail fetch clears it, by any path. `ApplyTo` is already the single writer of the "has had a full fetch" state, so the two can't disagree.
- **`RefreshTiers.IsDue`.** The ladder is measured from the later of the two timestamps, without a SQL `CASE`:
  - every `a.LastSyncedAt <= cutoff` becomes `a.LastSyncedAt <= cutoff && (a.LastRefreshFailedAt == null || a.LastRefreshFailedAt <= cutoff)`, since the later of two times is at or before a cutoff exactly when both are;
  - the "never fetched, always due" short-circuit becomes `a.LastSyncedAt == default && a.LastRefreshFailedAt == null`.
- **Ordering.** Add `RefreshTiers.LastAttemptAt`, `a => a.LastRefreshFailedAt > a.LastSyncedAt ? a.LastRefreshFailedAt.Value : a.LastSyncedAt`, which Npgsql translates to a `CASE`. The batch orders by it instead of `LastSyncedAt`. A null mark compares false, so such a row sorts by `LastSyncedAt` as today.
- **Tier choice.** The tier is chosen from the anime's cached fields as today. A never-fetched lean row that has no airing status and no end date falls to the 28-day tier.
- **Detail page.** `RefreshTiers.TtlFor` and `AnimeDetailService` don't change. The detail page still decides on `LastSyncedAt` alone, so opening a 404 anime behaves exactly as it does now.

**Alternatives considered:**
- **Stamp `LastSyncedAt` on a 404.** Rejected. Six files read it as "has had a full fetch". Announcement resolution would treat the anime as known, the detail page would serve the thin row as complete, and relation resolution and adjudication would trust an empty relation set.
- **Only sort 404s to the back.** Rejected. Most passes have fewer than 20 anime due, so a 404 would still be tried every pass.
- **A permanent "gone" flag.** Rejected. MAL does restore and merge entries. One retry per tier interval costs at most one call a day, and a success clears the mark.
- **A failure count with exponential backoff.** Rejected. It wasn't asked for, and the tier interval is already the anime's natural cadence.

### D5. The migration

- `dotnet ef migrations add AddAnimeMetadataLastRefreshFailedAt`, generated in the SDK 10 container and copied back with its designer file and the updated model snapshot.
- `Up` adds a nullable `timestamp with time zone` column, and `Down` drops it.
- No data migration: every existing row starts with no failure recorded, which is correct because no 404 has ever been recorded.

## Risks / Trade-offs

- **Two anime that MAL always fails, at the same time, still stall the job.** The skip (D6) remembers only one id, so the two would take turns ending every pass. → Accepted. One such anime has never been seen, let alone two at once. The warning names each anime in the backend log, and the row-marking alternative in D6 is the follow-up if it ever happens.
- **A single broken anime halves the job's pace.** → The normal load is about 25 refreshes a day. Running every other pass still allows about 1,440 (20 per pass × 72 passes), so nothing falls behind.
- **Other failures still spend the whole batch.** A revoked client id gives a 401 on every client-id call, which is `Other`, so each pass tries all 30. → Those attempts now count, so the job stops for the day after about 17 passes (under 3 hours) instead of never. Making 401 an outage kind was left out, since the brief lists the outage kinds explicitly.
- **A 404 that MAL returns only briefly** delays that anime's next job refresh by one tier interval (up to 28 days for a long-finished anime). → The detail page and the on-demand refresh ignore the mark, so opening the anime still refreshes it and clears the mark.
- **A real refusal returned as 403 ends every pass.** → That's R1 (Phase 3). Until then it costs at most one counted call per pass.
- **A 12-fake signature change.** Every test fake of `IMetadataRefreshService` has to take the new `RefreshStaleBatchAsync` parameter. → The edit is mechanical: all of them throw `NotImplementedException` from that method.

## Migration Plan

- Deploy as a normal release. The migration runs at startup like earlier ones and only adds a nullable column.
- Rollback: redeploy the previous image. The old model doesn't map the extra column, so it is simply ignored. Run `Down` only if the column must go.
