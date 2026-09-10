## Context

The activity log (`ActivityLogs`) is written from two families of paths.

- **In-app changes.** `UserAnimeEntryEditService` (my edits and removals) and `AiringWatchStatusService` (automatic completion and reopening) build their rows inline and never set `Source`, so they carry the default `BetterMal`. `HeldChangeService`'s two decline paths write through `EntryActivityRecorder` as `MalHeldDecline`, and so does its inline removal row.
- **Sync paths.** `InitialImportService`, `ReconciliationService.AcceptPendingDiffAsync` and `ResyncService` write through `EntryActivityRecorder` as `MalStartupImport`, `MalReconciliation` and `MalResync`. The import also skips recording on a "baseline" run (`isBaselineRun`, meaning no row exists yet).

Where `Source` is read:
- `ActivityLogRepository.GetEpisodeProgressInRangeAsync` counts only `BetterMal` rows toward recaps.
- `HeldChangeService.GetUnsentChangesAsync` lists only `BetterMal` rows as "what my unsent change was".
- `ProfileService` passes it on to `ActivityFeedItemDto`, where the frontend's `MalOriginTag` turns anything other than `BetterMal` into a "via MAL" badge.

Current data on this device: 917 rows. 913 are `BetterMal`, 1 is `MalReconciliation` (Added) and 3 are `MalResync` (StatusChanged). None are `MalHeldDecline`. The second device has its own database with its own mix. The backend runs `db.Database.Migrate()` at startup, so a new migration applies the moment the rebuilt container starts.

This is the first of a series (01–04) that makes the log travel between my two devices. The proposal gives the motivation. Here, the point is to make each device's log hold only what was done on that device.

## Goals / Non-Goals

**Goals:**
- The three sync paths write nothing to the activity log, and apply exactly what they apply today.
- Declining a held change stays recorded.
- `ActivityChangeSource`, `ActivityLog.Source`, the column, `ActivityFeedItemDto.Source`, the frontend type field and `MalOriginTag` are all gone.
- Rows previously written by the sync paths are deleted, on any device the migration runs on.
- Settings copy stops promising "marked as coming from MyAnimeList".

**Non-Goals:**
- Export or import of the log (later changes 03/04).
- Recording changes made on MyAnimeList's own site in any other way.
- Unifying how `UserAnimeEntryEditService` and `AiringWatchStatusService` build rows with `EntryActivityRecorder`.
- Anything to do with an anime's *source material* (`AnimeMetadata.Source`, `AnimeDetailDto.Source`, `formatSource` on `AnimeDetailPage`).

## Decisions

### D1. Don't write sync rows, rather than filter or deduplicate them

The three sync paths lose their `db.ActivityLogs.Add/AddRange` calls, along with the `EntrySnapshot` bookkeeping that existed only to feed them. In `ReconciliationService` that is the `before` local and its branches. In `ResyncService` it is the entry `before` snapshot, not the metadata `changeDetector.Snapshot`, which still feeds anime-updates.

*Alternatives:*
- Keep writing and hide the rows in the UI. Rejected: they would still travel in an export.
- Keep writing and deduplicate on import. Rejected: a reconciliation row is a net 5 → 8 where the originating device logged 5 → 6 → 7 → 8, at a different time. Matching them is heuristic.

Not writing is exact.

### D2. A declined held change stays recorded — the rule is "record what only happened here"

A reconciliation diff or re-sync adopts a value that was set somewhere else. If it was set in the app on the other device, that device logged it. Declining a held change instead **undoes** a row already in this device's log: my unsent edit. Only this device knows that edit was abandoned. Without a record, the log would still show my edit as the last thing I did to that anime. The destructive case, deleting the entry when MyAnimeList has none, must leave a trace. The same principle already covers the automatic completion and reopening. Both are applied by this device and recorded here only, and they reach the other device through MyAnimeList, where they aren't recorded again.

The activity-recording spec now lists the automatic completion ("A caught-up entry is completed once its full run is known") explicitly. It always wrote a row, but the old requirement named only the reopening.

