## 1. Data model and migration

- [x] 1.1 Add `DateTimeOffset? HeldForReviewAt` to `Models/UserAnimeEntry.cs` with a comment stating that non-null means "was already waiting to be pushed when the process started, and is awaiting my decision" (design D1).
- [x] 1.2 Add the same `DateTimeOffset? HeldForReviewAt` to `Models/PendingEntryDeletion.cs`, alongside its existing `RequestedAt` (design D7).
- [x] 1.3 Add the migration for both nullable columns — scaffolded with `dotnet ef migrations add HoldStartupPendingSyncForReview` inside the `mcr.microsoft.com/dotnet/sdk:10.0` image (the local SDK is 9.0), or hand-written with its Designer plus a snapshot edit following `20260905120000_RetireUpdatesOutsideMyList`. No backfill: the first startup after deploy stamps whatever is pending, which is the correct set by definition.

## 2. The startup hold

- [x] 2.1 Add `Services/Sync/StartupPendingSyncHold.cs` (+ interface) exposing one `ApplyAsync(CancellationToken)` that stamps `HeldForReviewAt = now` on every `UserAnimeEntry` with `PendingSync && HeldForReviewAt == null` and every `PendingEntryDeletion` with `HeldForReviewAt == null`, and logs how many of each it held.
- [x] 2.2 Call it from `Program.cs`'s existing startup scope, immediately after `db.Database.Migrate()` and before `app.Run()` — the only point with a guarantee that no hosted service, controller, or debounce timer can push first (design D2). Register whatever the service needs in DI above `var app = builder.Build()`.
- [x] 2.3 Verify idempotence by construction: the `HeldForReviewAt == null` guard means a second start neither re-dates an existing hold nor releases one.

## 3. Excluding held items from every push

- [x] 3.1 In `Services/Sync/EntryPushService.cs`, make `PushIfPendingAsync` return `false` without calling MAL when the entry is held, and `PushPendingDeletionAsync` do the same for a held removal — the guard goes at the MAL call so every caller inherits it (design D3).
- [x] 3.2 Filter held rows out of both queries in `DrainPendingAsync` (`.Where(e => e.PendingSync && e.HeldForReviewAt == null)` and the equivalent on `PendingEntryDeletions`), so the retry job and "sync now" never even enumerate them.
- [x] 3.3 Update the class/interface comments on `IEntryPushService` and `PendingSyncRetryBackgroundService` to say that the startup drain is now safe because the hold is stamped before the host starts.
- [x] 3.4 In `Services/Entries/UserAnimeEntryEditService.cs`, clear `HeldForReviewAt` in the existing `if (triggersSync)` branch that sets `PendingSync` — a deliberate edit releases a hold (design D15). Leave `AiringWatchStatusService` untouched: an automatic transition must not release one.

## 4. Reading MyAnimeList's current value for one anime

- [x] 4.1 Add `Task<MalListStatus?> GetMyListStatusAsync(int animeId, CancellationToken ct = default)` to `Services/Mal/IMalClient.cs`, documented as returning null when MyAnimeList has no list entry for the anime.
- [x] 4.2 Implement it in `MalClient` as `GET anime/{id}?fields=my_list_status` with `MalAuthMode.Bearer` (design D9 — `GetAnimeDetailsAsync` is client-id authenticated, so MAL returns no `my_list_status` on it), reading `MyListStatus` off the returned `MalAnimeNode` and treating a 404 as null rather than as a failure.
- [x] 4.3 Lift `ReconciliationService`'s inline local-vs-remote match test (`:83-88` — the six pushed fields with the Rewatching/`watching` carve-out) into one shared helper beside `MalStatusResolution`, and call it from `ReconciliationService` in place of the inline version. Its existing tests must still pass unchanged; this is a pure extraction (design D8a).

## 5. The held-change service

