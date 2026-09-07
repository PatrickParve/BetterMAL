## Context

Write-sync today is a single durable flag with three drains. `pending_sync` is set by four sites — my own edits (`UserAnimeEntryEditService:91`), the automatic reopen and automatic complete in `AiringWatchStatusService` (`:65`, `:107`), and the re-add race in `EntryPushService:77` — and cleared only by a successful push. Three things drain it: the 8-second per-anime debounce (`DebouncedEntrySyncScheduler`), the 2-minute retry loop (`PendingSyncRetryBackgroundService`), and manual "sync now" (`SyncController.SyncNow`). All three funnel through `IEntryPushService`, which is the single place a MAL update is built and the flag is cleared. `DrainPendingAsync` also pushes every queued `PendingEntryDeletion` in the same pass.

The retry loop calls `DrainPendingAsync` at the top of its `while`, *before* its first `Task.Delay`, so the first thing a freshly started process does is push every row left pending by the previous one. Nothing on `UserAnimeEntry` records when a row became pending — only `PendingSync` (bool) and `LastSyncedAt` — so no query can currently tell a three-week-old stale edit from one that failed ten seconds ago.

The important asymmetry: an in-session failure is safe to retry forever (the value is fresh and I am sitting right there), whereas a value that survived a restart is only still pending because its push failed *and* the app closed before recovery — which makes it arbitrarily old and quite possibly contradicted by another device.

The app already has a held-for-review precedent to follow rather than invent: `PendingReconciliationDiff` computes differences, holds them, and surfaces them on the settings page with accept and cancel actions.

## Goals / Non-Goals

**Goals:**

- Nothing that was already waiting to be pushed when the process started reaches MyAnimeList without an explicit per-item decision.
- Everything else about pushing is untouched: same 8-second debounce, same 2-minute retry, same automatic recovery from an in-session failure.
- The hold survives a crash, a second restart, and a review left unfinished — an item stays held until it is actually decided.
- The review carries enough to judge each item on its own: which anime, what the unsent change was, when it was made, and what MyAnimeList currently holds for it.
- The decision is per item. Ten held entries are ten independent judgements, not one all-or-nothing gate.

**Non-Goals:**

- Changing the debounce window, the retry interval, or the reconciliation flow.
- Holding anything that became pending during the current session, however long that session runs or however many times its push has failed.
- A general "outbox with review" mechanism. This holds exactly what crossed a process boundary.
- Merging a stale local value with MyAnimeList's field by field. Each held item resolves one way or the other, whole.

## Decisions

### D1 — The hold is a durable per-row marker, stamped once at startup

Add a nullable `HeldForReviewAt` (`DateTimeOffset?`) to `UserAnimeEntry` and to `PendingEntryDeletion`. Non-null means held. It is stamped once per process start, for every row that is pending at that moment, and cleared only by accept, decline, or a fresh deliberate edit.

*Alternative considered — a `PendingSince` column compared against an in-memory process-start timestamp* ("held ⟺ `PendingSince < SessionStart`"). It needs no startup write, and it hands the review UI a timestamp for free. It was rejected on three counts: the predicate depends on a wall clock, so a backward clock adjustment silently changes which rows are held; every query for held items becomes a comparison against in-memory state rather than a column test; and the "a fresh edit releases the hold" rule has to be implemented as a restamp of `PendingSince`, which then no longer means what its name says. A marker is a fact about the row, not a computation over two clocks.

*Alternative considered — an in-memory set of anime ids captured at startup.* Rejected: it does not survive the crash it exists to protect against, and a review interrupted by a restart would lose its own subject.

The stamp is idempotent — `WHERE pending_sync AND held_for_review_at IS NULL` — so a second startup neither re-dates an existing hold nor loses one.

### D2 — Stamped in `Program.cs`'s startup scope, immediately after `Database.Migrate()`