### D3. Remove the column rather than keep it with fewer values

Once the sync paths stop writing, only `BetterMal` and `MalHeldDecline` would ever be stored. Nothing needs the distinction: the badge goes (D8), and both queries that filter on it would match every relevant row once sync rows are gone (D5, D6). A column with a near-fixed value invites future code to branch on it, and it would have to travel in the export format for no reason.

### D4. The migration deletes sync rows by value, not "everything that isn't `BetterMal`"

`Up` runs, in order:
1. `DELETE FROM "ActivityLogs" WHERE "Source" IN ('MalStartupImport', 'MalReconciliation', 'MalResync')`
2. `DropColumn("Source")`

Filtering on `<> 'BetterMal'` would also delete `MalHeldDecline` rows. There are none today, but a decline made before deployment, or on the second device, would write one, and D2 says those are my actions. Deleting by value rather than by the four known ids also makes the migration correct on the second device, whose rows differ.

`Down` re-adds the column with default `'BetterMal'`. That restores the schema only: the deleted rows come back only from the dump, and restored held-decline rows would read as `BetterMal`. The migration's comment says so.

The migration and the updated model snapshot come from `dotnet ef migrations add RemoveActivityLogSource`, run in the `mcr.microsoft.com/dotnet/sdk:10.0` image. The local SDK is 9.0. Docker can't bind-mount `~/Documents`, so the backend is copied to `/private/tmp` first. There's no `dotnet-ef` tool manifest, so the tool is installed inside the container. The delete is then added to `Up` by hand, before `DropColumn`.

### D5. The recap query drops its filter, and declined-held-change progress now counts

`GetEpisodeProgressInRangeAsync` loses `&& l.Source == ActivityChangeSource.BetterMal`. `IActivityLogRepository`'s doc is rewritten: it returns every episode row in the range, because the log holds only changes made in the app.

**This corrects the brief's claim that the recap is unaffected.** Today, `MalHeldDecline` episode rows are excluded from recaps. After this change they count. `RecapWatchLog` sums positive increases, so a decline that raises an entry from 5 to 7 adds 2 episodes to the period that holds the decline. A decline that lowers the count contributes nothing, as today. That follows directly from D2 and D3: once there's no column, nothing can tell a decline row apart. The `list-recaps` delta states it as a scenario.

*Alternatives:*
- Keep a marker only for decline rows. Rejected: this reintroduces the column.
- Record declines without their episode row. Rejected: this breaks the granularity rule that every applied field is recorded.

The case needs three things at once: a change held at startup, MyAnimeList ahead on episodes, and choosing Decline. It is rare.

### D6. The unsent-changes query drops its filter, with no visible effect

`GetUnsentChangesAsync` loses its `Source` clause. The only rows it newly admits are decline rows. `ApplyTo` and `ToUserAnimeEntry` stamp `LastSyncedAt = now` with the same `now` the decline rows carry, and the query's `Timestamp > LastSyncedAt` is strict, so those rows are never listed. A declined entry also stops being held. Rows from the automatic completion and reopening were already `BetterMal` and already listed.

### D7. `isBaselineRun` goes, and `InitialImportService` stops touching the log

The flag, its comment, the `ImportOneAsync` parameter and both guarded `Add` calls are removed. So is the `Services.Entries` using, if nothing else in the file needs it. The resumability of the import itself (skip anime already in `AnimeMetadata`) is untouched.

### D8. `EntryActivityRecorder` loses `source`, and its only caller becomes `HeldChangeService`

**This corrects the brief:** the local edit path doesn't use the recorder. `UserAnimeEntryEditService` and `AiringWatchStatusService` build their rows inline, which stays as it is (non-goal). `Added` and `Diff` drop the parameter and every `Source =`. The class comment is rewritten to describe it as building the rows for applying MyAnimeList's current value to an entry when I decline a held change, at the same granularity the local edit path produces. It stays a separate pure class rather than being folded into `HeldChangeService`, because `EntryActivityRecorderTests` pin the granularity and ordering rules against it. `HeldChangeService`'s inline removal row just drops its `Source =` line.