- [x] 5.1 Add `Services/Sync/HeldChangeService.cs` (+ `IHeldChangeService`) and register it scoped in `Program.cs` beside `IReconciliationService`.
- [x] 5.2 Implement `GetHeldAsync`: every held entry and every held removal, each carrying anime id, title and picture, its kind (edit or removal), when it was held, and the values that would be pushed.
- [x] 5.3 Add the unsent-change summary to that result — `ActivityLog` rows for the anime with `Source = BetterMal` and `Timestamp > LastSyncedAt` (all `BetterMal` rows when `LastSyncedAt` is null), newest first, capped at a few per item with a count of the remainder (design D4).
- [x] 5.4 Enrich each item with MyAnimeList's current values via `GetMyListStatusAsync`, best-effort: a failed read marks that item's remote side unavailable instead of failing the call (design D8). One call per held anime only — never the whole list.
- [x] 5.5 Using that same read, clear any item MyAnimeList already agrees with before building the result (design D8a): for an entry, the shared comparer from 4.3 returning true means clear `PendingSync`/`HeldForReviewAt`, stamp `LastSyncedAt`, record no activity, and omit it from the response; for a removal, MAL no longer listing the anime means drop the `PendingEntryDeletion` and omit it. An item whose read failed clears nothing. Nothing is ever pushed by this path.
- [x] 5.6 Implement `AcceptAsync(animeId)`: clear `HeldForReviewAt`, save, then push through `PushIfPendingAsync` (entry) or `PushPendingDeletionAsync` (removal). A failed push leaves the item pending and unheld, for the retry job (design D5).
- [x] 5.7 Implement `DeclineAsync(animeId)` for a held **entry** (design D6): read MAL's current list status; on a hit, apply it through `MalStatusResolution.ResolveAgainstLocal` plus the other five fields, clear `PendingSync`/`HeldForReviewAt`, set `LastSyncedAt`, and record via `EntryActivityRecorder.Diff`; on a miss, delete the local entry, record one `Removed`, and queue **no** `PendingEntryDeletion`; on a read failure, change nothing and report it.
- [x] 5.8 Implement `DeclineAsync(animeId)` for a held **removal** (design D7): drop the `PendingEntryDeletion`; where MAL still lists the anime, recreate the local entry from its list status and record an addition; where it does not, leave the anime absent.
- [x] 5.9 Implement `AcceptAllAsync` / `DeclineAllAsync` as a loop over the held set applying the per-item rule, continuing past a per-item failure and returning how many succeeded and how many were left held.

## 6. Recording a declined held change

- [x] 6.1 Append one `ActivityChangeSource` value for a declined held change to `Models/ActivityLog.cs`, keeping the existing values in place (the enum is append-only) and commenting it as such. (Implemented alongside 5.7/5.8, which require it to compile — see report.)
- [x] 6.2 Use it as the `source` argument in every `EntryActivityRecorder` call made by `DeclineAsync` (tasks 5.7, 5.8). Accepting records nothing — nothing about the stored entry changes.

## 7. API surface

- [x] 7.1 Change `Data/Repositories/UserAnimeEntryRepository.GetSyncStatusAsync` (and its interface) to return `(PendingCount, HeldCount, LastSyncedAt)`, where `PendingCount` counts only `PendingSync && HeldForReviewAt == null` (design D14).
- [x] 7.2 Update `SyncController.GetStatus` to return `heldCount` alongside `pendingCount` and `lastSyncedAt`.
- [x] 7.3 Add `GET /api/sync/held` to `SyncController`, returning the held set as a list (empty rather than 204, since the page uses the count), with an XML doc comment in the style of the reconciliation endpoints beside it.
- [x] 7.4 Add `POST /api/sync/held/{animeId}/accept` and `POST /api/sync/held/{animeId}/decline`, returning 404 for an anime that is not held and a failure payload the page can show when a decision could not be applied.
- [x] 7.5 Add `POST /api/sync/held/accept` and `POST /api/sync/held/decline` for the whole set, returning how many succeeded and how many stayed held.

## 8. Settings page