The stamp must be committed before anything can push. `Program.cs` already opens a synchronous startup scope for `db.Database.Migrate()` and runs it before `app.Run()` — i.e. before any hosted service starts, any controller can be reached, and any debounce timer can exist. That is the only place in this application with that guarantee, and it costs one `UPDATE`.

*Alternative considered — a hosted service registered before `PendingSyncRetryBackgroundService`.* Rejected: `BackgroundService.StartAsync` returns at its first `await`, so registration order does not guarantee the stamp completes before the retry service's first drain. Making it safe would need a gate the retry service awaits, which is more machinery for a weaker guarantee.

Because the stamp runs on the first startup after deploy, rows already pending when the column is added are held by that same pass. The migration therefore needs no backfill.

### D3 — The hold is enforced in the push, not only in the drain query

`DrainPendingAsync` filters held rows out of both its pending-entry and pending-deletion queries, *and* `PushIfPendingAsync` / `PushPendingDeletionAsync` return `false` for a held item without calling MAL. Filtering the drain alone would not be enough: `DebouncedEntrySyncScheduler` calls `PushIfPendingAsync` directly for a single anime, and so does the re-add path in `EntryPushService`. Putting the guard where the MAL call is means every present and future caller inherits it — the same reasoning that already made `IEntryPushService` the one place `pending_sync` is cleared.

This matters concretely: `AiringWatchStatusService` can flip a held entry (an automatic reopen or complete) and calls `ScheduleSync`, which fires a debounced push 8 seconds later against an entry that is still held. The guard in `PushIfPendingAsync` is what stops that push.

### D4 — "What changed and when" is read from the activity log, not from a new column

Every path that sets `pending_sync` also writes `ActivityLog` rows: `UserAnimeEntryEditService` only sets the flag when `isNew || changes.Count > 0`, and both `AiringWatchStatusService` transitions add a row beside the flag. So the log already holds the unsent changes at finer granularity than any entry-level column could — one row per field, with its own timestamp, its human-readable detail, and its previous episode count.

A held item's unsent changes are the rows for that anime with `Source = BetterMal` and `Timestamp > LastSyncedAt` (all `BetterMal` rows when `LastSyncedAt` is null — an entry that has never synced at all). Both guards are deliberate: `LastSyncedAt` is the last moment local and remote agreed, and the `Source` filter keeps a MAL-origin row written at exactly that instant (reconciliation's accept stamps `LastSyncedAt = now` and logs at `now`) from reading as an unsent local change.

"When it was made" is the newest of those rows. The list is capped at the most recent few per item, with a count of the remainder, so an entry that accumulated many unsent edits does not turn one review row into a wall.

*Alternative considered — a `PendingSince` column.* It answers "when did this become pending" and nothing else; the log answers what changed, in what order, and when, and already exists.

### D5 — Accept releases the hold and pushes; a failed accept retries automatically

Accepting an entry clears `HeldForReviewAt`, leaves `PendingSync` set, and pushes immediately through the ordinary `PushIfPendingAsync`. If MAL is unreachable at that moment the item simply stays pending with no hold — which puts it in exactly the in-session-failure state the retry loop already handles correctly. Having explicitly said yes, I should not be asked again ten seconds later.

If the process is closed again before that accepted push lands, the next startup stamps it held again. That is correct rather than annoying: it crossed a process boundary again, and it is stale again for exactly the original reason.

### D6 — Decline discards the local change and adopts MyAnimeList's current value

Declining is the strong reading, not "clear the flag and let them diverge quietly". For a held **entry**:

1. Read MyAnimeList's current `my_list_status` for that anime (D9).
2. If MyAnimeList has one: overwrite the local status, episodes watched, score, start date, finish date, and rewatch count with it — resolving the status through `MalStatusResolution.ResolveAgainstLocal`, exactly as reconciliation does, so a local `Rewatching` is not demoted by MyAnimeList's `watching`. Clear `PendingSync` and `HeldForReviewAt`, set `LastSyncedAt = now`, and record the field-by-field diff via `EntryActivityRecorder` under the new origin (D10).
3. If MyAnimeList has **no** entry for the anime, the held change is a local addition that never reached MyAnimeList. Adopting "not on my list" means removing the local entry. It is deleted, one `Removed` record is written under the same origin, and — critically — **no `PendingEntryDeletion` is queued**, because MyAnimeList is already in the desired state.
4. If the read fails, nothing changes: the item stays held, still pending, and the failure is reported. A decline must never be half-applied on a guess about the remote value.

