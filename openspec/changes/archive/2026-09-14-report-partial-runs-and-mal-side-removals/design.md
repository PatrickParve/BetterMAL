## Context

This change bundles three triage items from `docs/ISSUE_TRIAGE.md`: B2, R2 and PF3. The proposal has the evidence. This section covers only what shapes the design.

**Job plumbing (B2, R2).**
- `IJobProgressSink` has only `SetTotal` and `ReportProgress`. A service never ends a run itself.
- Whoever owns the tracker ends the run:
  - a trigger loop (`AiringFullRefreshBackgroundService`, `SeriesBulkBuildBackgroundService`);
  - a `BackgroundJobRunner` lambda in `SyncController`, which calls `Fail` after the work returns, as sync now and accept/decline-all already do;
  - `InitialImportService`, which owns `ListImportProgress` directly.
- `BackgroundJobRunner` calls `Complete()` only when the work leaves the tracker still `Running`, so a `Fail` inside the lambda stands.
- The weekly reconciliation has no tracker. It records `LastRunFailed`/`LastRunError` on `ReconciliationRunLog`, and the Settings "Weekly check" line and the navbar dot already read those.

**The status mapping (R2).**
- `MalMappingExtensions.ToWatchStatus` is the only place that turns a MAL status string into `WatchStatus`, and it throws on anything unknown.
- It is reached only through `MalListStatus.ApplyTo(UserAnimeEntry)`, directly or through `ToUserAnimeEntry`. `ApplyTo` maps a null list status, or a null `Status`, to `PlanToWatch`. That is existing behaviour for an absent value and stays as it is.
- Every caller:

| Site | Per-item catch today | Effect of a throw |
|---|---|---|
| `ReconciliationService.cs:80` | none | whole run aborts; the lean `AnimeMetadata` row added at `:78` for a new anime is lost with it |
| `InitialImportService.cs:101` (entry-only branch) | none | whole import aborts |
| `InitialImportService.cs:134` (`ImportOneAsync`) | yes | counted as "couldn't be fetched", after a wasted details fetch |
| `HeldChangeService.cs:44,65,103` (`GetHeldAsync`) | none | the review read fails |
| `HeldChangeService.cs:224` (`DeclineEntryAsync`) | none | the decline answers 500; decline-all aborts |
| `HeldChangeService.cs:256` (`DeclineRemovalAsync`) | none | same |

**Reconciliation (PF3).**
- `RunLockedAsync`:
  - reads MAL's full list (paged, and slow for a long list);
  - then loads every local `UserAnimeEntry` (tracked) and the pending-removal ids;
  - walks the remote edges, building `Added`/`Updated` diff entries.
- `AcceptPendingDiffAsync` applies every entry as create-or-update and skips any entry that is `PendingSync`. It records no activity (`activity-recording`, "The sync paths record nothing").
- `PendingReconciliationDiffEntry.ChangeType` is stored as text (`HasConversion<string>()`). Its value columns are non-nullable, apart from score and dates.
- `EntryPushService.cs:36` stamps `UserAnimeEntry.LastSyncedAt` on every successful push. The import, a held decline and an accepted diff stamp it too.