- [x] 8.1 Add the held-change DTO types and the extended `SyncStatusDto` (`heldCount`) to `frontend/src/api/types.ts`, and append the new origin to the `ActivityChangeSource` union.
- [x] 8.2 Add `getHeldChanges`, `acceptHeldChange`, `declineHeldChange`, `acceptAllHeldChanges`, `declineAllHeldChanges` to `frontend/src/api/client.ts`, following the shapes of the reconciliation-diff calls.
- [x] 8.3 Load the held set in `SettingsPage`'s `load()` alongside the other status calls, and split the Sync group's status readout into pending/retrying and held-for-review figures — taking the held figure from the held-list payload, not from `status.heldCount`, so it cannot disagree with the rows below it (design D14).
- [x] 8.4 Render a held-changes subsection in the Sync group above the reconciliation diff, absent entirely when nothing is held: one row per item with `RowPicture`, the English-first display title, its kind, its unsent changes with their timestamps, the values that would be sent, and MyAnimeList's current values (or a note that they could not be read).
- [x] 8.5 Give each row its own Accept and Decline buttons plus section-level Accept all / Decline all, disabling a row's actions while its decision is in flight and showing a per-section error on failure, mirroring the diff section's `reviewing`/`diffError` handling.
- [x] 8.6 Write the explanations the `settings-page` spec now requires: what accepting sends, what declining takes instead, the destructive wording for an item MyAnimeList holds no entry for, and the sync-now line saying held changes are not among what it pushes.
- [x] 8.7 Add the styles to `SettingsPage.css`, reusing the `settings-subsection` / `settings-diff-row` patterns so the section reads as the review pattern already on the page.

## 9. Tests

- [x] 9.1 `StartupPendingSyncHold` tests: a pending entry and a queued removal are stamped; an already-held row is neither re-dated nor released; a non-pending entry is untouched.
- [x] 9.2 `EntryPushService` tests: a held entry is not pushed by `PushIfPendingAsync`, a held removal is not pushed by `PushPendingDeletionAsync`, and `DrainPendingAsync` pushes the unheld items only while leaving the held ones pending.
- [x] 9.3 `UserAnimeEntryEditService` test: editing a held anime clears its hold; and an `AiringWatchStatusService` test that an automatic reopen or complete on a held entry leaves the hold in place.
- [x] 9.4 `HeldChangeService` self-clearing tests: an entry matching MAL on all six fields clears and is omitted; a local Rewatching against MAL's `watching` counts as matching; an entry whose episode count is higher than MAL's stays held; a removal whose anime MAL no longer lists is dropped; a failed MAL read clears nothing; and no activity is recorded by any of it.
- [x] 9.5 `HeldChangeService` accept tests: the hold is cleared and the push attempted; a failed push leaves the item pending and unheld.
- [x] 9.6 `HeldChangeService` decline tests, one per branch: MAL's values applied; a local Rewatching not demoted by MAL's `watching`; MAL-absent deletes the entry and queues no removal; a read failure changes nothing; a held removal declined restores the entry, and one declined for an anime MAL no longer lists just drops the queued removal.
- [x] 9.7 Activity tests: a decline records one row per genuinely moved field under the new origin, records nothing when it moves nothing, records a removal in the MAL-absent branch, and an accept records nothing.
- [x] 9.8 `GetSyncStatusAsync` test: held entries are counted in `HeldCount` and excluded from `PendingCount`.

## 10. Verification

- [x] 10.1 Compile-check the backend against the .NET 10 SDK image (copy `backend/` to `/private/tmp/...` first — Docker cannot bind-mount `~/Documents`) and run the test project.
- [x] 10.2 Build the frontend with Node 22 (`PATH="$HOME/.nvm/versions/node/v22.23.1/bin:$PATH" npm run build`).
- [ ] 10.3 Exercise the flow against the dev stack: mark an entry pending with MAL unreachable, restart the backend, confirm nothing was pushed and the settings page lists the item; accept one and confirm it reaches MAL; decline another and confirm the local entry takes MAL's value and the change appears in Latest updates marked as coming from MyAnimeList.
- [ ] 10.4 Confirm the section disappears once the last held item is decided, and that a normal in-session edit still pushes ~8 seconds later with no review.
- [ ] 10.5 Exercise the self-clearing case end to end: let a push reach MAL, kill the backend before the flag clears, restart, and confirm the entry is held at startup but never shown — cleared on the first review read, with nothing pushed and nothing in Latest updates.