Because step 3 destroys a local entry, the review row states that outcome *before* the button is pressed, which is only possible because the list endpoint has already fetched the remote value (D8). Declining a held entry whose anime MyAnimeList does list is non-destructive by construction — it lands on values MyAnimeList already holds.

### D7 — A held **removal** is the mirror image

Accept pushes the `DELETE` through the existing `PushPendingDeletionAsync`, clearing the hold first, with the same failed-accept behaviour as D5.

Decline drops the queued `PendingEntryDeletion` and, if MyAnimeList still lists the anime, restores the local entry from MyAnimeList's current list status (recorded as an addition under the new origin). If MyAnimeList no longer lists it — the removal already happened on another device — the queued removal is simply dropped and the anime stays absent locally, since local and remote already agree.

Holding removals is a deliberate widening of the literal request. The justification: `DrainPendingAsync` pushes queued deletions in the same pass as pending edits, a queued deletion is an entry waiting to be pushed on any reading, and a stale `DELETE` destroys a MyAnimeList entry outright rather than merely writing an old number into one.

### D8 — The review list fetches MyAnimeList's current values, best-effort

The list endpoint enriches each held item with MyAnimeList's current values for that anime. This is what makes the review a judgement rather than a guess — the entire reason an item is held is that another device may have moved on, and the local value alone cannot show that. It also lets a destructive decline (D6.3) be labelled honestly before it is pressed.

The fetch is **per held anime, at review time** — one `GET anime/{id}` each, when `GET /api/sync/held` is served. Nothing else about my list is read: the whole-list pull is reconciliation's job, and a held set is one to three anime, not a library. It is deliberately not done during the startup stamp either: that step must stay fast and DB-only, and MyAnimeList being unreachable is often the very reason something is pending.

It costs one MAL request per held item. Held sets are small by construction (an item is only held because a push failed *and* the app was closed mid-recovery), the requests go through `MalAuthPacingHandler`'s existing pacing, and the fetch is best-effort: an item whose read fails renders with its local side and a note that the MyAnimeList side is unavailable, and its decline still re-reads at decision time (D6.4) rather than trusting anything cached from the render.

Because the read has happened by the time the list is built, it also does the work of D8a for free.

### D8a — An item MyAnimeList already agrees with clears itself, and nothing else does

When the review reads MyAnimeList's current value for a held item and it **already matches** what would be pushed, there is nothing to send and nothing to decide. The item SHALL clear itself — `PendingSync` and `HeldForReviewAt` cleared, `LastSyncedAt` stamped — and never reach the review at all. This is common rather than exotic: the usual way an entry ends up pending across a restart is that its push *did* reach MyAnimeList and the process died before the flag was cleared. Asking me to confirm a write that would change nothing is noise, and worse, noise that trains me to click Accept without reading.

"Matches" is the six pushed fields — status, episodes watched, score, start date, finish date, rewatch count — compared by the rewatch-preserving rule `ReconciliationService` already applies: a local **Rewatching** against a remote `watching` counts as agreeing, because that is exactly how the entry was pushed. That comparison currently lives inline in `ReconciliationService` (`:83-88`); it is lifted into one shared helper both paths call, on the same reasoning `MalStatusResolution` was extracted — a read-back rule restated per path is a rule that eventually disagrees with itself.

The auto-clear records **no** activity: no stored field moved, only sync bookkeeping, which `activity-recording` already says records nothing.