### D9. The frontend returns rows to the markup they had before the badge

`MalOriginTag.tsx` and `.css` are deleted. From `api/types.ts`, the `ActivityChangeSource` type (with its comment) and `ActivityFeedItemDto.source` are removed. Commit 41aa4cd split each row's text into a flex wrapper plus a text span only so the badge could sit beside it:
- `.edit-history__detail` / `__detail-text`
- `.profile-list-row__meta` / `__meta-text`

Each pair collapses back into one span carrying the text styles in effect now, including the ellipsis truncation, so the rows look identical without a badge. The field was optional on the frontend type, so a stale frontend against the new backend just renders unmarked rows. No deployment ordering is needed.

### D10. Settings copy

The three sentences in `SettingsPage.tsx` become:
- **Held changes:** "What declining applies is recorded in your edit history, like any other change you make here."
- **Reconciliation diff:** "Nothing you accept is recorded in Latest updates or the full edit history."
- **Re-sync:** "Nothing it applies is recorded in Latest updates or the full edit history."

The wording can be polished as long as it meets the `settings-page` delta.

### D11. Tests follow the specs, and origin-only tests go

- **Import, reconciliation and re-sync activity tests:** rewritten to assert that no rows are written, keeping one test per applied branch (changed entry, new entry). The baseline and "once history exists" tests fold into "an import records nothing". `ResyncServiceActivityTests`' two anime-updates tests stay as they are.
- **`HeldChangeServiceActivityTests`:** loses its `Source` asserts, and gains the "declining a held removal MyAnimeList still lists" case from the spec.
- **`RecapMalOriginTests`:** deleted. A recap test for "progress applied by declining a held change counts" replaces it.
- **`ActivityLogRepositoryTests`' origin tests:** become one "every episode row in range" test.
- **`ProfileServiceMalOriginTests`:** apart from `Source`, it asserts interleaving, feed filtering, completion-plus-score merging and episode-run collapsing. Each case is checked against the existing Profile tests; any not covered elsewhere moves there, without `Source`, and the file is deleted.

## Risks / Trade-offs

- **[The deletion is irreversible]** → Take a `pg_dump` before the rebuilt backend starts. The migration runs at startup, so "before the migration" means before `docker compose up --build`. Rollback is restoring the dump; `Down` alone brings back only the column.
- **[The second device still writes sync rows until it is upgraded]** → Upgrade both devices, each with its own dump, before any export/import change lands. Because the migration deletes by value, it cleans the second device's rows too.
- **[A decline can overlap the other device's rows]** When MyAnimeList was ahead because the other device pushed, the decline's 5 → 7 row and the other device's 5 → 6, 6 → 7 rows all survive a merge of the two logs. → Accepted by D2. Rare.
- **[Recaps count decline progress]** (D5) → Accepted and written into the `list-recaps` delta. Also rare.
- **[Changes made on MyAnimeList's own site leave no history anywhere]** → Accepted consequence, stated in the specs and the Settings copy.

## Migration Plan

1. Take a dump of the running database to a location outside the repository, and confirm `pg_restore --list` can read it.
2. `docker compose up -d --build backend frontend`. The backend applies `RemoveActivityLogSource` on start.
3. Verify:
   - `ActivityLogs` has no `Source` column.
   - The row count is 913 on this device.
   - The profile feed and edit history render with no badges.
   - The Settings copy reads as specified.
4. Repeat steps 1–3 on the second device. Its expected count is its own `BetterMal` + `MalHeldDecline` total, taken before upgrading.

**Rollback:** restore the dump with `pg_restore --clean` into the stopped stack's database, check out the previous commit, and rebuild.

## Open Questions

- **For 03/04, not this change:** both devices run `AiringWatchStatusService` independently. If both see the same entry before either one's result syncs through MyAnimeList, both record the same automatic completion or reopening, and a merged log holds it twice. It's worth deciding how the import step treats those rows.