**Constraints.**
- The backend builds and tests only in `mcr.microsoft.com/dotnet/sdk:10.0`, from a copy under `/private/tmp` (the local SDK is 9.0, and `~/Documents` can't be bind-mounted).
- The frontend has no test runner. It builds with nvm's Node 22 and is checked by hand.

## Goals / Non-Goals

**Goals:**
- Both manual data-tool jobs end as failed, saying how many, when any item failed. "No data" results are not failures.
- One anime with an unrecognized MAL status can't stop a reconciliation run, the import, the held-change review or a decline. That anime is never guessed at, and it leaves a visible trace in Settings and a log line naming it and its raw status.
- Reconciliation proposes removing local entries that MAL no longer lists. Accepting deletes them locally, with nothing pushed.
- Reuse the existing reporting throughout: `Fail(message)`, the weekly run log, `RemoteUnavailable`, and the existing diff review.

**Non-Goals:**
- A "completed with a note" job phase, or any new job DTO field.
- Mapping an unknown status to anything, or letting it be configured.
- Per-item accept or decline in the reconciliation review. It stays all-or-nothing.
- Changing the import's retry schedule or the rules that decide when it retries.
- Guarding against MAL returning an incomplete list (R3). That is separate work.
- Anything under `ResyncService` (already deleted) or B5.

## Decisions

### D1. `RefreshManyAsync` returns both counts

`IEpisodeScheduleRefreshService.RefreshManyAsync` returns `RefreshManyResult(int NoData, int Failed)` in place of the `int zeroRows`:
- `RefreshOneCoreAsync` returning `0` counts toward `NoData`;
- a caught exception counts toward `Failed`.

`EpisodeScheduleRefreshBackgroundService` (`:88`, `:103`) keeps discarding the result.

`AiringFullRefreshBackgroundService` captures the result:
- `Failed > 0`: `progress.Fail($"{Failed} of {targets.Count} anime couldn't be refreshed from AniList.")`
- otherwise: `progress.Complete()`.

**Alternatives considered:**
- Return only the failed count. Rejected: it throws away the no-data count, which costs nothing to keep and is useful when logging.
- Count failures in the background service through the `onProgress` callback. Rejected: the callback can't tell a failure from a success.

### D2. Build all series counts failures in its existing catch

`SeriesBulkBuildBackgroundService.RunAsync` adds `failed++` to the `catch (Exception ex) when (ex is not OperationCanceledException)` branch. A `SeriesNotFoundException` stays a non-failure. After the loop:
- `failed > 0`: `progress.Fail($"{failed} of {targets.Count} series couldn't be built.")`
- otherwise: `progress.Complete()`.

A target that was already covered, or that failed, still counts as processed, so the bar reaches its total as it does today.

### D3. One list of known statuses, checked before mapping rather than caught

In `MalMappingExtensions`:
- `TryToWatchStatus(this string malStatus, out WatchStatus status)` holds the one `switch` over the five known strings. Adding a status later means editing only this.
- `ToWatchStatus` becomes `TryToWatchStatus(...) ? status : throw new ArgumentOutOfRangeException(...)`, unchanged for any caller.
- `HasRecognizedStatus(this MalListStatus? status)` is true when `status?.Status` is null (the existing Plan-to-watch default) or `TryToWatchStatus` succeeds.

Reconciliation and the import call `HasRecognizedStatus` **before** doing anything for an edge, then `continue`. The warning names the anime id and the raw `edge.ListStatus.Status`.

**Alternatives considered:**
- Catch `ArgumentOutOfRangeException` around each call. Rejected for two reasons: it is broad enough to hide unrelated bugs, and in reconciliation the throw comes after the lean metadata row has already been added, so a skip would still cache something.
- Map an unknown status to Plan to watch, which the triage also suggested. Rejected by the user: an unknown status must be surfaced and added by hand, never guessed.
- A dedicated exception type thrown by `ToWatchStatus`. Rejected as unnecessary: the pre-check keeps skipping a normal branch, not an exception path.

### D4. Reconciliation: skip, but still count the anime as seen

In `RunLockedAsync`'s loop:
1. `seenAnimeIds.Add(animeId)` comes **first**, before any `continue`. An anime MAL lists is never "removed on MAL", whether it is skipped as a pending removal, for its status, or as a pending edit.
2. The pending-removal skip stays as it is. It runs before the status check, so an anime being removed doesn't raise a status warning.
3. `if (!edge.ListStatus.HasRecognizedStatus())`: log the warning, `skippedUnrecognized++`, `continue`. This comes before the lean-metadata add, so nothing at all is written for that anime.
4. The rest of the walk is unchanged.

`ReconciliationResult` gains `RemovedOnMal` and `SkippedUnrecognized`. The completion log line reports both.

`RunAsync` still never ends a run, since the sink can't. Its two callers read `SkippedUnrecognized`:
- **Manual** (`SyncController.Reconcile` lambda): `if (result.SkippedUnrecognized > 0) reconcileProgress.Fail(JobFailure.UnrecognizedStatuses(result.SkippedUnrecognized))`, following sync now's pattern.
- **Weekly** (`ReconciliationBackgroundService`): `RecordRunOutcomeAsync(failed: result.SkippedUnrecognized > 0, error: failed ? JobFailure.UnrecognizedStatuses(...) : null, ...)`.

The diff is saved either way. The Settings page reloads the pending diff whenever the reconcile job leaves `Running`, whatever its phase, so a "failed" manual run still shows its diff.

`JobFailure` gains `UnrecognizedStatuses(int leftOut, int? total = null)`, so the manual run, the weekly line and the import use the same words:
- without a total: `"Left out {n} anime whose MyAnimeList list status this app doesn't recognize — the backend log names each one."`
- with a total: `"Left out {n} of {total} anime whose …"`

**Alternative considered:** give `IJobProgressSink` a `Fail`. Rejected: it would let services end runs, contradicting the sink's documented purpose. The weekly run has no sink either.

### D5. Removal detection runs after the forward walk, guarded by when the read started

- `var readStartedAt = DateTimeOffset.UtcNow;` is captured immediately before `GetFullUserAnimeListAsync`.
- After the forward walk, each `local` in `localEntries.Values` gets a `RemovedOnMal` diff entry when all of these hold:
  - `!seenAnimeIds.Contains(local.AnimeId)`
  - `!local.PendingSync`. This mirrors the existing guard and also covers held entries, which are always `PendingSync`.
  - `!pendingDeletionAnimeIds.Contains(local.AnimeId)`. This is normally vacuous, since an in-app removal deletes the entry, but it mirrors the existing guard as asked.
  - `!(local.LastSyncedAt >= readStartedAt)`. An entry pushed while the list was being paged is newer than the list the run read. A null `LastSyncedAt` is eligible.
- The entry carries the **local** values (`Status`, `EpisodesWatched`, `MyScore`, `StartedAt`, `CompletedAt`, `RewatchCount`), so the review can show what would be removed. This reuses `ToDiffEntry` with the local entry in place of the remote one.
- `removedOnMal++` for each one.

**Why a timestamp guard:** reading a long list takes seconds to tens of seconds. An anime added here whose debounced push lands in that window has `PendingSync = false` and is absent from the list already read. Without the guard, it would be proposed for removal. The existing `Added`/`Updated` walk has a milder form of the same race; this change leaves that alone.

**Alternative considered:** re-read MAL's single-entry status for each candidate before proposing it. Rejected: it costs one MAL call per candidate against the shared pacer, for a race the timestamp already closes.

### D6. Accepting branches on change type; a removal only deletes

`AcceptPendingDiffAsync` loops as today, and for `ChangeType == RemovedOnMal`:
- no local entry: nothing to do;
- `local.PendingSync`: skip, the same guard as today;
- `local.LastSyncedAt > diff.ComputedAt`: skip. It was pushed after the diff was computed, so MAL has it now;
- otherwise: `db.UserAnimeEntries.Remove(local)`. No `PendingEntryDeletion`, no push, no `ActivityLog`.

`Added`/`Updated` keep the current create-or-update body unchanged, including creating an entry for an `Updated` whose local row has since gone. The whole diff is still removed and saved once.

Cancel is unchanged: it drops the diff and nothing else.

### D7. Held changes: an unrecognized status is thrown as an unreadable read, inside the existing try blocks

`HeldChangeService` gains a private `ReadRemoteStatusAsync(int animeId, CancellationToken ct)`:
- it awaits `malClient.GetMyListStatusAsync`;
- if the status is not null and `!HasRecognizedStatus()`, it throws `InvalidDataException($"MyAnimeList gave list status '{raw}' for anime {animeId}, which this app doesn't recognize.")`;
- otherwise it returns the status.

`TryGetRemoteStatusAsync`, `DeclineEntryAsync` and `DeclineRemovalAsync` call it in place of the client, inside the `try` blocks they already have. Their existing behaviour then applies unchanged:
- **the review** logs a warning with the exception, returns `(null, true)`, shows the item as `RemoteUnavailable` and never self-clears it;
- **a decline** logs a warning and returns `Failed("Could not read MyAnimeList's current value.")`, so decline-all counts the item as still held.

Accepting such an item still works: it pushes the local, recognized values.

**Alternative considered:** a non-throwing check after each of the three reads, each with its own log line and return. Rejected: three copies of the check, log and outcome could drift apart. The throw keeps "unrecognized = unreadable" in one place, which is what the spec says.

### D8. Import: skip before either branch; retries unchanged

In `InitialImportService.RunAsync`'s loop, before the `existingAnimeIds` branch:

```
if (!edge.ListStatus.HasRecognizedStatus()) { log warning; skipped++; continue; }
```

This covers both `:101` and `:134`, and avoids the wasted details fetch.

The end of the run becomes:
- `failed == 0 && skipped == 0`: `Complete()`, as today;
- otherwise: `Fail(...)` with the sentences that apply, joined by a space: `"{failed} of {total} anime couldn't be fetched."` and `JobFailure.UnrecognizedStatuses(skipped, total)`.

`ListImportProgress.Fail` records `LastReadFailure`, so a run that only skipped anime starts the existing retry sequence (1, 5, 15, 60, 60 minutes). This is kept on purpose. See Risks.

### D9. Frontend: one more change type and a label

- `api/types.ts`: `ReconciliationDiffChangeType = 'Added' | 'Updated' | 'RemovedOnMal'`.
- `pages/SettingsPage.tsx`: the row detail's `entry.changeType === 'Added' ? 'New entry' : 'Updated'` becomes a small label lookup:
  - `Added` → "New entry"
  - `Updated` → "Updated"
  - `RemovedOnMal` → "Removed on MyAnimeList"
- A removal row reads `Removed on MyAnimeList — here: Watching, 5 ep, score 8`, so it is clear the values are the local ones being removed.
- The hint above the list ("Accepting applies exactly the differences listed below…") stays accurate and is unchanged.

The job and weekly-check failure text needs no frontend change, since `progressWords` and `formatWeeklyCheck` already render the reason.

### D10. No migration

`ChangeType` is a `text` column written through `HasConversion<string>()`, and a removal reuses the existing value columns. No schema or model-snapshot change is needed.

## Risks / Trade-offs

- **[Spurious removals from a bad list read]** A short page (R3), or re-authorizing as a different MAL account, would propose removing entries that are really there.
  → Nothing is applied without review, and a diff with unexpected removals can be cancelled.
  → Even if it is accepted, nothing reaches MAL, and the import on the next start re-adds any anime MAL lists that this device lacks. The loss is temporary and local.
- **[The import retries when it only skipped anime]** Retrying can't fix an unrecognized status that MAL still reports.
  → The cost is at most five extra list reads over about 2.5 hours, and none after that until the next start.
  → A retry does pick up the anime if I change its status on MAL's site in the meantime.
  → Changing the retry rules would touch `ListImportProgress`'s gate semantics, which the file import also reads. Not worth it for a near-unreachable case.
- **[A manual reconcile reads "Failed" though it stored a diff]**
  → The reason says anime were *left out*, not that the run broke, and the diff still loads below it.
- **["Failed after 595/595 processed"]** A partial airing refresh or bulk build shows a full bar and a failure.
  → This is the same shape sync now and the import already use. The reason says how many didn't succeed.
- **[The weekly check fails every week]** It keeps failing until the status is added to the code.
  → Intended. That is the signal the user asked for.
- **[`InvalidDataException` as control flow]**
  → It is confined to one private helper in `HeldChangeService`, and caught by blocks that already exist for read failures.
- **[Rolling back with a removal diff pending]** Older code can't turn `"RemovedOnMal"` back into the enum, so reading the pending diff would throw.
  → Cancel any pending diff before rolling back, or delete its `PendingReconciliationDiffs` row.

## Migration Plan

- Ship the backend and frontend together, as always. No data migration.
- Rollback: revert the commit, after cancelling any pending reconciliation diff (see Risks).

## Open Questions

None.