**Anything else stays held.** In particular, a held item whose episode count is *higher* than MyAnimeList's is still shown — it is not evidence the push landed, and it is exactly the case where a stale local value would overwrite a newer remote one. It is tempting to auto-accept when local is strictly ahead on episodes ("surely I watched more since"), and that temptation is the whole bug this change exists to fix: a local 12 against a remote 4 reads identically whether I watched nine more episodes yesterday or MyAnimeList was deliberately corrected downward on another device three weeks ago. The only automatic resolution that is safe is the one that sends nothing and changes nothing.

A held **removal** has the same no-op case: if MyAnimeList no longer lists the anime, the queued removal's desired end state already holds, so the queued removal is dropped silently — the same tolerance `DeleteMyListStatusAsync` already gives a 404, applied one step earlier.

If MyAnimeList cannot be read, nothing clears: every item stays held and the next read tries again.

*Alternative considered — auto-clear during the startup stamp, or in a background pass just after it,* so the held count is already correct before Settings is ever opened. Rejected for now: it puts N network calls on the boot path (or adds a fourth background service) to fix a count that is only ever read on one page, which D14 handles by sourcing that count from the same payload as the rows.

### D9 — A new bearer-authenticated single-anime `my_list_status` read on `IMalClient`

`GetAnimeDetailsAsync` cannot serve this: it sends `MalAuthMode.ClientId`, and MyAnimeList only returns `my_list_status` on a bearer-authenticated request. Add `GetMyListStatusAsync(int animeId)` issuing `GET anime/{id}?fields=my_list_status` with `MalAuthMode.Bearer`, returning `MalListStatus?` — null when MyAnimeList has no list entry for the anime, mirroring the "absent is data, not failure" tolerance `DeleteMyListStatusAsync` already gives a 404.

*Alternative considered — reuse `GetFullUserAnimeListAsync`.* One call-set instead of N, but it pages the entire list to answer a question about one to three anime, and it is the expensive call reconciliation is built around. Wrong tool at this granularity.

### D10 — Declining records under an origin of its own

Declining applies MyAnimeList's values to a stored entry, so `activity-recording`'s "every path that changes my list records what it changed" applies. Its spec also requires the origin to stay answerable per sync path, so this does not reuse `MalReconciliation` — that origin means an accepted reconciliation diff, and conflating the two would make the log unable to say which surface applied a change. One value is appended to `ActivityChangeSource` (append-only, so existing rows are unaffected) and mapped through to the frontend's origin union.

Note the wording overlap to keep straight: `activity-recording` says a *declined reconciliation diff* records nothing, and that stays true — declining a diff applies nothing. Declining a **held change** applies MyAnimeList's current value, which is a real change to a stored entry and is recorded as one.

### D11 — Manual "sync now" does not push held items

`POST /api/sync/now` calls `DrainPendingAsync`, so it inherits the D3 filter and pushes only unheld pending items. Its settings-page explanation says so. A control labelled "pushes your own unsent edits right away" must not be the back door that bypasses a review the user has not done — and the honest fix is the label plus the filter, not a second confirmation step on a button that behaves correctly for everything else.

### D12 — Reconciliation is untouched

`ReconciliationService` already skips entries with `PendingSync` when computing a diff (`:71`) and when applying one (`:157`). A held entry is still `PendingSync = true`, so it stays skipped with no code change, which is exactly what we want: one anime is never awaiting my decision on two different review surfaces at once. Once it is accepted or declined the entry stops being pending, and the next reconciliation run treats it normally.

### D13 — API shape

- `GET /api/sync/held` — the held set (entries and removals together, each tagged with its kind), or an empty list. Empty rather than 204, since the settings page also uses the count.
- `POST /api/sync/held/{animeId}/accept`, `POST /api/sync/held/{animeId}/decline` — the per-item decisions.
- `POST /api/sync/held/accept`, `POST /api/sync/held/decline` — the whole set, applying the same per-item rules in turn and reporting how many succeeded. A per-item failure inside a bulk run leaves that item held rather than aborting the rest.

These live on the existing `SyncController` beside the reconciliation review endpoints they parallel.

### D14 — The status readout separates pending from held

