## Why

A local edit that fails to reach MyAnimeList stays `pending_sync = true` in the database, and the retry service drains every pending entry the moment the app starts — before the first `Task.Delay`, then again every 2 minutes. So an entry can only still be pending across a restart because its push failed *and* the app was closed before it recovered: by the time that device is opened again the local value may be weeks old, and another device may have moved the same entry on. Today that stale value is pushed to MyAnimeList automatically, silently overwriting whatever the newer truth is, with nothing asked and nothing shown.

The same is true of a queued removal: `DrainPendingAsync` pushes pending `DELETE`s in the same pass, and a stale delete is the most destructive push of the set.

## What Changes

- **Entries pending at startup are held for review instead of pushed.** A durable per-row hold marker is stamped once during application startup — in `Program.cs`'s existing synchronous startup scope, alongside `Database.Migrate()`, so it is written before any hosted service, controller, or debounce timer can push anything. Held items are excluded from every push path: the startup retry drain, the 2-minute retry pass, the debounce timer, and manual "sync now".
- **Everything else about pushing stays automatic.** An edit made in-session marks the entry pending, pushes 8 seconds later, and — if that push fails — keeps being retried by the background service until it lands. No review, no hold, no change to the debounce window.
- **Pending removals are held on the same rule.** A `PendingEntryDeletion` still queued at startup is held, surfaced, and accepted or declined like a held edit. *(This is a deliberate widening of the request's wording, on the grounds that a queued removal is an entry waiting to be pushed and is the more destructive of the two.)*
- **Held items are reviewed on the settings page**, inside the existing Sync group, as a new subsection that is absent entirely when nothing is held. Each row names the anime, states what the unsent change was and when it was made (read from the activity log, which already records every path that sets `pending_sync`), shows the values that would be pushed, and — best-effort — MyAnimeList's current values for that anime, so a stale local value can be judged against what the other device did.
- **Accept pushes; decline adopts MyAnimeList's value.** Accepting releases the hold and pushes immediately; if that push fails it stays pending and retries automatically like any in-session failure. Declining discards the unsent local change and takes MyAnimeList's current value for that anime, recorded in the activity log as coming from MyAnimeList. Both act per-entry, with accept-all / decline-all convenience actions over the whole held set.
- **A deliberate edit to a held anime releases its hold.** A fresh edit is current by definition, so it clears the hold and pushes on the normal debounce. An *automatic* status transition (`AiringWatchStatusService`'s reopen/complete) does not release a hold — it is not a statement of intent.
- **The sync status readout separates the two counts**: entries pending-and-retrying versus entries held for review, so "Pending / retrying: 3" no longer conflates something in flight with something waiting on me.
- **New MAL client capability:** a single-anime `my_list_status` read (bearer-authenticated), needed both to render the comparison and to apply a decline. `GetAnimeDetailsAsync` cannot serve this — it is client-id authenticated, so MyAnimeList returns no `my_list_status` on it.

## Capabilities

### New Capabilities

None. The behaviour belongs to the existing write-sync capability and its settings-page surface.

### Modified Capabilities

- `mal-write-sync`: entries and removals pending at startup are held for review rather than drained; held items are excluded from every automatic push path and from manual "sync now"; accept/decline semantics for a held item; a fresh edit releases a hold; reconciliation continues to skip held entries (they are still `pending_sync`) so one anime is never under review on two surfaces at once.
- `settings-page`: a held-changes review subsection in the Sync group, present only when something is held; its accept and decline controls join the set of sync controls required to state what they will do before being pressed; the sync status readout gains the held count.
- `activity-recording`: declining a held change applies MyAnimeList's current value to a stored entry, so it is a path that changes my list and must record what it changed, under an origin of its own distinct from the accepted-reconciliation-diff and corrective-re-sync origins.

## Impact

**Backend**

- `Models/UserAnimeEntry.cs`, `Models/PendingEntryDeletion.cs` — hold marker column on each; EF migration (additive, nullable, no backfill needed).
- `Services/Sync/EntryPushService.cs` — `PushIfPendingAsync` / `PushPendingDeletionAsync` refuse a held item; `DrainPendingAsync` filters held items out.
- `Services/Sync/PendingSyncRetryBackgroundService.cs` — unchanged in shape; its startup drain becomes safe because the hold is already stamped.
- `Program.cs` — hold-stamping step in the existing startup scope, immediately after `Database.Migrate()`.
- New `Services/Sync/HeldChangeService.cs` (+ interface) — list, accept, decline, accept-all, decline-all.
- `Services/Entries/UserAnimeEntryEditService.cs` — a fresh edit clears the hold.
- `Data/Repositories/UserAnimeEntryRepository.cs` — `GetSyncStatusAsync` returns the held count alongside the pending count.
- `Services/Mal/IMalClient.cs` + `MalClient.cs` — bearer-authenticated single-anime `my_list_status` read.
- `Models/ActivityLog.cs` — one appended `ActivityChangeSource` value for a declined-hold adoption.
- `Controllers/SyncController.cs` — `GET /api/sync/held`, `POST /api/sync/held/{animeId}/accept|decline`, `POST /api/sync/held/accept|decline`.

**Frontend**

- `api/client.ts`, `api/types.ts` — the held-changes endpoints, their DTOs, the extended sync status, and the new activity origin.
- `pages/SettingsPage.tsx`, `pages/SettingsPage.css` — the review subsection and the split status readout.

**Not affected**

- The 8-second debounce window, the 2-minute retry interval, `ReconciliationService`'s diff/accept/cancel flow, and the corrective re-sync all keep their current behaviour.