`GetSyncStatusAsync` returns `(PendingCount, HeldCount, LastSyncedAt)`, where `PendingCount` counts only unheld pending entries. "Pending / retrying: 3" today would otherwise mean three things in flight *or* three things waiting on me, which are opposite states — one resolves itself and the other never will.

The settings page takes its **held** figure from the held-list payload rather than from this status call. It loads both anyway, and only the list has been through D8a — the status call counts rows in the database, so it would say "2 held" beside a review listing one until something happened to re-read it. `HeldCount` stays on the status endpoint for any caller that wants the cheap DB figure without the MAL reads.

### D15 — A fresh edit releases a hold; an automatic transition does not

`UserAnimeEntryEditService.UpdateEntryAsync` clears `HeldForReviewAt` wherever it sets `PendingSync` (its existing `triggersSync` branch). A deliberate edit is current by definition and supersedes whatever stale value was held, so it pushes on the ordinary debounce with no review.

`AiringWatchStatusService`'s automatic reopen and complete do **not** clear it. They are inferences from an airing schedule, not statements of intent, and an entry held because its value may be weeks out of date is not made current by the app deciding something about it on its own. The entry stays held; its local values simply move, and the review renders them as they stand when it is read. Re-adding an anime with a held removal already cancels that removal through the existing `PendingEntryDeletions.Remove` in `UpdateEntryAsync`.

## Risks / Trade-offs

- **A destructive decline.** Declining a held entry that MyAnimeList does not list deletes it locally (D6.3) → the row states that outcome before the button is pressed; the deletion is recorded in the activity log like any other change; and the fetch behind the label is re-read at decision time, so the label cannot be stale.
- **The review is never done.** A held item blocks itself and its anime's reconciliation indefinitely → this is already true of any pending entry today, with the difference that a held one is visible on the settings page with actions on it rather than silently skipped.
- **A long-lived process never holds anything.** A server left running for weeks retries an in-session failure forever with no review → that is the rule as specified: the process boundary *is* the staleness signal. If this ever proves too coarse, an age threshold can be added to the same stamp with no change to anything downstream of it.
- **N MAL reads on settings-page load.** Held sets are tiny, the reads are paced by the existing handler, and each failure degrades to a local-only row rather than failing the page (D8).
- **The stamp is the whole guarantee.** If it did not run, stale rows would drain as they do today → it is one statement in the same startup block as `Database.Migrate()`, which already must succeed for the app to serve at all; there is no path that starts the request pipeline without it.
- **`xmin` concurrency.** `UserAnimeEntry` maps `xmin` as a concurrency token, so the startup `UPDATE` bumps it → it runs before any request or hosted service can hold a tracked entity, so there is nothing to conflict with.
- **A self-clearing item is a write on a read path.** `GET /api/sync/held` clears flags for items MyAnimeList already agrees with → it writes only sync bookkeeping, never a user-visible field, and the same call already had to read MyAnimeList to render at all; the alternative is showing decisions that provably change nothing.
- **One more "pending" concept.** The app now has pending-sync, pending-removal, pending-reconciliation-diff, and held → mitigated by putting held items in the same Sync group with the same accept/decline shape as the reconciliation diff, so it reads as the pattern already there rather than a fourth vocabulary.

## Migration Plan

1. Additive EF migration: nullable `held_for_review_at` on `user_anime_entries` and `pending_entry_deletions`. No backfill — the first startup after deploy stamps whatever is pending, which is the correct set by definition.
2. Appended `ActivityChangeSource` value; existing rows keep their stored values (the enum is append-only and persisted by name).
3. Rollback is the reverse migration plus a redeploy; the only state the change introduces is two nullable columns, and dropping them returns the app to draining everything pending at startup.

## Open Questions

None blocking. Two decisions were taken rather than deferred, and are the ones to overturn first if either reads wrong in use: holding queued **removals** as well as edits (D7), and declining a MyAnimeList-absent entry by **deleting it locally** (D6.3).
